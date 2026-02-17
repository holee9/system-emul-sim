/**
 * @file crc16.h
 * @brief CRC-16/CCITT polynomial utility for frame header validation
 *
 * Implements CRC-16/CCITT (polynomial 0x1021) as specified in REQ-FW-042.
 * Used for frame header integrity checking in UDP packet transmission.
 *
 * REQ-FW-042: The frame header CRC-16 shall be computed over the header
 * fields (excluding the CRC field itself) using CRC-16/CCITT polynomial.
 */

#ifndef DETECTOR_UTIL_CRC16_H
#define DETECTOR_UTIL_CRC16_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief CRC-16/CCITT polynomial (0x1021)
 *
 * Polynomial: x^16 + x^12 + x^5 + 1
 * Initial value: 0xFFFF
 * Final XOR: 0x0000
 * Reverse input: No
 * Reverse output: No
 */
#define CRC16_CCITT_POLY 0x1021
#define CRC16_INITIAL_VALUE 0xFFFF

/**
 * @brief Compute CRC-16/CCITT checksum
 *
 * @param data Input data buffer
 * @param len Length of data in bytes
 * @return uint16_t CRC-16 checksum
 *
 * @note This function computes CRC over the entire buffer.
 * For frame header CRC, pass the header excluding the CRC field.
 */
uint16_t crc16_compute(const uint8_t *data, size_t len);

/**
 * @brief Compute CRC-16/CCITT with initial value
 *
 * @param data Input data buffer
 * @param len Length of data in bytes
 * @param initial Initial CRC value (for incremental computation)
 * @return uint16_t CRC-16 checksum
 */
uint16_t crc16_compute_with_init(const uint8_t *data, size_t len, uint16_t initial);

/**
 * @brief Verify CRC-16/CCITT checksum
 *
 * @param data Input data buffer
 * @param len Length of data including 2-byte CRC at end
 * @param expected_crc Expected CRC value
 * @return int 1 if CRC matches, 0 if mismatch
 */
int crc16_verify(const uint8_t *data, size_t len, uint16_t expected_crc);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_UTIL_CRC16_H */
