/**
 * @file test_config_loader.c
 * @brief Unit tests for Configuration Loader (FW-UT-04)
 *
 * Test ID: FW-UT-04
 * Coverage: Config loader validation per REQ-FW-003, REQ-FW-130, REQ-FW-131
 *
 * Tests:
 * - Valid configuration loading
 * - Invalid configuration detection (out of range values)
 * - Edge cases (boundary values)
 * - Hot/cold parameter classification
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <stdbool.h>

/* Configuration structure */
typedef struct {
    /* Panel configuration */
    uint16_t rows;
    uint16_t cols;
    uint8_t bit_depth;

    /* Timing configuration */
    uint16_t frame_rate;
    uint32_t line_time_us;
    uint32_t frame_time_us;

    /* SPI configuration */
    uint32_t spi_speed_hz;
    uint8_t spi_mode;

    /* CSI-2 configuration */
    uint32_t csi2_lane_speed_mbps;
    uint8_t csi2_lanes;

    /* Network configuration */
    char host_ip[16];
    uint16_t data_port;
    uint16_t control_port;
    uint32_t send_buffer_size;

    /* Scan mode */
    uint8_t scan_mode;  /* 0=Single, 1=Continuous, 2=Calibration */

    /* Logging */
    uint8_t log_level;
} detector_config_t;

/* Function under test */
extern int config_load(const char *filename, detector_config_t *config);
extern int config_validate(const detector_config_t *config);
extern bool config_is_hot_swappable(const char *param_name);
extern int config_set(detector_config_t *config, const char *key, const void *value);

/* Mock file content */
extern void mock_yaml_set_content(const char *content);

/* Valid YAML configuration */
static const char *valid_yaml_config =
    "# Detector Configuration\n"
    "panel:\n"
    "  rows: 2048\n"
    "  cols: 2048\n"
    "  bit_depth: 16\n"
    "\n"
    "timing:\n"
    "  frame_rate: 15\n"
    "  line_time_us: 50\n"
    "\n"
    "spi:\n"
    "  speed_hz: 50000000\n"
    "  mode: 0\n"
    "\n"
    "csi2:\n"
    "  lane_speed_mbps: 400\n"
    "  lanes: 4\n"
    "\n"
    "network:\n"
    "  host_ip: \"192.168.1.100\"\n"
    "  data_port: 8000\n"
    "  control_port: 8001\n"
    "  send_buffer_size: 16777216\n"
    "\n"
    "scan:\n"
    "  mode: continuous\n"
    "\n"
    "logging:\n"
    "  level: INFO\n";

/* ==========================================================================
 * Valid Configuration Tests
 * ========================================================================== */

/**
 * @test FW_UT_04_001: Load valid configuration
 * @pre Valid YAML config file exists
 * @post Configuration loaded successfully, all values correct
 */
static void test_config_load_valid(void **state) {
    (void)state;

    detector_config_t config;
    mock_yaml_set_content(valid_yaml_config);

    int result = config_load("detector_config.yaml", &config);

    assert_int_equal(result, 0);
    assert_int_equal(config.rows, 2048);
    assert_int_equal(config.cols, 2048);
    assert_int_equal(config.bit_depth, 16);
    assert_int_equal(config.frame_rate, 15);
    assert_int_equal(config.spi_speed_hz, 50000000);
    assert_int_equal(config.data_port, 8000);
    assert_int_equal(config.control_port, 8001);
}

/**
 * @test FW_UT_04_002: Validate valid configuration
 * @pre Valid configuration structure
 * @post Validation passes
 */
static void test_config_validate_valid(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 2048,
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 15,
        .line_time_us = 50,
        .frame_time_us = 66667,
        .spi_speed_hz = 50000000,
        .spi_mode = 0,
        .csi2_lane_speed_mbps = 400,
        .csi2_lanes = 4,
        .host_ip = "192.168.1.100",
        .data_port = 8000,
        .control_port = 8001,
        .send_buffer_size = 16777216,
        .scan_mode = 1,
        .log_level = 2,  /* INFO */
    };

    int result = config_validate(&config);
    assert_int_equal(result, 0);
}

/* ==========================================================================
 * Invalid Configuration Tests (REQ-FW-130)
 * ========================================================================== */

/**
 * @test FW_UT_04_003: Resolution too small
 * @pre Configuration with rows < 128
 * @post Validation fails with error
 */
