/**
 * @file test_frame_header.c
 * @brief Unit tests for Frame Header encode/decode (FW-UT-02)
 *
 * Test ID: FW-UT-02
 * Coverage: Frame header encoding/decoding per REQ-FW-040, REQ-FW-042
 *
 * Tests:
 * - Frame header encoding
 * - Frame header decoding
 * - CRC-16 calculation and validation
 * - Endianness handling (little-endian)
 * - Boundary conditions
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <string.h>

/* Frame header definitions per REQ-FW-040 */
#define FRAME_HEADER_MAGIC       0xD7E01234u  /* FPGA->Host direction */
#define FRAME_HEADER_SIZE        32u          /* Total header size in bytes */
#define FRAME_HEADER_CRC_OFFSET  28u          /* CRC field offset */
#define FRAME_HEADER_CRC_LEN     2u           /* CRC field size */

/* Frame flags per REQ-FW-040 */
#define FRAME_FLAG_FIRST_PACKET  (1u << 0)
#define FRAME_FLAG_LAST_PACKET   (1u << 1)
#define FRAME_FLAG_DROP_INDICATOR (1u << 15)

/* Function under test */
extern int frame_header_encode(uint8_t *buf, size_t buf_size,
                               uint32_t frame_number,
                               uint16_t packet_index,
                               uint16_t total_packets,
                               uint16_t payload_len,
                               uint16_t flags,
                               uint64_t timestamp_ns);

extern int frame_header_decode(const uint8_t *buf, size_t buf_size,
                               uint32_t *frame_number,
                               uint16_t *packet_index,
                               uint16_t *total_packets,
                               uint16_t *payload_len,
                               uint16_t *flags,
                               uint64_t *timestamp_ns,
                               bool *crc_valid);

extern uint16_t crc16_ccitt(const uint8_t *data, size_t len);

/* ==========================================================================
 * Helper Functions
 * ========================================================================== */

/**
 * Compare little-endian 16-bit value
 */
static bool le16_eq(const uint8_t *buf, uint16_t expected) {
    uint16_t actual = buf[0] | (buf[1] << 8);
    return actual == expected;
}

/**
 * Compare little-endian 32-bit value
 */
static bool le32_eq(const uint8_t *buf, uint32_t expected) {
    uint32_t actual = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
    return actual == expected;
}

/**
 * Compare little-endian 64-bit value
 */
static bool le64_eq(const uint8_t *buf, uint64_t expected) {
    uint64_t actual = (uint64_t)buf[0] |
                      ((uint64_t)buf[1] << 8) |
                      ((uint64_t)buf[2] << 16) |
                      ((uint64_t)buf[3] << 24) |
                      ((uint64_t)buf[4] << 32) |
                      ((uint64_t)buf[5] << 40) |
                      ((uint64_t)buf[6] << 48) |
                      ((uint64_t)buf[7] << 56);
    return actual == expected;
}

/* ==========================================================================
 * Encode Tests
 * ========================================================================== */

/**
 * @test FW_UT_02_001: Basic frame header encoding
 * @pre Valid buffer and parameters
 * @post Header encoded correctly, CRC valid
 */
static void test_frame_header_encode_basic(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];
    memset(buf, 0xAA, sizeof(buf));  /* Fill with pattern to detect overwrites */

    int result = frame_header_encode(buf, sizeof(buf),
                                     1,      /* frame_number */
                                     0,      /* packet_index */
                                     1024,   /* total_packets */
                                     8192,   /* payload_len */
                                     FRAME_FLAG_FIRST_PACKET,
                                     1234567890000ULL);  /* timestamp_ns */

    assert_int_equal(result, 0);

    /* Verify magic number */
    assert_true(le32_eq(buf, FRAME_HEADER_MAGIC));

    /* Verify frame number */
    assert_true(le32_eq(buf + 4, 1));

    /* Verify packet index */
    assert_true(le16_eq(buf + 8, 0));

    /* Verify total packets */
    assert_true(le16_eq(buf + 10, 1024));

    /* Verify payload length */
    assert_true(le16_eq(buf + 12, 8192));

    /* Verify flags */
    assert_true(le16_eq(buf + 14, FRAME_FLAG_FIRST_PACKET));

    /* Verify reserved is zero */
    assert_true(le32_eq(buf + 16, 0));
    assert_true(le32_eq(buf + 20, 0));

    /* Verify timestamp */
    assert_true(le64_eq(buf + 22, 1234567890000ULL));

    /* Verify CRC is non-zero (will validate separately) */
    uint16_t crc = buf[28] | (buf[29] << 8);
    assert_int_not_equal(crc, 0);
}

