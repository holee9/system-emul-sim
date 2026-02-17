/**
 * @file mock_spidev.c
 * @brief Mock SPI device implementation for unit testing
 *
 * Simulates Linux spidev interface for testing SPI Master HAL.
 */

#include "mock_spidev.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>

/* Mock state */
static mock_spidev_config_t g_mock_config = {0};
static int g_initialized = 0;

/* Mock file descriptor base */
#define MOCK_FD_BASE 100

void mock_spidev_init_default(void) {
    memset(&g_mock_config, 0, sizeof(g_mock_config));
    g_mock_config.open_result = MOCK_FD_BASE;
    g_mock_config.transfer_result = 0;
    g_mock_config.fail_after_count = 0;
    g_mock_config.register_map_valid = 0;
    g_initialized = 1;
}

void mock_spidev_set_config(const mock_spidev_config_t *config) {
    if (config) {
        memcpy(&g_mock_config, config, sizeof(g_mock_config));
        g_initialized = 1;
    }
}

void mock_spidev_get_config(mock_spidev_config_t *config) {
    if (config) {
        memcpy(config, &g_mock_config, sizeof(g_mock_config));
    }
}

void mock_spidev_set_register(uint8_t addr, uint16_t value) {
    if (addr < 256) {
        g_mock_config.register_map[addr] = value;
        g_mock_config.register_map_valid = 1;
    }
}

uint16_t mock_spidev_get_register(uint8_t addr) {
    if (addr < 256 && g_mock_config.register_map_valid) {
        return g_mock_config.register_map[addr];
    }
    return 0;
}

int mock_spidev_open(const char *device) {
    (void)device;
    if (!g_initialized) {
        mock_spidev_init_default();
    }
    return g_mock_config.open_result;
}

int mock_spidev_close(int fd) {
    (void)fd;
    return 0;
}

int mock_spidev_transfer(int fd, const uint8_t *tx_buf, uint8_t *rx_buf, size_t len) {
    (void)fd;

    if (!g_initialized) {
        mock_spidev_init_default();
    }

    /* Check for failure condition */
    if (g_mock_config.fail_after_count > 0 &&
        g_mock_config.transfer_count >= g_mock_config.fail_after_count) {
        return -1;  /* Simulate failure */
    }

    g_mock_config.transfer_count++;

    /* Parse FPGA transaction format: [addr, rw, data_hi, data_lo] */
    if (len == 4 && tx_buf != NULL && rx_buf != NULL) {
        uint8_t addr = tx_buf[0];
        uint8_t rw = tx_buf[1];
        uint16_t data = (tx_buf[2] << 8) | tx_buf[3];

        /* Echo TX to RX (SPI behavior) */
        memcpy(rx_buf, tx_buf, len);

        /* Handle register read/write simulation */
        if (addr < 256) {
            if (rw & 0x01) {
                /* Write: store in register map */
                g_mock_config.register_map[addr] = data;
                g_mock_config.register_map_valid = 1;
            } else {
                /* Read: return stored value in data bytes */
                uint16_t reg_value = g_mock_config.register_map[addr];
                rx_buf[2] = (reg_value >> 8) & 0xFF;
                rx_buf[3] = reg_value & 0xFF;
            }
        }
    } else {
        /* For non-FPGA transactions, just echo */
        if (tx_buf != NULL && rx_buf != NULL) {
            memcpy(rx_buf, tx_buf, len);
        }
    }

    return g_mock_config.transfer_result;
}

void mock_spidev_reset(void) {
    mock_spidev_init_default();
}

int mock_spidev_get_transfer_count(void) {
    return g_mock_config.transfer_count;
}
