/**
 * @file bq40z50_driver.h
 * @brief TI BQ40z50 battery gauge driver
 *
 * REQ-FW-090: Kernel 6.6 port from 4.4
 * REQ-FW-091: 6 battery metrics (SOC, voltage, current, temperature, remaining capacity, full charge capacity)
 * REQ-FW-092: Low battery shutdown (10% warning, 5% emergency)
 *
 * SBS (Smart Battery System) Register Map:
 * - 0x08: Temperature (0.1 K)
 * - 0x09: Voltage (mV)
 * - 0x0A: Current (mA)
 * - 0x0D: State of Charge (%)
 * - 0x0F: Remaining Capacity (mAh)
 * - 0x10: Full Charge Capacity (mAh)
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_BQ40Z50_DRIVER_H
#define DETECTOR_BQ40Z50_DRIVER_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* ==========================================================================
 * Constants
 * ========================================================================== */

#define BQ40Z50_I2C_ADDR          0x0B    /* Default 7-bit I2C address */
#define BQ40Z50_I2C_ADDR_8BIT     0x16    /* 8-bit address for Linux */

/* SBS Register Addresses */
#define BQ40Z50_REG_TEMPERATURE       0x08    /* Temperature (0.1 K) */
#define BQ40Z50_REG_VOLTAGE          0x09    /* Voltage (mV) */
#define BQ40Z50_REG_CURRENT          0x0A    /* Current (mA) */
#define BQ40Z50_REG_SOC              0x0D    /* State of Charge (%) */
#define BQ40Z50_REG_REMAIN_CAP       0x0F    /* Remaining Capacity (mAh) */
#define BQ40Z50_REG_FULL_CHG_CAP     0x10    /* Full Charge Capacity (mAh) */
#define BQ40Z50_REG_SERIAL           0x1C    /* Serial Number */
#define BQ40Z50_REG_DEVICE_CHEMISTRY 0x22    /* Device Chemistry */

/* Battery Thresholds (REQ-FW-092) */
#define BQ40Z50_LOW_BATTERY_WARNING   10     /* 10% - warning level */
#define BQ40Z50_LOW_BATTERY_EMERGENCY 5      /* 5% - emergency shutdown */

/* Valid Ranges */
#define BQ40Z50_SOC_MIN               0      /* 0% */
#define BQ40Z50_SOC_MAX             100      /* 100% */
#define BQ40Z50_VOLTAGE_MIN         2800     /* 2.8V - minimum safe voltage */
#define BQ40Z50_VOLTAGE_MAX         4200     /* 4.2V - fully charged */
#define BQ40Z50_TEMP_MIN            2731     /* 0 C in 0.1 K */
#define BQ40Z50_TEMP_MAX            3331     /* 60 C in 0.1 K */

/* ==========================================================================
 * Types
 * ========================================================================== */

/**
 * @brief Battery metrics (REQ-FW-091)
 */
typedef struct {
    uint8_t state_of_charge;        /* 0-100 % */
    uint16_t voltage;               /* mV */
    int16_t current;                /* mA (negative = discharge, positive = charge) */
    uint16_t temperature;           /* 0.1 K (e.g., 2982 = 298.2 K = 25 C) */
    uint16_t remaining_capacity;    /* mAh */
    uint16_t full_charge_capacity;  /* mAh */
} battery_metrics_t;

/**
 * @brief BQ40z50 driver context
 */
typedef struct {
    int i2c_fd;                     /* I2C device file descriptor */
    uint8_t i2c_addr;               /* I2C slave address (7-bit) */
    bool initialized;               /* Initialization flag */
    bool low_battery_warning;       /* Low battery warning flag (10%) */
    bool emergency_shutdown;        /* Emergency shutdown flag (5%) */
    battery_metrics_t last_metrics; /* Last read metrics */
} bq40z50_context_t;

/* ==========================================================================
 * API Functions
 * ========================================================================== */

/**
 * @brief Initialize BQ40z50 driver
 * @param ctx Driver context
 * @param i2c_device I2C device path (e.g., "/dev/i2c-1")
 * @param i2c_addr I2C 7-bit slave address (default: 0x0B)
 * @return 0 on success, negative error code on failure
 *
 * Opens I2C device, sets slave address, reads initial metrics.
 */
int bq40z50_init(bq40z50_context_t *ctx, const char *i2c_device, uint8_t i2c_addr);

/**
 * @brief Read all battery metrics
 * @param ctx Driver context
 * @param metrics Pointer to metrics structure to fill
 * @return 0 on success, negative error code on failure
 *
 * Reads all 6 required metrics per REQ-FW-091:
 * - State of charge (%)
 * - Voltage (mV)
 * - Current (mA)
 * - Temperature (0.1 K)
 * - Remaining capacity (mAh)
 * - Full charge capacity (mAh)
 */
int bq40z50_read_metrics(bq40z50_context_t *ctx, battery_metrics_t *metrics);

/**
 * @brief Check if battery is at warning level
 * @param ctx Driver context
 * @return true if SOC <= 10%, false otherwise
 *
 * REQ-FW-092: Low battery warning at 10% SOC.
 */
bool bq40z50_is_low_battery(const bq40z50_context_t *ctx);

/**
 * @brief Check if battery is at emergency shutdown level
 * @param ctx Driver context
 * @return true if SOC <= 5%, false otherwise
 *
 * REQ-FW-092: Emergency shutdown at 5% SOC.
 */
bool bq40z50_emergency_shutdown(const bq40z50_context_t *ctx);

/**
 * @brief Get battery state of charge
 * @param ctx Driver context
 * @return SOC (0-100%) or negative error code
 */
int bq40z50_get_soc(bq40z50_context_t *ctx);

/**
 * @brief Get battery voltage
 * @param ctx Driver context
 * @return Voltage in mV or negative error code
 */
int bq40z50_get_voltage(bq40z50_context_t *ctx);

/**
 * @brief Get battery current
 * @param ctx Driver context
 * @return Current in mA or negative error code (negative = discharge)
 */
int bq40z50_get_current(bq40z50_context_t *ctx);

/**
 * @brief Get battery temperature
 * @param ctx Driver context
 * @return Temperature in 0.1 K or negative error code
 */
int bq40z50_get_temperature(bq40z50_context_t *ctx);

/**
 * @brief Close BQ40z50 driver
 * @param ctx Driver context
 */
void bq40z50_cleanup(bq40z50_context_t *ctx);

/* ==========================================================================
 * Internal Functions (for testing)
 * ========================================================================== */

#ifdef TESTING
/**
 * @brief Read SBS register via I2C SMBus
 * @param ctx Driver context
 * @param reg Register address
 * @param value Pointer to store read value
 * @return 0 on success, negative error code on failure
 */
int bq40z50_read_sbs_reg(bq40z50_context_t *ctx, uint8_t reg, uint16_t *value);

/**
 * @brief Write SBS register via I2C SMBus
 * @param ctx Driver context
 * @param reg Register address
 * @param value Value to write
 * @return 0 on success, negative error code on failure
 */
int bq40z50_write_sbs_reg(bq40z50_context_t *ctx, uint8_t reg, uint16_t value);
#endif

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_BQ40Z50_DRIVER_H */