/**
 * @test FW_UT_02_002: CRC calculation
 * @pre Header encoded
 * @post CRC covers bytes 0-27 (excluding CRC field at 28-29)
 */
static void test_frame_header_crc_calculation(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    frame_header_encode(buf, sizeof(buf),
                       100,   /* frame_number */
                       5,     /* packet_index */
                       128,   /* total_packets */
                       4096,  /* payload_len */
                       FRAME_FLAG_LAST_PACKET,
                       9876543210000ULL);

    /* Extract CRC from header */
    uint16_t header_crc = buf[FRAME_HEADER_CRC_OFFSET] |
                         (buf[FRAME_HEADER_CRC_OFFSET + 1] << 8);

    /* Calculate CRC over header data (excluding CRC field) */
    uint16_t calculated_crc = crc16_ccitt(buf, FRAME_HEADER_CRC_OFFSET);

    assert_int_equal(header_crc, calculated_crc);
}

/**
 * @test FW_UT_02_003: Maximum values encoding
 * @pre Maximum parameter values
 * @post Header encoded correctly
 */
static void test_frame_header_encode_max_values(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    int result = frame_header_encode(buf, sizeof(buf),
                                     0xFFFFFFFFUL,      /* max frame_number */
                                     0xFFFF,            /* max packet_index */
                                     0xFFFF,            /* max total_packets */
                                     0xFFFF,            /* max payload_len */
                                     0xFFFF,            /* max flags */
                                     0xFFFFFFFFFFFFFFFFULL);  /* max timestamp */

    assert_int_equal(result, 0);

    assert_true(le32_eq(buf + 4, 0xFFFFFFFF));
    assert_true(le16_eq(buf + 8, 0xFFFF));
    assert_true(le16_eq(buf + 10, 0xFFFF));
    assert_true(le16_eq(buf + 12, 0xFFFF));
    assert_true(le16_eq(buf + 14, 0xFFFF));
    assert_true(le64_eq(buf + 22, 0xFFFFFFFFFFFFFFFFULL));
}

/* ==========================================================================
 * Decode Tests
 * ========================================================================== */

/**
 * @test FW_UT_02_004: Basic frame header decoding
 * @pre Valid encoded header
 * @post Decoded values match encoded values
 */
static void test_frame_header_decode_basic(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    /* Encode header */
    frame_header_encode(buf, sizeof(buf),
                       42,
                       10,
                       512,
                       2048,
                       FRAME_FLAG_FIRST_PACKET | FRAME_FLAG_LAST_PACKET,
                       1111111111111ULL);

    /* Decode header */
    uint32_t frame_number;
    uint16_t packet_index;
    uint16_t total_packets;
    uint16_t payload_len;
    uint16_t flags;
    uint64_t timestamp_ns;
    bool crc_valid;

    int result = frame_header_decode(buf, sizeof(buf),
                                    &frame_number,
                                    &packet_index,
                                    &total_packets,
                                    &payload_len,
                                    &flags,
                                    &timestamp_ns,
                                    &crc_valid);

    assert_int_equal(result, 0);
    assert_true(crc_valid);
    assert_int_equal(frame_number, 42);
    assert_int_equal(packet_index, 10);
    assert_int_equal(total_packets, 512);
    assert_int_equal(payload_len, 2048);
    assert_int_equal(flags, FRAME_FLAG_FIRST_PACKET | FRAME_FLAG_LAST_PACKET);
    assert_int_equal(timestamp_ns, 1111111111111ULL);
}

/**
 * @test FW_UT_02_005: Decode with invalid CRC
 * @pre Header with corrupted CRC
 * @post Decode succeeds, crc_valid = false
 */
static void test_frame_header_decode_invalid_crc(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    frame_header_encode(buf, sizeof(buf), 1, 0, 1, 100, 0, 0);

    /* Corrupt CRC */
    buf[28] ^= 0xFF;

    uint32_t frame_number;
    uint16_t packet_index;
    uint16_t total_packets;
    uint16_t payload_len;
    uint16_t flags;
    uint64_t timestamp_ns;
    bool crc_valid;

    int result = frame_header_decode(buf, sizeof(buf),
                                    &frame_number,
                                    &packet_index,
                                    &total_packets,
                                    &payload_len,
                                    &flags,
                                    &timestamp_ns,
                                    &crc_valid);

    assert_int_equal(result, 0);
    assert_false(crc_valid);
}

