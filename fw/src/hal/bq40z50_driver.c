/**
 * @file bq40z50_driver.c
 * @brief TI BQ40z50 battery gauge driver implementation
 *
 * REQ-FW-090: Kernel 6.6 port from 4.4 (user-space SMBus fallback)
 * REQ-FW-091: 6 battery metrics
 * REQ-FW-092: Low battery shutdown (10% warning, 5% emergency)
 *
 * DDD Analysis:
 * - Kernel 4.4 driver used i2c_smbus_read_word_data() kernel API
 * - Kernel 6.6 has similar API but requires porting
 * - Fallback: Use user-space SMBus via /dev/i2c-X
 * - Behavior: All reads are word-sized (16-bit) in little-endian format
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "hal/bq40z50_driver.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <linux/i2c-dev.h>
#include <errno.h>

/* ==========================================================================
 * Internal Constants
 * ========================================================================== */

#define I2C_SLAVE_FORCE 0x0706

/* ==========================================================================
 * Internal Helper Functions
 * ========================================================================== */

/**
 * @brief Set I2C slave address
 */
static int set_i2c_slave(int fd, uint8_t addr) {
    if (ioctl(fd, I2C_SLAVE_FORCE, addr) < 0) {
        return -errno;
    }
    return 0;
}

/**
 * @brief Read word from I2C SMBus
 */
static int i2c_smbus_read_word(int fd, uint8_t reg) {
    /* Use I2C block read for word data */
    unsigned char buf[2];
    struct i2c_rdwr_ioctl_data ioctl_data;
    struct i2c_msg msgs[2];

    /* Write register address */
    buf[0] = reg;

    msgs[0].addr = 0;  /* Will be set by ioctl */
    msgs[0].flags = 0;
    msgs[0].len = 1;
    msgs[0].buf = buf;

    /* Read word data */
    msgs[1].addr = 0;  /* Will be set by ioctl */
    msgs[1].flags = I2C_M_RD;
    msgs[1].len = 2;
    msgs[1].buf = buf;

    ioctl_data.msgs = msgs;
    ioctl_data.nmsgs = 2;

    if (ioctl(fd, I2C_RDWR, &ioctl_data) < 0) {
        return -errno;
    }

    /* SMBus word data is little-endian */
    return (int)((uint16_t)buf[1] << 8 | (uint16_t)buf[0]);
}

/* ==========================================================================
 * API Implementation
 * ========================================================================== */

int bq40z50_init(bq40z50_context_t *ctx, const char *i2c_device, uint8_t i2c_addr) {
    if (ctx == NULL || i2c_device == NULL) {
        return -EINVAL;
    }

    memset(ctx, 0, sizeof(*ctx));

    /* Open I2C device */
    ctx->i2c_fd = open(i2c_device, O_RDWR);
    if (ctx->i2c_fd < 0) {
        return -errno;
    }

    /* Set slave address */
    ctx->i2c_addr = i2c_addr;
    int ret = set_i2c_slave(ctx->i2c_fd, i2c_addr);
    if (ret < 0) {
        close(ctx->i2c_fd);
        return ret;
    }

    /* Read initial metrics */
    ret = bq40z50_read_metrics(ctx, &ctx->last_metrics);
    if (ret < 0) {
        close(ctx->i2c_fd);
        return ret;
    }

    /* Initialize warning flags */
    ctx->low_battery_warning = bq40z50_is_low_battery(ctx);
    ctx->emergency_shutdown = bq40z50_emergency_shutdown(ctx);

    ctx->initialized = true;

    return 0;
}

int bq40z50_read_sbs_reg(bq40z50_context_t *ctx, uint8_t reg, uint16_t *value) {
    if (ctx == NULL || !ctx->initialized || value == NULL) {
        return -EINVAL;
    }

    /* Ensure slave address is still set */
    set_i2c_slave(ctx->i2c_fd, ctx->i2c_addr);

    /* Read word from register */
    int ret = i2c_smbus_read_word(ctx->i2c_fd, reg);
    if (ret < 0) {
        return ret;
    }

    *value = (uint16_t)ret;
    return 0;
}

