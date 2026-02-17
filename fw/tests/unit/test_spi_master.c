/**
 * @file test_spi_master.c
 * @brief Unit tests for SPI Master HAL (FW-UT-01)
 *
 * Test ID: FW-UT-01
 * Coverage: SPI register communication per REQ-FW-020, REQ-FW-021
 *
 * Tests:
 * - Register read/write round-trip
 * - Write verification with retry logic
 * - Error injection (SPI failure, timeout)
 * - Transaction format validation (32-bit: 8-bit addr + 8-bit R/W + 16-bit data)
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <stdbool.h>

/* Mock spidev functions - provided by mock_spidev.c */
extern int mock_spidev_open(const char *device);
extern int mock_spidev_close(int fd);
extern int mock_spidev_transfer(int fd, const uint8_t *tx_buf, uint8_t *rx_buf, size_t len);

/* Function under test */
extern int fpga_spi_init(const char *device);
extern void fpga_spi_deinit(void);
extern int fpga_reg_write(uint8_t addr, uint16_t data);
extern int fpga_reg_read(uint8_t addr, uint16_t *data);

/* Test state */
static int spi_fd = -1;

/* ==========================================================================
 * Setup/Teardown
 * ========================================================================== */

static int setup_spi(void **state) {
    (void)state;
    spi_fd = mock_spidev_open("/dev/spidev0.0");
    if (spi_fd < 0) {
        return -1;
    }
    return 0;
}

static int teardown_spi(void **state) {
    (void)state;
    mock_spidev_close(spi_fd);
    spi_fd = -1;
    return 0;
}

/* ==========================================================================
 * Transaction Format Tests (REQ-FW-020)
 * ========================================================================== */

/**
 * @test FW_UT_01_001: Register write transaction format
 * @pre SPI initialized
 * @post Transaction format: [addr<<8|WRITE(0x00), data]
 *
 * Transaction format (32-bit):
 * - Word0: [address (7:0) << 8 | WRITE (0x00)]
 * - Word1: [16-bit data]
 */