/* ==========================================================================
 * Boundary Tests
 * ========================================================================== */

/**
 * @test FW_UT_02_006: NULL buffer handling
 * @pre buf = NULL
 * @post Returns error
 */
static void test_frame_header_encode_null_buffer(void **state) {
    (void)state;

    int result = frame_header_encode(NULL, FRAME_HEADER_SIZE,
                                     1, 0, 1, 100, 0, 0);

    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_02_007: Buffer too small
 * @pre buf_size < FRAME_HEADER_SIZE
 * @post Returns error
 */
static void test_frame_header_encode_buffer_too_small(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE - 1];

    int result = frame_header_encode(buf, sizeof(buf),
                                     1, 0, 1, 100, 0, 0);

    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_02_008: Invalid magic number on decode
 * @pre Header with wrong magic
 * @post Returns error
 */
static void test_frame_header_decode_invalid_magic(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    /* Encode valid header */
    frame_header_encode(buf, sizeof(buf), 1, 0, 1, 100, 0, 0);

    /* Corrupt magic number */
    buf[0] = 0xFF;

    uint32_t frame_number;
    uint16_t packet_index;
    uint16_t total_packets;
    uint16_t payload_len;
    uint16_t flags;
    uint64_t timestamp_ns;
    bool crc_valid;

    int result = frame_header_decode(buf, sizeof(buf),
                                    &frame_number,
                                    &packet_index,
                                    &total_packets,
                                    &payload_len,
                                    &flags,
                                    &timestamp_ns,
                                    &crc_valid);

    assert_int_equal(result, -EINVAL);
}

/* ==========================================================================
 * Flag Tests
 * ========================================================================== */

/**
 * @test FW_UT_02_009: Drop indicator flag
 * @pre FRAME_FLAG_DROP_INDICATOR set
 * @post Flag decoded correctly
 */
static void test_frame_header_drop_indicator(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    frame_header_encode(buf, sizeof(buf),
                       1, 0, 1, 100, FRAME_FLAG_DROP_INDICATOR, 0);

    uint16_t flags;
    bool crc_valid;
    uint32_t frame_number;
    uint16_t packet_index;
    uint16_t total_packets;
    uint16_t payload_len;
    uint64_t timestamp_ns;

    frame_header_decode(buf, sizeof(buf),
                       &frame_number, &packet_index, &total_packets,
                       &payload_len, &flags, &timestamp_ns, &crc_valid);

    assert_int_equal(flags, FRAME_FLAG_DROP_INDICATOR);
}

/**
 * @test FW_UT_02_010: First and last packet flags
 * @pre Both flags set (single-packet frame)
 * @post Flags decoded correctly
 */
static void test_frame_header_first_last_packet(void **state) {
    (void)state;

    uint8_t buf[FRAME_HEADER_SIZE];

    frame_header_encode(buf, sizeof(buf),
                       1, 0, 1, 100,
                       FRAME_FLAG_FIRST_PACKET | FRAME_FLAG_LAST_PACKET,
                       0);

    uint16_t flags;
    bool crc_valid;
    uint32_t frame_number;
    uint16_t packet_index;
    uint16_t total_packets;
    uint16_t payload_len;
    uint64_t timestamp_ns;

    frame_header_decode(buf, sizeof(buf),
                       &frame_number, &packet_index, &total_packets,
                       &payload_len, &flags, &timestamp_ns, &crc_valid);

    assert_int_equal(flags, FRAME_FLAG_FIRST_PACKET | FRAME_FLAG_LAST_PACKET);
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Encode tests */
        cmocka_unit_test(test_frame_header_encode_basic),
        cmocka_unit_test(test_frame_header_crc_calculation),
        cmocka_unit_test(test_frame_header_encode_max_values),

        /* Decode tests */
        cmocka_unit_test(test_frame_header_decode_basic),
        cmocka_unit_test(test_frame_header_decode_invalid_crc),

        /* Boundary tests */
        cmocka_unit_test(test_frame_header_encode_null_buffer),
        cmocka_unit_test(test_frame_header_encode_buffer_too_small),
        cmocka_unit_test(test_frame_header_decode_invalid_magic),

        /* Flag tests */
        cmocka_unit_test(test_frame_header_drop_indicator),
        cmocka_unit_test(test_frame_header_first_last_packet),
    };

    return cmocka_run_group_tests_name("FW-UT-02: Frame Header Tests",
                                       tests, NULL, NULL);
}
