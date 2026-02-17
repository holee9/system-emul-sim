/**
 * @file test_bq40z50_driver.c
 * @brief Unit tests for BQ40z50 Battery Driver (FW-UT-10)
 *
 * Test ID: FW-UT-10
 * Coverage: BQ40z50 driver per REQ-FW-090, REQ-FW-091, REQ-FW-092
 *
 * Tests:
 * - I2C initialization and communication per REQ-FW-090
 * - 6 battery metrics reading per REQ-FW-091
 * - Low battery thresholds per REQ-FW-092
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#include <errno.h>
#include <fcntl.h>
#include <unistd.h>

/* Driver under test */
#include "hal/bq40z50_driver.h"

/* Mock functions */
extern int mock_open(const char *pathname, int flags);
extern void mock_set_i2c_fd(int fd);
extern int mock_ioctl(int fd, unsigned long request, void *arg);
extern void mock_set_i2c_read_data(uint16_t *data, size_t count);

/* ==========================================================================
 * Initialization Tests (REQ-FW-090)
 * ========================================================================== */

/**
 * @test FW_UT_10_001: Driver initialization
 * @pre I2C device available
 * @post Driver initialized, metrics read
 */
static void test_bq40z50_init(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int ret;

    /* Mock I2C operations */
    mock_set_i2c_fd(42);

    uint16_t mock_data[] = {
        2982,  /* Temperature: 25 C */
        3700,  /* Voltage: 3.7 V */
        0,     /* Current: 0 mA */
        100,   /* SOC: 100% */
        3000,  /* Remaining: 3000 mAh */
        3000,  /* Full: 3000 mAh */
    };
    mock_set_i2c_read_data(mock_data, 6);

    ret = bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_int_equal(ret, 0);
    assert_true(ctx.initialized);
    assert_int_equal(ctx.i2c_addr, BQ40Z50_I2C_ADDR);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_002: NULL parameter handling
 * @pre ctx = NULL or device = NULL
 * @post Returns error without crash
 */
static void test_bq40z50_null_init(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int ret;

    ret = bq40z50_init(NULL, "/dev/i2c-1", BQ40Z50_I2C_ADDR);
    assert_int_equal(ret, -EINVAL);

    ret = bq40z50_init(&ctx, NULL, BQ40Z50_I2C_ADDR);
    assert_int_equal(ret, -EINVAL);
}

/**
 * @test FW_UT_10_003: I2C open failure
 * @pre I2C device not accessible
 * @post Returns error code
 */
static void test_bq40z50_i2c_open_fail(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int ret;

    /* Mock open failure */
    mock_set_i2c_fd(-1);  /* open() returns -1 on failure */

    ret = bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_int_equal(ret, -ENOENT);
}

/* ==========================================================================
 * Metrics Reading Tests (REQ-FW-091)
 * ========================================================================== */

/**
 * @test FW_UT_10_004: Read all 6 metrics
 * @pre Driver initialized
 * @post All metrics valid per REQ-FW-091
 */
static void test_bq40z50_read_all_metrics(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;
    int ret;

    /* Initialize */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 100, 3000, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    /* Read metrics */
    ret = bq40z50_read_metrics(&ctx, &metrics);

    assert_int_equal(ret, 0);
    assert_int_equal(metrics.state_of_charge, 100);
    assert_int_equal(metrics.voltage, 3700);
    assert_int_equal(metrics.current, 0);
    assert_int_equal(metrics.temperature, 2982);
    assert_int_equal(metrics.remaining_capacity, 3000);
    assert_int_equal(metrics.full_charge_capacity, 3000);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_005: Metric valid ranges
 * @pre Metrics read from battery
 * @post All values within valid ranges
 */
static void test_bq40z50_metric_ranges(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;

    /* Initialize with typical values */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, -500, 80, 2400, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    bq40z50_read_metrics(&ctx, &metrics);

    /* SOC: 0-100% */
    assert_true(metrics.state_of_charge >= 0 && metrics.state_of_charge <= 100);

    /* Voltage: 2.8-4.2V */
    assert_true(metrics.voltage >= 2800 && metrics.voltage <= 4200);

    /* Temperature: 0-60 C (273-333 K) */
    assert_true(metrics.temperature >= 2731 && metrics.temperature <= 3331);

    /* Capacity: positive values */
    assert_true(metrics.remaining_capacity > 0);
    assert_true(metrics.full_charge_capacity > 0);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_006: Discharge current (negative)
 * @pre Battery discharging
 * @post Current value negative
 */
static void test_bq40z50_discharge_current(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;

    /* Initialize with discharge current */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0xFC18, 80, 2400, 3000};  /* -1000 mA */
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    bq40z50_read_metrics(&ctx, &metrics);

    /* Current should be negative (discharging) */
    assert_true(metrics.current < 0);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_007: Charge current (positive)
 * @pre Battery charging
 * @post Current value positive
 */
static void test_bq40z50_charge_current(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;

    /* Initialize with charge current */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0x03E8, 80, 2400, 3000};  /* +1000 mA */
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    bq40z50_read_metrics(&ctx, &metrics);

    /* Current should be positive (charging) */
    assert_true(metrics.current > 0);

    bq40z50_cleanup(&ctx);
}

/* ==========================================================================
 * Low Battery Threshold Tests (REQ-FW-092)
 * ========================================================================== */

/**
 * @test FW_UT_10_008: Normal battery level
 * @pre SOC > 10%
 * @post No warning, no emergency
 */
static void test_bq40z50_normal_battery(void **state) {
    (void)state;

    bq40z50_context_t ctx;

    /* Initialize with 50% SOC */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_false(bq40z50_is_low_battery(&ctx));
    assert_false(bq40z50_emergency_shutdown(&ctx));

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_009: Low battery warning (10%)
 * @pre SOC = 10%
 * @post Warning triggered, no emergency
 */
static void test_bq40z50_low_battery_warning(void **state) {
    (void)state;

    bq40z50_context_t ctx;

    /* Initialize with 10% SOC */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 10, 300, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_true(bq40z50_is_low_battery(&ctx));
    assert_false(bq40z50_emergency_shutdown(&ctx));

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_010: Emergency shutdown (5%)
 * @pre SOC = 5%
 * @post Emergency triggered
 */
static void test_bq40z50_emergency_shutdown(void **state) {
    (void)state;

    bq40z50_context_t ctx;

    /* Initialize with 5% SOC */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 5, 150, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_true(bq40z50_is_low_battery(&ctx));  /* 5% also triggers warning */
    assert_true(bq40z50_emergency_shutdown(&ctx));

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_011: Critical battery (1%)
 * @pre SOC = 1%
 * @post Emergency triggered
 */
static void test_bq40z50_critical_battery(void **state) {
    (void)state;

    bq40z50_context_t ctx;

    /* Initialize with 1% SOC */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3600, 0, 1, 30, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_true(bq40z50_is_low_battery(&ctx));
    assert_true(bq40z50_emergency_shutdown(&ctx));

    bq40z50_cleanup(&ctx);
}

/* ==========================================================================
 * Helper Function Tests
 * ========================================================================== */

/**
 * @test FW_UT_10_012: Get SOC helper
 * @pre Driver initialized
 * @post Returns correct SOC
 */
static void test_bq40z50_get_soc(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int soc;

    /* Initialize with 75% SOC */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 75, 2250, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    soc = bq40z50_get_soc(&ctx);
    assert_int_equal(soc, 75);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_013: Get voltage helper
 * @pre Driver initialized
 * @post Returns correct voltage
 */
static void test_bq40z50_get_voltage(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int voltage;

    /* Initialize with 3.85V */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3850, 0, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    voltage = bq40z50_get_voltage(&ctx);
    assert_int_equal(voltage, 3850);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_014: Get current helper
 * @pre Driver initialized
 * @post Returns correct current
 */
static void test_bq40z50_get_current(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int current;

    /* Initialize with -500 mA (discharge) */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0xFE0C, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    current = bq40z50_get_current(&ctx);
    assert_int_equal(current, -500);

    bq40z50_cleanup(&ctx);
}

/**
 * @test FW_UT_10_015: Get temperature helper
 * @pre Driver initialized
 * @post Returns correct temperature
 */
static void test_bq40z50_get_temperature(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    int temp;

    /* Initialize with 30 C */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {3031, 3700, 0, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    temp = bq40z50_get_temperature(&ctx);
    assert_int_equal(temp, 3031);  /* 303.1 K = 30 C */

    bq40z50_cleanup(&ctx);
}

/* ==========================================================================
 * Cleanup Tests
 * ========================================================================== */

/**
 * @test FW_UT_10_016: Normal cleanup
 * @pre Driver initialized
 * @post Resources freed
 */
static void test_bq40z50_cleanup(void **state) {
    (void)state;

    bq40z50_context_t ctx;

    /* Initialize */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    assert_true(ctx.initialized);

    /* Cleanup */
    bq40z50_cleanup(&ctx);

    assert_false(ctx.initialized);
}

/**
 * @test FW_UT_10_017: Cleanup without init
 * @pre Driver not initialized
 * @post Handles gracefully
 */
static void test_bq40z50_cleanup_no_init(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    memset(&ctx, 0, sizeof(ctx));

    /* Should not crash */
    bq40z50_cleanup(&ctx);
}

/* ==========================================================================
 * Error Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_10_018: Read metrics without init
 * @pre Driver not initialized
 * @post Returns error
 */
static void test_bq40z50_read_no_init(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;
    int ret;

    memset(&ctx, 0, sizeof(ctx));
    ret = bq40z50_read_metrics(&ctx, &metrics);

    assert_int_equal(ret, -EINVAL);
}

/**
 * @test FW_UT_10_019: I2C read failure
 * @pre I2C communication fails
 * @post Returns error code
 */
static void test_bq40z50_i2c_read_fail(void **state) {
    (void)state;

    bq40z50_context_t ctx;
    battery_metrics_t metrics;
    int ret;

    /* Initialize */
    mock_set_i2c_fd(42);
    uint16_t mock_data[] = {2982, 3700, 0, 50, 1500, 3000};
    mock_set_i2c_read_data(mock_data, 6);
    bq40z50_init(&ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);

    /* Mock read failure for next call */
    mock_set_i2c_fd(-1);

    ret = bq40z50_read_metrics(&ctx, &metrics);

    assert_int_equal(ret, -EBADF);

    bq40z50_cleanup(&ctx);
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Initialization tests */
        cmocka_unit_test(test_bq40z50_init),
        cmocka_unit_test(test_bq40z50_null_init),
        cmocka_unit_test(test_bq40z50_i2c_open_fail),

        /* Metrics reading tests */
        cmocka_unit_test(test_bq40z50_read_all_metrics),
        cmocka_unit_test(test_bq40z50_metric_ranges),
        cmocka_unit_test(test_bq40z50_discharge_current),
        cmocka_unit_test(test_bq40z50_charge_current),

        /* Low battery threshold tests */
        cmocka_unit_test(test_bq40z50_normal_battery),
        cmocka_unit_test(test_bq40z50_low_battery_warning),
        cmocka_unit_test(test_bq40z50_emergency_shutdown),
        cmocka_unit_test(test_bq40z50_critical_battery),

        /* Helper function tests */
        cmocka_unit_test(test_bq40z50_get_soc),
        cmocka_unit_test(test_bq40z50_get_voltage),
        cmocka_unit_test(test_bq40z50_get_current),
        cmocka_unit_test(test_bq40z50_get_temperature),

        /* Cleanup tests */
        cmocka_unit_test(test_bq40z50_cleanup),
        cmocka_unit_test(test_bq40z50_cleanup_no_init),

        /* Error handling tests */
        cmocka_unit_test(test_bq40z50_read_no_init),
        cmocka_unit_test(test_bq40z50_i2c_read_fail),
    };

    return cmocka_run_group_tests_name("FW-UT-10: BQ40z50 Driver Tests",
                                       tests, NULL, NULL);
}