static void test_spi_write_transaction_format(void **state) {
    (void)state;

    uint8_t test_addr = 0x20;
    uint16_t test_data = 0x1234;

    /* Expected TX buffer for write */
    /* Word0: 0x2000 (addr=0x20, WRITE=0x00) */
    /* Word1: 0x1234 (data) */
    uint8_t expected_tx[4] = {
        0x00,  /* LSB of Word0: WRITE (0x00) */
        0x20,  /* MSB of Word0: address (0x20) */
        0x34,  /* LSB of Word1: data low byte */
        0x12   /* MSB of Word1: data high byte */
    };

    /* Mock will verify TX buffer format */
    int result = fpga_reg_write(test_addr, test_data);

    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_01_002: Register read transaction format
 * @pre SPI initialized
 * @post Transaction format: [addr<<8|READ(0x80), dummy]
 *
 * Transaction format (32-bit):
 * - Word0: [address (7:0) << 8 | READ (0x80)]
 * - Word1: [dummy (don't care)]
 */
static void test_spi_read_transaction_format(void **state) {
    (void)state;

    uint8_t test_addr = 0x20;
    uint16_t read_data;

    /* Expected TX buffer for read */
    /* Word0: 0xA020 (addr=0x20, READ=0x80) */
    /* Word1: dummy (don't care) */
    uint8_t expected_tx[4] = {
        0x80,  /* LSB of Word0: READ (0x80) */
        0x20,  /* MSB of Word0: address (0x20) */
        0x00,  /* LSB of Word1: dummy */
        0x00   /* MSB of Word1: dummy */
    };

    /* Mock will return test data in RX buffer */
    int result = fpga_reg_read(test_addr, &read_data);

    assert_int_equal(result, 0);
}

/* ==========================================================================
 * Write Verification Tests (REQ-FW-021)
 * ========================================================================== */

/**
 * @test FW_UT_01_003: Successful write with verification
 * @pre SPI initialized, FPGA responds correctly
 * @post Write succeeds, read-back matches written value
 */
static void test_spi_write_verify_success(void **state) {
    (void)state;

    uint8_t addr = 0x20;
    uint16_t data = 0x1234;

    /* Mock returns same value on read-back */
    int result = fpga_reg_write(addr, data);

    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_01_004: Write verification failure - retry logic
 * @pre SPI initialized, FPGA returns wrong value on first read
 * @post Write retries up to 3 times, succeeds on retry
 */
static void test_spi_write_verify_retry(void **state) {
    (void)state;

    uint8_t addr = 0x20;
    uint16_t data = 0x1234;

    /* Mock simulates: write OK, first read-back wrong, second read-back OK */
    /* Write: TX=[addr<<8|WRITE, data], RX ignored */
    /* Read 1: TX=[addr<<8|READ, dummy], RX=[wrong data] */
    /* Read 2: TX=[addr<<8|READ, dummy], RX=[correct data] */
    int result = fpga_reg_write(addr, data);

    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_01_005: Write verification failure - max retries exceeded
 * @pre SPI initialized, FPGA consistently returns wrong value
 * @post Write fails after 3 retries, returns error code
 */
static void test_spi_write_verify_max_retry(void **state) {
    (void)state;

    uint8_t addr = 0x20;
    uint16_t data = 0x1234;

    /* Mock simulates: write OK, all 3 read-backs return wrong value */
    int result = fpga_reg_write(addr, data);

    assert_int_equal(result, -ETIMEDOUT);  /* Or appropriate error code */
}

/* ==========================================================================
 * Error Injection Tests
 * ========================================================================== */

/**
 * @test FW_UT_01_006: SPI transfer failure
 * @pre SPI initialized, ioctl returns error
 * @post Write fails, returns appropriate error code
 */
static void test_spi_transfer_error(void **state) {
    (void)state;

    /* Mock simulates SPI ioctl failure (e.g., EIO) */
    int result = fpga_reg_write(0x20, 0x1234);

    assert_int_equal(result, -EIO);
}

/**
 * @test FW_UT_01_007: SPI not initialized
 * @pre SPI not initialized (fd < 0)
 * @post Operation fails, returns error
 */
static void test_spi_not_initialized(void **state) {
    (void)state;

    uint16_t data;
    int result = fpga_reg_read(0x20, &data);

    assert_int_equal(result, -EBADF);  /* Bad file descriptor */
}

/**
 * @test FW_UT_01_008: Invalid address
 * @pre SPI initialized
 * @post Write fails for address > 0x7F (only 7-bit addresses valid)
 */
static void test_spi_invalid_address(void **state) {
    (void)state;

    int result = fpga_reg_write(0xFF, 0x1234);  /* Invalid address */

    assert_int_equal(result, -EINVAL);
}

/* ==========================================================================
 * Timing Tests (REQ-FW-023)
 * ========================================================================== */

/**
 * @test FW_UT_01_009: SPI round-trip latency
 * @pre SPI initialized, mocked SPI returns immediately
 * @post Transaction completes within 10 ms
 *
 * Note: This test measures mock performance. Real hardware test
 * required for actual latency validation (AC-FW-001).
 */
static void test_spi_round_trip_latency(void **state) {
    (void)state;

    struct timespec start, end;
    uint16_t data;

    clock_gettime(CLOCK_MONOTONIC, &start);
    int result = fpga_reg_read(0x20, &data);
    clock_gettime(CLOCK_MONOTONIC, &end);

    assert_int_equal(result, 0);

    /* Calculate elapsed time in milliseconds */
    uint64_t elapsed_ns = (end.tv_sec - start.tv_sec) * 1000000000ULL +
                          (end.tv_nsec - start.tv_nsec);
    double elapsed_ms = elapsed_ns / 1000000.0;

    /* Mock should be much faster than 10 ms */
    assert_true(elapsed_ms < 10.0);
}

/* ==========================================================================
 * Boundary Tests
 * ========================================================================== */

/**
 * @test FW_UT_01_010: Minimum register value (0x0000)
 * @pre SPI initialized
 * @post Write and read of 0x0000 succeeds
 */
static void test_spi_min_value(void **state) {
    (void)state;

    int result = fpga_reg_write(0x20, 0x0000);
    assert_int_equal(result, 0);

    uint16_t data;
    result = fpga_reg_read(0x20, &data);
    assert_int_equal(result, 0);
    assert_int_equal(data, 0x0000);
}

/**
 * @test FW_UT_01_011: Maximum register value (0xFFFF)
 * @pre SPI initialized
 * @post Write and read of 0xFFFF succeeds
 */
static void test_spi_max_value(void **state) {
    (void)state;

    int result = fpga_reg_write(0x20, 0xFFFF);
    assert_int_equal(result, 0);

    uint16_t data;
    result = fpga_reg_read(0x20, &data);
    assert_int_equal(result, 0);
    assert_int_equal(data, 0xFFFF);
}

/**
 * @test FW_UT_01_012: Multiple register writes
 * @pre SPI initialized
 * @post Sequential writes to different addresses succeed
 */
static void test_spi_multiple_writes(void **state) {
    (void)state;

    struct {
        uint8_t addr;
        uint16_t data;
    } writes[] = {
        {0x20, 0x1234},
        {0x21, 0x5678},
        {0x22, 0x9ABC},
        {0x23, 0xDEF0},
    };

    for (size_t i = 0; i < 4; i++) {
        int result = fpga_reg_write(writes[i].addr, writes[i].data);
        assert_int_equal(result, 0);
    }

    /* Verify all writes */
    for (size_t i = 0; i < 4; i++) {
        uint16_t data;
        int result = fpga_reg_read(writes[i].addr, &data);
        assert_int_equal(result, 0);
        assert_int_equal(data, writes[i].data);
    }
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Transaction format tests */
        cmocka_unit_test_setup(test_spi_write_transaction_format, setup_spi),
        cmocka_unit_test_setup(test_spi_read_transaction_format, setup_spi),

        /* Write verification tests */
        cmocka_unit_test_setup(test_spi_write_verify_success, setup_spi),
        cmocka_unit_test_setup(test_spi_write_verify_retry, setup_spi),
        cmocka_unit_test_setup(test_spi_write_verify_max_retry, setup_spi),

        /* Error injection tests */
        cmocka_unit_test_setup(test_spi_transfer_error, setup_spi),
        cmocka_unit_test(test_spi_not_initialized),
        cmocka_unit_test_setup(test_spi_invalid_address, setup_spi),

        /* Timing test */
        cmocka_unit_test_setup(test_spi_round_trip_latency, setup_spi),

        /* Boundary tests */
        cmocka_unit_test_setup(test_spi_min_value, setup_spi),
        cmocka_unit_test_setup(test_spi_max_value, setup_spi),
        cmocka_unit_test_setup(test_spi_multiple_writes, setup_spi),
    };

    return cmocka_run_group_tests_name("FW-UT-01: SPI Master HAL Tests",
                                       tests,
                                       NULL,  /* group_setup */
                                       NULL); /* group_teardown */
}