static void test_config_validate_resolution_too_small(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 64,  /* Too small, minimum is 128 */
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 15,
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_004: Resolution too large
 * @pre Configuration with rows > 4096
 * @post Validation fails with error
 */
static void test_config_validate_resolution_too_large(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 8192,  /* Too large, maximum is 4096 */
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 15,
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_005: Invalid bit depth
 * @pre Configuration with bit_depth = 8 (not 14 or 16)
 * @post Validation fails with error
 */
static void test_config_validate_invalid_bit_depth(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 2048,
        .cols = 2048,
        .bit_depth = 8,  /* Invalid, must be 14 or 16 */
        .frame_rate = 15,
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_006: Frame rate out of range
 * @pre Configuration with frame_rate = 100 (> 60)
 * @post Validation fails with error
 */
static void test_config_validate_frame_rate_too_high(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 2048,
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 100,  /* Too high, maximum is 60 */
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_007: SPI speed out of range
 * @pre Configuration with spi_speed_hz > 50 MHz
 * @post Validation fails with error
 */
static void test_config_validate_spi_speed_too_high(void **state) {
    (void)state;

    detector_config_t config = {
        .spi_speed_hz = 100000000,  /* 100 MHz, maximum is 50 MHz */
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_008: Network port out of range
 * @pre Configuration with data_port = 100 (< 1024)
 * @post Validation fails with error (privileged port)
 */
static void test_config_validate_port_too_low(void **state) {
    (void)state;

    detector_config_t config = {
        .data_port = 100,  /* Privileged port, minimum is 1024 */
        .control_port = 8001,
    };

    int result = config_validate(&config);
    assert_int_equal(result, -EINVAL);
}

/* ==========================================================================
 * Boundary Tests
 * ========================================================================== */

/**
 * @test FW_UT_04_009: Minimum valid resolution
 * @pre Configuration with rows = 128, cols = 128
 * @post Validation passes
 */
static void test_config_validate_min_resolution(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 128,  /* Minimum */
        .cols = 128,
        .bit_depth = 14,
        .frame_rate = 1,
    };

    int result = config_validate(&config);
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_04_010: Maximum valid resolution
 * @pre Configuration with rows = 4096, cols = 4096
 * @post Validation passes
 */
static void test_config_validate_max_resolution(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 4096,  /* Maximum */
        .cols = 4096,
        .bit_depth = 16,
        .frame_rate = 15,
    };

    int result = config_validate(&config);
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_04_011: Minimum frame rate
 * @pre Configuration with frame_rate = 1
 * @post Validation passes
 */
static void test_config_validate_min_frame_rate(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 2048,
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 1,  /* Minimum */
    };

    int result = config_validate(&config);
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_04_012: Maximum frame rate
 * @pre Configuration with frame_rate = 60
 * @post Validation passes
 */
static void test_config_validate_max_frame_rate(void **state) {
    (void)state;

    detector_config_t config = {
        .rows = 2048,
        .cols = 2048,
        .bit_depth = 16,
        .frame_rate = 60,  /* Maximum */
    };

    int result = config_validate(&config);
    assert_int_equal(result, 0);
}

/* ==========================================================================
 * Hot/Cold Parameter Tests (REQ-FW-131)
 * ========================================================================== */

/**
 * @test FW_UT_04_013: Hot-swappable parameters
 * @pre Various parameter names
 * @post Returns true for hot-swappable parameters
 */
static void test_config_hot_swappable_parameters(void **state) {
    (void)state;

    /* Hot-swappable parameters */
    assert_true(config_is_hot_swappable("frame_rate"));
    assert_true(config_is_hot_swappable("host_ip"));
    assert_true(config_is_hot_swappable("data_port"));
    assert_true(config_is_hot_swappable("control_port"));
    assert_true(config_is_hot_swappable("log_level"));
}

/**
 * @test FW_UT_04_014: Cold parameters
 * @pre Various parameter names
 * @post Returns false for cold parameters
 */
static void test_config_cold_parameters(void **state) {
    (void)state;

    /* Cold parameters (require scan stop) */
    assert_false(config_is_hot_swappable("rows"));
    assert_false(config_is_hot_swappable("cols"));
    assert_false(config_is_hot_swappable("bit_depth"));
    assert_false(config_is_hot_swappable("csi2_lane_speed_mbps"));
    assert_false(config_is_hot_swappable("csi2_lanes"));
}

/* ==========================================================================
 * Error Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_04_015: File not found
 * @pre Configuration file does not exist
 * @post Returns error
 */
static void test_config_load_file_not_found(void **state) {
    (void)state;

    detector_config_t config;
    mock_yaml_set_content(NULL);  /* Simulate file not found */

    int result = config_load("nonexistent.yaml", &config);
    assert_int_equal(result, -ENOENT);
}

/**
 * @test FW_UT_04_016: Malformed YAML
 * @pre Invalid YAML syntax
 * @post Returns error
 */
static void test_config_load_malformed_yaml(void **state) {
    (void)state;

    const char *malformed_yaml = "panel:\n  rows: 2048\n  cols: [unclosed\n";
    mock_yaml_set_content(malformed_yaml);

    detector_config_t config;
    int result = config_load("detector_config.yaml", &config);
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_04_017: NULL parameters
 * @pre config = NULL
 * @post Returns error
 */
static void test_config_load_null_config(void **state) {
    (void)state;

    mock_yaml_set_content(valid_yaml_config);
    int result = config_load("detector_config.yaml", NULL);
    assert_int_equal(result, -EINVAL);
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Valid configuration tests */
        cmocka_unit_test(test_config_load_valid),
        cmocka_unit_test(test_config_validate_valid),

        /* Invalid configuration tests */
        cmocka_unit_test(test_config_validate_resolution_too_small),
        cmocka_unit_test(test_config_validate_resolution_too_large),
        cmocka_unit_test(test_config_validate_invalid_bit_depth),
        cmocka_unit_test(test_config_validate_frame_rate_too_high),
        cmocka_unit_test(test_config_validate_spi_speed_too_high),
        cmocka_unit_test(test_config_validate_port_too_low),

        /* Boundary tests */
        cmocka_unit_test(test_config_validate_min_resolution),
        cmocka_unit_test(test_config_validate_max_resolution),
        cmocka_unit_test(test_config_validate_min_frame_rate),
        cmocka_unit_test(test_config_validate_max_frame_rate),

        /* Hot/cold parameter tests */
        cmocka_unit_test(test_config_hot_swappable_parameters),
        cmocka_unit_test(test_config_cold_parameters),

        /* Error handling tests */
        cmocka_unit_test(test_config_load_file_not_found),
        cmocka_unit_test(test_config_load_malformed_yaml),
        cmocka_unit_test(test_config_load_null_config),
    };

    return cmocka_run_group_tests_name("FW-UT-04: Configuration Loader Tests",
                                       tests, NULL, NULL);
}
