/**
 * @file mock_sequence.c
 * @brief Mock functions for Sequence Engine testing
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdint.h>
#include <stdbool.h>

/* Mock FPGA status */
static uint16_t mock_fpga_status_reg = 0;
static bool mock_fpga_error_state = false;

/* Mock SPI state */
static uint8_t mock_spi_last_addr = 0;
static uint16_t mock_spi_last_value = 0;

/* Mock sequence state */
static uint32_t mock_last_seq = 0;

/* Mock HMAC state */
static bool mock_hmac_valid = true;

/**
 * @brief Reset all mocks
 */
void mock_spi_reset(void) {
    mock_spi_last_addr = 0;
    mock_spi_last_value = 0;
}

/**
 * @brief Set FPGA status register
 */
void mock_fpga_set_status(uint16_t status) {
    mock_fpga_status_reg = status;
}

/**
 * @brief Set FPGA error state
 */
void mock_fpga_set_error(bool error) {
    mock_fpga_error_state = error;
    if (error) {
        mock_fpga_status_reg |= FPGA_STATUS_ERROR;
    } else {
        mock_fpga_status_reg &= ~FPGA_STATUS_ERROR;
    }
}

/**
 * @brief Set mock SPI register value
 */
void mock_spi_set_register(uint8_t addr, uint16_t value) {
    mock_spi_last_addr = addr;
    mock_spi_last_value = value;
}

/**
 * @brief Set mock last sequence number
 */
void mock_seq_set_last(uint32_t seq) {
    mock_last_seq = seq;
}

/**
 * @brief Set mock HMAC validity
 */
void mock_hmac_set_valid(bool valid) {
    mock_hmac_valid = valid;
}
