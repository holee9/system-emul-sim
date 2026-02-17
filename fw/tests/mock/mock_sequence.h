/**
 * @file mock_sequence.h
 * @brief Mock functions for Sequence Engine testing
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef MOCK_SEQUENCE_H
#define MOCK_SEQUENCE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* FPGA Status Register bits */
#define FPGA_STATUS_BUSY    (1U << 0)
#define FPGA_STATUS_ERROR   (1U << 1)
#define FPGA_STATUS_READY   (1U << 2)

/**
 * @brief Reset all mocks
 */
void mock_spi_reset(void);

/**
 * @brief Set FPGA status register
 */
void mock_fpga_set_status(uint16_t status);

/**
 * @brief Set FPGA error state
 */
void mock_fpga_set_error(bool error);

/**
 * @brief Set mock SPI register value
 */
void mock_spi_set_register(uint8_t addr, uint16_t value);

/**
 * @brief Set mock last sequence number
 */
void mock_seq_set_last(uint32_t seq);

/**
 * @brief Set mock HMAC validity
 */
void mock_hmac_set_valid(bool valid);

#ifdef __cplusplus
}
#endif

#endif /* MOCK_SEQUENCE_H */
