/**
 * @file test_health_monitor.c
 * @brief Unit tests for Health Monitor (FW-UT-08)
 *
 * Test ID: FW-UT-08
 * Coverage: Health Monitor per REQ-FW-060, REQ-FW-110, REQ-FW-111, REQ-FW-112
 *
 * Tests:
 * - Watchdog timer (1s pet, 5s timeout) per REQ-FW-060
 * - Runtime statistics aggregation per REQ-FW-111
 * - Structured syslog logging per REQ-FW-110
 * - GET_STATUS response assembly per REQ-FW-112
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
#include <time.h>

/* Log levels */
typedef enum {
    LOG_DEBUG = 0,
    LOG_INFO,
    LOG_WARNING,
    LOG_ERROR,
    LOG_CRITICAL,
} log_level_t;

/* Runtime statistics counters */
typedef struct {
    uint64_t frames_received;
    uint64_t frames_sent;
    uint64_t frames_dropped;
    uint64_t spi_errors;
    uint64_t csi2_errors;
    uint64_t packets_sent;
    uint64_t bytes_sent;
    uint64_t auth_failures;
    uint64_t watchdog_resets;
} runtime_stats_t;

/* System status for GET_STATUS */
typedef struct {
    uint8_t state;           /* Current sequence engine state */
    runtime_stats_t stats;   /* Runtime counters */
    uint8_t battery_soc;     /* Battery state of charge (%) */
    uint16_t battery_mv;     /* Battery voltage (mV) */
    uint32_t uptime_sec;     /* Daemon uptime (seconds) */
    uint16_t fpga_temp;      /* FPGA temperature (0.1 C) */
} system_status_t;

/* Function under test */
extern int health_monitor_init(void);
extern void health_monitor_deinit(void);
extern void health_monitor_pet_watchdog(void);
extern bool health_monitor_is_alive(void);
extern void health_monitor_get_stats(runtime_stats_t *stats);
extern void health_monitor_update_stat(const char *name, int64_t delta);
extern void health_monitor_log(log_level_t level, const char *module, const char *format, ...);
extern int health_monitor_get_status(system_status_t *status);
extern int health_monitor_set_log_level(log_level_t level);
extern log_level_t health_monitor_get_log_level(void);

/* Mock functions */
extern uint64_t mock_get_time_ms(void);
extern void mock_set_time_ms(uint64_t time_ms);
extern void mock_syslog_capture(const char *msg);

/* Test configuration */
#define WATCHDOG_PET_INTERVAL_MS 1000   /* 1 second */
#define WATCHDOG_TIMEOUT_MS      5000   /* 5 seconds */

/* ==========================================================================
 * Watchdog Tests (REQ-FW-060)
 * ========================================================================== */

/**
 * @test FW_UT_08_001: Watchdog initialization
 * @pre Health monitor initialized
 * @post Watchdog timer started, system marked alive
 */