int bq40z50_write_sbs_reg(bq40z50_context_t *ctx, uint8_t reg, uint16_t value) {
    if (ctx == NULL || !ctx->initialized) {
        return -EINVAL;
    }

    /* Ensure slave address is still set */
    set_i2c_slave(ctx->i2c_fd, ctx->i2c_addr);

    /* Write word to register (not commonly used for BQ40z50) */
    unsigned char buf[3];
    buf[0] = reg;
    buf[1] = value & 0xFF;        /* LSB */
    buf[2] = (value >> 8) & 0xFF; /* MSB */

    struct i2c_rdwr_ioctl_data ioctl_data;
    struct i2c_msg msg;

    msg.addr = 0;  /* Will be set by ioctl */
    msg.flags = 0;
    msg.len = 3;
    msg.buf = buf;

    ioctl_data.msgs = &msg;
    ioctl_data.nmsgs = 1;

    if (ioctl(ctx->i2c_fd, I2C_RDWR, &ioctl_data) < 0) {
        return -errno;
    }

    return 0;
}

int bq40z50_read_metrics(bq40z50_context_t *ctx, battery_metrics_t *metrics) {
    if (ctx == NULL || !ctx->initialized || metrics == NULL) {
        return -EINVAL;
    }

    int ret;
    uint16_t value;

    /* Read temperature (0x08) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_TEMPERATURE, &value);
    if (ret < 0) return ret;
    metrics->temperature = value;

    /* Read voltage (0x09) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_VOLTAGE, &value);
    if (ret < 0) return ret;
    metrics->voltage = value;

    /* Read current (0x0A) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_CURRENT, &value);
    if (ret < 0) return ret;
    /* Current is signed 16-bit */
    metrics->current = (int16_t)value;

    /* Read state of charge (0x0D) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_SOC, &value);
    if (ret < 0) return ret;
    metrics->state_of_charge = (uint8_t)value;

    /* Read remaining capacity (0x0F) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_REMAIN_CAP, &value);
    if (ret < 0) return ret;
    metrics->remaining_capacity = value;

    /* Read full charge capacity (0x10) */
    ret = bq40z50_read_sbs_reg(ctx, BQ40Z50_REG_FULL_CHG_CAP, &value);
    if (ret < 0) return ret;
    metrics->full_charge_capacity = value;

    /* Update cached metrics and warning flags */
    memcpy(&ctx->last_metrics, metrics, sizeof(*metrics));
    ctx->low_battery_warning = bq40z50_is_low_battery(ctx);
    ctx->emergency_shutdown = bq40z50_emergency_shutdown(ctx);

    return 0;
}

bool bq40z50_is_low_battery(const bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return false;
    }

    return ctx->last_metrics.state_of_charge <= BQ40Z50_LOW_BATTERY_WARNING;
}

bool bq40z50_emergency_shutdown(const bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return false;
    }

    return ctx->last_metrics.state_of_charge <= BQ40Z50_LOW_BATTERY_EMERGENCY;
}

int bq40z50_get_soc(bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return -EINVAL;
    }

    return (int)ctx->last_metrics.state_of_charge;
}

int bq40z50_get_voltage(bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return -EINVAL;
    }

    return (int)ctx->last_metrics.voltage;
}

int bq40z50_get_current(bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return -EINVAL;
    }

    return (int)ctx->last_metrics.current;
}

int bq40z50_get_temperature(bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return -EINVAL;
    }

    return (int)ctx->last_metrics.temperature;
}

void bq40z50_cleanup(bq40z50_context_t *ctx) {
    if (ctx == NULL || !ctx->initialized) {
        return;
    }

    if (ctx->i2c_fd >= 0) {
        close(ctx->i2c_fd);
        ctx->i2c_fd = -1;
    }

    ctx->initialized = false;
}
