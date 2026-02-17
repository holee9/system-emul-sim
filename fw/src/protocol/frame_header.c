/**
 * @file frame_header.c
 * @brief Frame Header Protocol for UDP transmission
 *
 * REQ-FW-040~042: Frame header formatting for UDP transmission.
 * - Frame fragmentation with header
 * - TX within 1 frame period
 * - CRC-16/CCITT
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "protocol/frame_header.h"
#include "util/crc16.h"
#include <errno.h>
#include <string.h>

/* CRC-16 calculation offset (CRC covers bytes 0-27) */
#define FRAME_HEADER_CRC_OFFSET  28

/* Little-endian encoding helpers */
static void encode_le16(uint8_t *buf, uint16_t value) {
    buf[0] = value & 0xFF;
    buf[1] = (value >> 8) & 0xFF;
}

static void encode_le32(uint8_t *buf, uint32_t value) {
    buf[0] = value & 0xFF;
    buf[1] = (value >> 8) & 0xFF;
    buf[2] = (value >> 16) & 0xFF;
    buf[3] = (value >> 24) & 0xFF;
}

static void encode_le64(uint8_t *buf, uint64_t value) {
    buf[0] = value & 0xFF;
    buf[1] = (value >> 8) & 0xFF;
    buf[2] = (value >> 16) & 0xFF;
    buf[3] = (value >> 24) & 0xFF;
    buf[4] = (value >> 32) & 0xFF;
    buf[5] = (value >> 40) & 0xFF;
    buf[6] = (value >> 48) & 0xFF;
    buf[7] = (value >> 56) & 0xFF;
}

/* Little-endian decoding helpers */
static uint16_t decode_le16(const uint8_t *buf) {
    return buf[0] | (buf[1] << 8);
}

static uint32_t decode_le32(const uint8_t *buf) {
    return buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
}

static uint64_t decode_le64(const uint8_t *buf) {
    return (uint64_t)buf[0] |
           ((uint64_t)buf[1] << 8) |
           ((uint64_t)buf[2] << 16) |
           ((uint64_t)buf[3] << 24) |
           ((uint64_t)buf[4] << 32) |
           ((uint64_t)buf[5] << 40) |
           ((uint64_t)buf[6] << 48) |
           ((uint64_t)buf[7] << 56);
}

/* ==========================================================================
 * Public API
 * ========================================================================== */

/**
 * @brief Encode frame header
 */
int frame_header_encode(uint8_t *buf, size_t buf_size,
                        uint32_t frame_number,
                        uint16_t packet_index,
                        uint16_t total_packets,
                        uint16_t payload_len,
                        uint16_t flags,
                        uint64_t timestamp_ns) {
    if (buf == NULL) {
        return -EINVAL;
    }

    if (buf_size < FRAME_HEADER_SIZE) {
        return -EINVAL;
    }

    /* Clear buffer */
    memset(buf, 0, FRAME_HEADER_SIZE);

    /* Encode magic number */
    encode_le32(buf + 0, FRAME_HEADER_MAGIC);

    /* Encode frame number */
    encode_le32(buf + 4, frame_number);

    /* Encode packet index */
    encode_le16(buf + 8, packet_index);

    /* Encode total packets */
    encode_le16(buf + 10, total_packets);

    /* Encode payload length */
    encode_le16(buf + 12, payload_len);

    /* Encode flags */
    encode_le16(buf + 14, flags);

    /* Reserved fields (already zeroed) */

    /* Encode timestamp */
    encode_le64(buf + 22, timestamp_ns);

    /* Calculate CRC over bytes 0-27 (excluding CRC field at 28-29) */
    uint16_t crc = crc16_ccitt(buf, FRAME_HEADER_CRC_OFFSET);
    encode_le16(buf + FRAME_HEADER_CRC_OFFSET, crc);

    return 0;
}

/**
 * @brief Decode frame header
 */