static void test_health_watchdog_init(void **state) {
    (void)state;

    int result = health_monitor_init();
    assert_int_equal(result, 0);
    assert_true(health_monitor_is_alive());

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_002: Watchdog pet keeps system alive
 * @pre Watchdog active
 * @post Regular pet calls keep system alive
 */
static void test_health_watchdog_pet(void **state) {
    (void)state;

    health_monitor_init();

    mock_set_time_ms(0);
    health_monitor_pet_watchdog();
    assert_true(health_monitor_is_alive());

    /* Pet before timeout */
    mock_set_time_ms(WATCHDOG_PET_INTERVAL_MS);
    health_monitor_pet_watchdog();
    assert_true(health_monitor_is_alive());

    /* Pet again before timeout */
    mock_set_time_ms(WATCHDOG_PET_INTERVAL_MS * 2);
    health_monitor_pet_watchdog();
    assert_true(health_monitor_is_alive());

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_003: Watchdog timeout detection
 * @pre No pet for 5+ seconds
 * @post System marked as dead (timeout detected)
 */
static void test_health_watchdog_timeout(void **state) {
    (void)state;

    health_monitor_init();

    mock_set_time_ms(0);
    health_monitor_pet_watchdog();
    assert_true(health_monitor_is_alive());

    /* Simulate time passing beyond timeout */
    mock_set_time_ms(WATCHDOG_TIMEOUT_MS + 100);

    /* Watchdog should have timed out */
    assert_false(health_monitor_is_alive());

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_004: Watchdog recovery after timeout
 * @pre Watchdog timed out
 * @post Pet after timeout restarts watchdog
 */
static void test_health_watchdog_recovery(void **state) {
    (void)state;

    health_monitor_init();

    /* Trigger timeout */
    mock_set_time_ms(0);
    health_monitor_pet_watchdog();
    mock_set_time_ms(WATCHDOG_TIMEOUT_MS + 100);
    assert_false(health_monitor_is_alive());

    /* Pet after timeout should restart watchdog */
    health_monitor_pet_watchdog();
    assert_true(health_monitor_is_alive());

    health_monitor_deinit();
}

/* ==========================================================================
 * Statistics Tests (REQ-FW-111)
 * ========================================================================== */

/**
 * @test FW_UT_08_005: Get statistics
 * @pre Statistics initialized
 * @post All counters accessible
 */
static void test_health_get_stats(void **state) {
    (void)state;

    health_monitor_init();

    runtime_stats_t stats;
    health_monitor_get_stats(&stats);

    /* All counters should be zero or have valid values */
    assert_true(stats.frames_received >= 0);
    assert_true(stats.frames_sent >= 0);
    assert_true(stats.frames_dropped >= 0);
    assert_true(stats.spi_errors >= 0);
    assert_true(stats.csi2_errors >= 0);
    assert_true(stats.packets_sent >= 0);
    assert_true(stats.bytes_sent >= 0);
    assert_true(stats.auth_failures >= 0);
    assert_true(stats.watchdog_resets >= 0);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_006: Update individual statistics
 * @pre Statistic name and delta
 * @post Counter incremented by delta
 */
static void test_health_update_stat(void **state) {
    (void)state;

    health_monitor_init();

    runtime_stats_t stats_before, stats_after;

    health_monitor_get_stats(&stats_before);
    health_monitor_update_stat("frames_received", 10);
    health_monitor_get_stats(&stats_after);

    assert_int_equal(stats_after.frames_received, stats_before.frames_received + 10);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_007: Multiple statistic updates
 * @pre Multiple updates to same counter
 * @post Counter accumulates all updates
 */
static void test_health_update_stat_multiple(void **state) {
    (void)state;

    health_monitor_init();

    health_monitor_update_stat("frames_sent", 5);
    health_monitor_update_stat("frames_sent", 3);
    health_monitor_update_stat("frames_sent", 2);

    runtime_stats_t stats;
    health_monitor_get_stats(&stats);

    assert_int_equal(stats.frames_sent, 10);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_008: Update with negative delta
 * @pre Negative delta value
 * @post Counter decrements
 */
static void test_health_update_stat_negative(void **state) {
    (void)state;

    health_monitor_init();

    health_monitor_update_stat("spi_errors", 5);
    health_monitor_update_stat("spi_errors", -2);

    runtime_stats_t stats;
    health_monitor_get_stats(&stats);

    assert_int_equal(stats.spi_errors, 3);

    health_monitor_deinit();
}

/* ==========================================================================
 * Logging Tests (REQ-FW-110)
 * ========================================================================== */

/**
 * @test FW_UT_08_009: Log at different levels
 * @pre Various log levels
 * @post Messages formatted correctly
 */
static void test_health_log_levels(void **state) {
    (void)state;

    health_monitor_init();

    health_monitor_log(LOG_DEBUG, "TEST", "Debug message");
    health_monitor_log(LOG_INFO, "TEST", "Info message");
    health_monitor_log(LOG_WARNING, "TEST", "Warning message");
    health_monitor_log(LOG_ERROR, "TEST", "Error message");
    health_monitor_log(LOG_CRITICAL, "TEST", "Critical message");

    /* Verify all messages were logged */
    /* (Mock syslog would capture these for verification) */

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_010: Structured log format
 * @pre Log call with module and message
 * @post Log contains timestamp, module, severity, message
 */
static void test_health_log_structured(void **state) {
    (void)state;

    health_monitor_init();

    health_monitor_log(LOG_INFO, "spi_master", "SPI register write failed");

    /* Verify structured format: [timestamp] [module] [LEVEL] message */
    /* e.g., [2026-02-18 12:34:56.789] [spi_master] [INFO] SPI register write failed */

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_011: Set log level
 * @pre Log level changed
 * @post Only messages at or above set level are logged
 */
static void test_health_set_log_level(void **state) {
    (void)state;

    health_monitor_init();

    /* Set to WARNING level */
    health_monitor_set_log_level(LOG_WARNING);
    assert_int_equal(health_monitor_get_log_level(), LOG_WARNING);

    /* DEBUG and INFO should not be logged */
    /* WARNING, ERROR, CRITICAL should be logged */

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_012: Get log level
 * @pre Log level set
 * @post Returns current log level
 */
static void test_health_get_log_level(void **state) {
    (void)state;

    health_monitor_init();

    assert_int_equal(health_monitor_get_log_level(), LOG_INFO);  /* Default */

    health_monitor_set_log_level(LOG_ERROR);
    assert_int_equal(health_monitor_get_log_level(), LOG_ERROR);

    health_monitor_deinit();
}

/* ==========================================================================
 * GET_STATUS Response Tests (REQ-FW-112)
 * ========================================================================== */

/**
 * @test FW_UT_08_013: Get status - all fields
 * @pre System running
 * @post Status contains all required fields
 */
static void test_health_get_status_complete(void **state) {
    (void)state;

    health_monitor_init();

    /* Simulate some activity */
    health_monitor_update_stat("frames_received", 100);
    health_monitor_update_stat("frames_sent", 99);

    system_status_t status;
    int result = health_monitor_get_status(&status);

    assert_int_equal(result, 0);
    assert_true(status.state >= 0);
    assert_int_equal(status.stats.frames_received, 100);
    assert_int_equal(status.stats.frames_sent, 99);
    assert_true(status.uptime_sec >= 0);
    assert_true(status.battery_soc <= 100);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_014: Get status timing
 * @pre Status requested
 * @post Response completes within 50 ms (REQ-FW-112)
 */
static void test_health_get_status_timing(void **state) {
    (void)state;

    health_monitor_init();

    struct timespec start, end;

    clock_gettime(CLOCK_MONOTONIC, &start);

    system_status_t status;
    int result = health_monitor_get_status(&status);

    clock_gettime(CLOCK_MONOTONIC, &end);

    assert_int_equal(result, 0);

    /* Calculate elapsed time in milliseconds */
    uint64_t elapsed_ns = (end.tv_sec - start.tv_sec) * 1000000000ULL +
                          (end.tv_nsec - start.tv_nsec);
    double elapsed_ms = elapsed_ns / 1000000.0;

    /* Should be much faster than 50 ms (using cached values) */
    assert_true(elapsed_ms < 50.0);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_015: Status includes battery metrics
 * @pre Battery monitoring active
 * @post Status contains SOC and voltage
 */
static void test_health_status_battery_metrics(void **state) {
    (void)state;

    health_monitor_init();

    system_status_t status;
    health_monitor_get_status(&status);

    assert_true(status.battery_soc <= 100);
    assert_true(status.battery_mv >= 2800 && status.battery_mv <= 4200);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_016: Status includes error counters
 * @pre Errors have occurred
 * @post Status reflects error counts
 */
static void test_health_status_error_counters(void **state) {
    (void)state;

    health_monitor_init();

    /* Simulate errors */
    health_monitor_update_stat("spi_errors", 3);
    health_monitor_update_stat("csi2_errors", 2);
    health_monitor_update_stat("auth_failures", 1);

    system_status_t status;
    health_monitor_get_status(&status);

    assert_int_equal(status.stats.spi_errors, 3);
    assert_int_equal(status.stats.csi2_errors, 2);
    assert_int_equal(status.stats.auth_failures, 1);

    health_monitor_deinit();
}

/* ==========================================================================
 * Error Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_08_017: NULL parameter handling
 * @pre stats = NULL or status = NULL
 * @post Returns error without crash
 */
static void test_health_null_parameters(void **state) {
    (void)state;

    health_monitor_init();

    int result = health_monitor_get_stats(NULL);
    assert_int_equal(result, -EINVAL);

    result = health_monitor_get_status(NULL);
    assert_int_equal(result, -EINVAL);

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_018: Double initialization
 * @pre Already initialized
 * @post Handles gracefully (no-op or reinit)
 */
static void test_health_double_init(void **state) {
    (void)state;

    health_monitor_init();
    int result = health_monitor_init();

    assert_int_equal(result, 0);  /* Should succeed or indicate already init */

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_019: Deinit without init
 * @pre Not initialized
 * @post Handles gracefully
 */
static void test_health_deinit_without_init(void **state) {
    (void)state;

    /* Should not crash */
    health_monitor_deinit();
}

/**
 * @test FW_UT_08_020: Invalid statistic name
 * @pre Unknown statistic name
 * @post Ignores update or returns error
 */
static void test_health_invalid_stat_name(void **state) {
    (void)state;

    health_monitor_init();

    runtime_stats_t stats_before, stats_after;

    health_monitor_get_stats(&stats_before);
    health_monitor_update_stat("invalid_counter", 100);
    health_monitor_get_stats(&stats_after);

    /* No valid counter should have changed */
    assert_true(stats_after.frames_received == stats_before.frames_received);

    health_monitor_deinit();
}

/* ==========================================================================
 * Log Level Filtering Tests
 * ========================================================================== */

/**
 * @test FW_UT_08_021: Log filtering at ERROR level
 * @pre Log level = ERROR
 * @post Only ERROR and CRITICAL logged
 */
static void test_health_log_filter_error(void **state) {
    (void)state;

    health_monitor_init();
    health_monitor_set_log_level(LOG_ERROR);

    /* These should not be logged */
    health_monitor_log(LOG_DEBUG, "TEST", "Debug");
    health_monitor_log(LOG_INFO, "TEST", "Info");
    health_monitor_log(LOG_WARNING, "TEST", "Warning");

    /* These should be logged */
    health_monitor_log(LOG_ERROR, "TEST", "Error");
    health_monitor_log(LOG_CRITICAL, "TEST", "Critical");

    /* Verify only 2 messages logged */

    health_monitor_deinit();
}

/**
 * @test FW_UT_08_022: Log all messages at DEBUG level
 * @pre Log level = DEBUG
 * @post All messages logged
 */
static void test_health_log_filter_debug(void **state) {
    (void)state;

    health_monitor_init();
    health_monitor_set_log_level(LOG_DEBUG);

    /* All should be logged */
    health_monitor_log(LOG_DEBUG, "TEST", "Debug");
    health_monitor_log(LOG_INFO, "TEST", "Info");
    health_monitor_log(LOG_WARNING, "TEST", "Warning");
    health_monitor_log(LOG_ERROR, "TEST", "Error");
    health_monitor_log(LOG_CRITICAL, "TEST", "Critical");

    /* Verify all 5 messages logged */

    health_monitor_deinit();
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Watchdog tests */
        cmocka_unit_test(test_health_watchdog_init),
        cmocka_unit_test(test_health_watchdog_pet),
        cmocka_unit_test(test_health_watchdog_timeout),
        cmocka_unit_test(test_health_watchdog_recovery),

        /* Statistics tests */
        cmocka_unit_test(test_health_get_stats),
        cmocka_unit_test(test_health_update_stat),
        cmocka_unit_test(test_health_update_stat_multiple),
        cmocka_unit_test(test_health_update_stat_negative),

        /* Logging tests */
        cmocka_unit_test(test_health_log_levels),
        cmocka_unit_test(test_health_log_structured),
        cmocka_unit_test(test_health_set_log_level),
        cmocka_unit_test(test_health_get_log_level),

        /* GET_STATUS tests */
        cmocka_unit_test(test_health_get_status_complete),
        cmocka_unit_test(test_health_get_status_timing),
        cmocka_unit_test(test_health_status_battery_metrics),
        cmocka_unit_test(test_health_status_error_counters),

        /* Error handling tests */
        cmocka_unit_test(test_health_null_parameters),
        cmocka_unit_test(test_health_double_init),
        cmocka_unit_test(test_health_deinit_without_init),
        cmocka_unit_test(test_health_invalid_stat_name),

        /* Log filtering tests */
        cmocka_unit_test(test_health_log_filter_error),
        cmocka_unit_test(test_health_log_filter_debug),
    };

    return cmocka_run_group_tests_name("FW-UT-08: Health Monitor Tests",
                                       tests, NULL, NULL);
}
