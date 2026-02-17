/**
 * @file frame_header.h
 * @brief Frame Header Protocol for UDP transmission
 *
 * REQ-FW-040~042: Frame header formatting for UDP transmission.
 * - Frame fragmentation with header
 * - TX within 1 frame period
 * - CRC-16/CCITT
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_FRAME_HEADER_H
#define DETECTOR_FRAME_HEADER_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Frame magic number */
#define FRAME_HEADER_MAGIC       0xD7E01234u
#define FRAME_HEADER_SIZE        32u

/* Frame flags */
#define FRAME_FLAG_FIRST_PACKET  (1u << 0)
#define FRAME_FLAG_LAST_PACKET   (1u << 1)
#define FRAME_FLAG_DROP_INDICATOR (1u << 15)

/* Maximum payload size per packet */
#define MAX_PAYLOAD_SIZE  8192

/**
 * @brief Frame header structure (32 bytes)
 */
typedef struct {
    uint32_t magic;           /* 0x00: Magic number (0xD7E01234) */
    uint32_t frame_number;    /* 0x04: Frame sequence number */
    uint16_t packet_index;    /* 0x08: Packet index in frame */
    uint16_t total_packets;   /* 0x0A: Total packets for this frame */
    uint16_t payload_len;     /* 0x0C: Payload length in bytes */
    uint16_t flags;           /* 0x0E: Frame flags */
    uint32_t reserved;        /* 0x10: Reserved (must be 0) */
    uint32_t reserved2;       /* 0x14: Reserved (must be 0) */
    uint64_t timestamp_ns;    /* 0x18: Timestamp in nanoseconds */
    uint16_t crc16;           /* 0x22: CRC-16/CCITT (bytes 0-27) */
    uint16_t reserved3;       /* 0x24: Reserved (must be 0) */
} __attribute__((packed)) frame_header_t;

/**
 * @brief Encode frame header
 *
 * @param buf Buffer to encode header into
 * @param buf_size Buffer size
 * @param frame_number Frame sequence number
 * @param packet_index Packet index
 * @param total_packets Total packets
 * @param payload_len Payload length
 * @param flags Frame flags
 * @param timestamp_ns Timestamp in nanoseconds
 * @return 0 on success, -errno on failure
 */
int frame_header_encode(uint8_t *buf, size_t buf_size,
                        uint32_t frame_number,
                        uint16_t packet_index,
                        uint16_t total_packets,
                        uint16_t payload_len,
                        uint16_t flags,
                        uint64_t timestamp_ns);

/**
 * @brief Decode frame header
 *
 * @param buf Buffer containing encoded header
 * @param buf_size Buffer size
 * @param frame_number Pointer to store frame number
 * @param packet_index Pointer to store packet index
 * @param total_packets Pointer to store total packets
 * @param payload_len Pointer to store payload length
 * @param flags Pointer to store flags
 * @param timestamp_ns Pointer to store timestamp
 * @param crc_valid Pointer to store CRC validity
 * @return 0 on success, -errno on failure
 */
int frame_header_decode(const uint8_t *buf, size_t buf_size,
                        uint32_t *frame_number,
                        uint16_t *packet_index,
                        uint16_t *total_packets,
                        uint16_t *payload_len,
                        uint16_t *flags,
                        uint64_t *timestamp_ns,
                        bool *crc_valid);

/**
 * @brief Calculate total packets for frame size
 *
 * @param frame_size Frame size in bytes
 * @param payload_size Payload size per packet
 * @return Total packets needed
 */
uint32_t frame_header_calc_packets(size_t frame_size, size_t payload_size);

/**
 * @brief Validate CRC-16 in frame header
 *
 * @param header Frame header to validate
 * @return true if CRC valid, false otherwise
 */
bool frame_header_verify_crc(const frame_header_t *header);

/**
 * @brief Build frame header structure
 *
 * @param header Pointer to header structure
 * @param frame_number Frame sequence number
 * @param packet_index Packet index
 * @param total_packets Total packets
 * @param payload_len Payload length
 * @param flags Frame flags
 * @param timestamp_ns Timestamp in nanoseconds
 */
void frame_header_build(frame_header_t *header,
                        uint32_t frame_number,
                        uint16_t packet_index,
                        uint16_t total_packets,
                        uint16_t payload_len,
                        uint16_t flags,
                        uint64_t timestamp_ns);

/**
 * @brief Convert flags to string
 *
 * @param flags Frame flags
 * @return String representation
 */
const char *frame_header_flags_to_string(uint16_t flags);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_FRAME_HEADER_H */
