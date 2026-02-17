/**
 * @file test_crc16.c
 * @brief Unit tests for CRC-16/CCITT utility
 *
 * Test suite for CRC-16/CCITT implementation per REQ-FW-042.
 * Uses CMocka framework for unit testing.
 */

#include "util/crc16.h"
#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>

/* Test fixtures */
static void test_crc16_empty_buffer(void **state) {
    (void)state;
    uint16_t crc = crc16_compute(NULL, 0);
    /* Empty buffer should return initial value */
    assert_int_equal(crc, CRC16_INITIAL_VALUE);
}

static void test_crc16_single_byte_zero(void **state) {
    (void)state;
    uint8_t data[] = {0x00};
    uint16_t crc = crc16_compute(data, 1);
    /* Known test vector for 0x00 with CRC-16/CCITT */
    assert_int_equal(crc, 0x3D0A);
}

static void test_crc16_single_byte_ff(void **state) {
    (void)state;
    uint8_t data[] = {0xFF};
    uint16_t crc = crc16_compute(data, 1);
    /* Known test vector for 0xFF with CRC-16/CCITT */
    assert_int_equal(crc, 0xE8C4);
}

static void test_crc16_test_vector_1(void **state) {
    (void)state;
    /* Known test vector: "123456789" */
    uint8_t data[] = {0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39};
    uint16_t crc = crc16_compute(data, sizeof(data));
    /* CRC-16/CCITT for "123456789" is 0x29B1 */
    assert_int_equal(crc, 0x29B1);
}

static void test_crc16_test_vector_2(void **state) {
    (void)state;
    /* Known test vector: All zeros 8 bytes */
    uint8_t data[] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
    uint16_t crc = crc16_compute(data, sizeof(data));
    assert_int_equal(crc, 0x0F73);
}

static void test_crc16_incremental(void **state) {
    (void)state;
    /* Test incremental computation */
    uint8_t data1[] = {0x12, 0x34};
    uint8_t data2[] = {0x56, 0x78};

    uint16_t crc1 = crc16_compute(data1, 2);
    uint16_t crc2 = crc16_compute_with_init(data2, 2, crc1);
    uint16_t crc_combined = crc16_compute(data1, 4);

    assert_int_equal(crc2, crc_combined);
}

static void test_crc16_frame_header_pattern(void **state) {
    (void)state;
    /* Simulate frame header with magic number and sequence */
    uint8_t frame_header[] = {
        0xD7, 0xE0, 0x12, 0x34,  /* magic: 0xD7E01234 */
        0x00, 0x01,              /* frame_number: 1 */
        0x00, 0x00, 0x08, 0x00,  /* width: 2048 */
        0x00, 0x00, 0x08, 0x00,  /* height: 2048 */
        0x00, 0x10,              /* bit_depth: 16 */
        0x00, 0x00, 0x00, 0x01,  /* packet_index: 1 */
        0x00, 0x02,              /* total_packets: 2 */
        0x00, 0x00, 0x00, 0x10,  /* payload_len: 16 */
        0x00, 0x00, 0x00, 0x00   /* timestamp: 0 */
    };

    uint16_t crc = crc16_compute(frame_header, sizeof(frame_header));
    /* CRC should be non-zero for non-zero data */
    assert_int_not_equal(crc, 0x0000);
    assert_int_not_equal(crc, CRC16_INITIAL_VALUE);
}

static void test_crc16_verify_valid(void **state) {
    (void)state;
    uint8_t data[] = {0x31, 0x32, 0x33, 0x34};
    uint16_t crc = crc16_compute(data, 4);

    int result = crc16_verify(data, 4, crc);
    assert_int_equal(result, 1);
}

static void test_crc16_verify_invalid(void **state) {
    (void)state;
    uint8_t data[] = {0x31, 0x32, 0x33, 0x34};
    uint16_t wrong_crc = 0xBAD;

    int result = crc16_verify(data, 4, wrong_crc);
    assert_int_equal(result, 0);
}

static void test_crc16_large_buffer(void **state) {
    (void)state;
    /* Test with larger buffer (simulating max frame header) */
    uint8_t data[256];
    for (int i = 0; i < 256; i++) {
        data[i] = (uint8_t)i;
    }

    uint16_t crc = crc16_compute(data, 256);
    /* Known CRC-16/CCITT for pattern 0x00-0xFF is 0x7EF1 */
    assert_int_equal(crc, 0x7EF1);
}

static void test_crc16_all_ones(void **state) {
    (void)state;
    /* Test with all 0xFF bytes */
    uint8_t data[8];
    for (int i = 0; i < 8; i++) {
        data[i] = 0xFF;
    }

    uint16_t crc = crc16_compute(data, 8);
    assert_int_equal(crc, 0x1685);
}

/* Test runner */
int main(void) {
    const struct CMUnitTest tests[] = {
        cmocka_unit_test(test_crc16_empty_buffer),
        cmocka_unit_test(test_crc16_single_byte_zero),
        cmocka_unit_test(test_crc16_single_byte_ff),
        cmocka_unit_test(test_crc16_test_vector_1),
        cmocka_unit_test(test_crc16_test_vector_2),
        cmocka_unit_test(test_crc16_incremental),
        cmocka_unit_test(test_crc16_frame_header_pattern),
        cmocka_unit_test(test_crc16_verify_valid),
        cmocka_unit_test(test_crc16_verify_invalid),
        cmocka_unit_test(test_crc16_large_buffer),
        cmocka_unit_test(test_crc16_all_ones),
    };

    return cmocka_run_group_tests(tests, NULL, NULL);
}