int frame_header_decode(const uint8_t *buf, size_t buf_size,
                        uint32_t *frame_number,
                        uint16_t *packet_index,
                        uint16_t *total_packets,
                        uint16_t *payload_len,
                        uint16_t *flags,
                        uint64_t *timestamp_ns,
                        bool *crc_valid) {
    if (buf == NULL) {
        return -EINVAL;
    }

    if (buf_size < FRAME_HEADER_SIZE) {
        return -EINVAL;
    }

    /* Validate magic number */
    uint32_t magic = decode_le32(buf + 0);
    if (magic != FRAME_HEADER_MAGIC) {
        return -EINVAL;
    }

    /* Decode fields */
    if (frame_number != NULL) {
        *frame_number = decode_le32(buf + 4);
    }

    if (packet_index != NULL) {
        *packet_index = decode_le16(buf + 8);
    }

    if (total_packets != NULL) {
        *total_packets = decode_le16(buf + 10);
    }

    if (payload_len != NULL) {
        *payload_len = decode_le16(buf + 12);
    }

    if (flags != NULL) {
        *flags = decode_le16(buf + 14);
    }

    if (timestamp_ns != NULL) {
        *timestamp_ns = decode_le64(buf + 22);
    }

    /* Validate CRC */
    if (crc_valid != NULL) {
        uint16_t expected_crc = decode_le16(buf + FRAME_HEADER_CRC_OFFSET);
        uint16_t calculated_crc = crc16_ccitt(buf, FRAME_HEADER_CRC_OFFSET);
        *crc_valid = (expected_crc == calculated_crc);
    }

    return 0;
}

/**
 * @brief Calculate total packets for frame size
 */
uint32_t frame_header_calc_packets(size_t frame_size, size_t payload_size) {
    if (payload_size == 0) {
        return 0;
    }

    /* Calculate number of packets needed */
    uint32_t packets = (frame_size + payload_size - 1) / payload_size;

    /* Ensure at least 1 packet */
    if (packets == 0) {
        packets = 1;
    }

    return packets;
}

/**
 * @brief Validate CRC-16 in frame header
 */
bool frame_header_verify_crc(const frame_header_t *header) {
    if (header == NULL) {
        return false;
    }

    /* Validate magic number */
    if (header->magic != FRAME_HEADER_MAGIC) {
        return false;
    }

    /* Calculate CRC over bytes 0-27 */
    const uint8_t *buf = (const uint8_t *)header;
    uint16_t calculated_crc = crc16_ccitt(buf, FRAME_HEADER_CRC_OFFSET);

    return (calculated_crc == header->crc16);
}

/**
 * @brief Build frame header structure
 */
void frame_header_build(frame_header_t *header,
                        uint32_t frame_number,
                        uint16_t packet_index,
                        uint16_t total_packets,
                        uint16_t payload_len,
                        uint16_t flags,
                        uint64_t timestamp_ns) {
    if (header == NULL) {
        return;
    }

    header->magic = FRAME_HEADER_MAGIC;
    header->frame_number = frame_number;
    header->packet_index = packet_index;
    header->total_packets = total_packets;
    header->payload_len = payload_len;
    header->flags = flags;
    header->reserved = 0;
    header->reserved2 = 0;
    header->timestamp_ns = timestamp_ns;
    header->reserved3 = 0;

    /* Calculate CRC */
    const uint8_t *buf = (const uint8_t *)header;
    header->crc16 = crc16_ccitt(buf, FRAME_HEADER_CRC_OFFSET);
}

/**
 * @brief Convert flags to string
 */
const char *frame_header_flags_to_string(uint16_t flags) {
    static char buffer[64];

    buffer[0] = '\0';

    if (flags & FRAME_FLAG_FIRST_PACKET) {
        strcat(buffer, "FIRST_PACKET ");
    }

    if (flags & FRAME_FLAG_LAST_PACKET) {
        strcat(buffer, "LAST_PACKET ");
    }

    if (flags & FRAME_FLAG_DROP_INDICATOR) {
        strcat(buffer, "DROP ");
    }

    /* Remove trailing space */
    size_t len = strlen(buffer);
    if (len > 0 && buffer[len - 1] == ' ') {
        buffer[len - 1] = '\0';
    }

    if (buffer[0] == '\0') {
        strcpy(buffer, "NONE");
    }

    return buffer;
}
