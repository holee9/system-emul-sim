/**
 * @file mock_spidev.h
 * @brief Mock SPI device interface for unit testing
 *
 * Provides mock implementations of spidev ioctls for testing
 * SPI Master HAL without real hardware.
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef MOCK_SPIDEV_H
#define MOCK_SPIDEV_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* SPI mock configuration */
typedef struct {
    int open_result;           /* Return value for open() */
    int transfer_result;       /* Return value for ioctl transfers */
    uint8_t *read_buffer;      /* Simulated RX buffer */
    size_t read_buffer_len;    /* Length of read buffer */
    int transfer_count;        /* Number of transfers performed */
    int fail_after_count;      /* Fail after N transfers (0 = never fail) */
    uint16_t register_map[256];/* Simulated FPGA registers */
    bool register_map_valid;   /* True if register map is initialized */
} mock_spidev_config_t;

/**
 * Initialize mock spidev with default configuration
 */
void mock_spidev_init_default(void);

/**
 * Set mock spidev configuration
 */
void mock_spidev_set_config(const mock_spidev_config_t *config);

/**
 * Get current mock configuration
 */
void mock_spidev_get_config(mock_spidev_config_t *config);

/**
 * Set a register value for read-back simulation
 */
void mock_spidev_set_register(uint8_t addr, uint16_t value);

/**
 * Get a register value
 */
uint16_t mock_spidev_get_register(uint8_t addr);

/**
 * Open mock spidev device
 * Returns a mock file descriptor (>= 0 on success)
 */
int mock_spidev_open(const char *device);

/**
 * Close mock spidev device
 */
int mock_spidev_close(int fd);

/**
 * Mock SPI transfer
 * Simulates spidev ioctl(SPI_IOC_MESSAGE)
 * Returns 0 on success, negative errno on failure
 */
int mock_spidev_transfer(int fd, const uint8_t *tx_buf, uint8_t *rx_buf, size_t len);

/**
 * Reset mock state
 */
void mock_spidev_reset(void);

/**
 * Get transfer count
 */
int mock_spidev_get_transfer_count(void);

#ifdef __cplusplus
}
#endif

#endif /* MOCK_SPIDEV_H */
