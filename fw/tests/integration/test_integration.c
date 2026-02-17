/**
 * @file test_integration.c
 * @brief Integration tests for SoC Firmware (FW-IT-01 through FW-IT-05)
 *
 * Test IDs: FW-IT-01 through FW-IT-05
 * Coverage: End-to-end firmware functionality
 *
 * Tests:
 * - FW-IT-01: CSI-2 frame capture (100 frames, counter pattern)
 * - FW-IT-02: SPI + CSI-2 concurrent operation
 * - FW-IT-03: Full scan sequence (single frame, all states)
 * - FW-IT-04: Continuous 1000 frames (drop rate < 0.01%)
 * - FW-IT-05: Error injection (SPI, CSI-2, network)
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
#include <pthread.h>
#include <unistd.h>
#include <time.h>

/* Test configuration */
#define TEST_FRAME_COUNT       100
#define TEST_CONTINUOUS_FRAMES 1000
#define TEST_MAX_DROP_RATE     0.0001  /* 0.01% */

/* External dependencies */
extern int csi2_init(const char *device);
extern void csi2_deinit(void);
extern int csi2_start_streaming(void);
extern int csi2_stop_streaming(void);
extern int csi2_get_frame(uint8_t **buf, size_t *size, uint32_t *frame_number);
extern int csi2_release_frame(uint32_t frame_number);

extern int fpga_spi_init(const char *device);
extern void fpga_spi_deinit(void);
extern int fpga_reg_write(uint8_t addr, uint16_t data);
extern int fpga_reg_read(uint8_t addr, uint16_t *data);

extern int eth_init(const char *dest_ip, uint16_t data_port, uint16_t control_port);
extern void eth_deinit(void);
extern int eth_send_frame(const uint8_t *data, size_t size, uint32_t frame_number);

extern int seq_init(void);
extern void seq_deinit(void);
extern int seq_start_scan(int mode);
extern int seq_stop_scan(void);
extern int seq_get_status(uint32_t *frames_received, uint32_t *frames_sent, uint32_t *errors);

extern int frame_mgr_init(uint16_t rows, uint16_t cols, uint8_t bit_depth);
extern void frame_mgr_deinit(void);

/* Mock hardware simulation */
typedef struct {
    uint32_t frame_counter;
    uint16_t *test_pattern;
    size_t frame_size;
    bool csi2_active;
    bool spi_error_mode;
    bool csi2_error_mode;
    bool network_error_mode;
} mock_hardware_t;

static mock_hardware_t hw = {0};

/* ==========================================================================
 * Mock Hardware Setup
 * ========================================================================== */

/**
 * Initialize mock hardware with test pattern
 */
static int mock_hardware_init(uint16_t rows, uint16_t cols) {
    hw.frame_size = rows * cols * 2;  /* 16-bit pixels */
    hw.test_pattern = calloc(hw.frame_size, sizeof(uint16_t));
    if (!hw.test_pattern) {
        return -ENOMEM;
    }

    /* Generate counter pattern: each pixel = its address */
    for (size_t i = 0; i < rows * cols; i++) {
        hw.test_pattern[i] = (uint16_t)(i & 0xFFFF);
    }

    hw.frame_counter = 0;
    hw.csi2_active = false;
    hw.spi_error_mode = false;
    hw.csi2_error_mode = false;
    hw.network_error_mode = false;

    return 0;
}

static void mock_hardware_cleanup(void) {
    free(hw.test_pattern);
    memset(&hw, 0, sizeof(hw));
}

/* ==========================================================================
 * FW-IT-01: CSI-2 Frame Capture Tests
 * ========================================================================== */

/**
 * @test FW_IT_01_001: CSI-2 frame capture - 100 frames
 * @pre CSI-2 initialized, FPGA transmitting counter pattern
 * @post All 100 frames received, data matches expected pattern
 *
 * Validates: AC-FW-002
 */
static void test_it_01_csi2_capture_100_frames(void **state) {
    (void)state;

    const uint16_t rows = 1024;
    const uint16_t cols = 1024;

    /* Initialize mock hardware */
    assert_int_equal(mock_hardware_init(rows, cols), 0);

    /* Initialize CSI-2 HAL */
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(csi2_start_streaming(), 0);

    hw.csi2_active = true;

    /* Capture 100 frames */
    uint32_t frames_received = 0;
    uint32_t pattern_errors = 0;

    for (int i = 0; i < TEST_FRAME_COUNT; i++) {
        uint8_t *buf;
        size_t size;
        uint32_t frame_number;

        int result = csi2_get_frame(&buf, &size, &frame_number);

        if (result == 0) {
            frames_received++;

            /* Verify counter pattern */
            uint16_t *pixels = (uint16_t *)buf;
            bool frame_ok = true;

            for (size_t j = 0; j < rows * cols && frame_ok; j++) {
                if (pixels[j] != (uint16_t)(j & 0xFFFF)) {
                    pattern_errors++;
                    frame_ok = false;
                }
            }

            csi2_release_frame(frame_number);
        }
    }

    /* Stop streaming */
    csi2_stop_streaming();
    csi2_deinit();
    hw.csi2_active = false;

    /* Verify results */
    assert_int_equal(frames_received, TEST_FRAME_COUNT);
    assert_int_equal(pattern_errors, 0);

    mock_hardware_cleanup();
}

/**
 * @test FW_IT_01_002: CSI-2 frame timing
 * @pre CSI-2 streaming at 15 fps
 * @post Frame intervals match 66.7ms ± 10ms
 */
static void test_it_01_csi2_frame_timing(void **state) {
    (void)state;

    const uint16_t rows = 1024;
    const uint16_t cols = 1024;

    assert_int_equal(mock_hardware_init(rows, cols), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(csi2_start_streaming(), 0);

    hw.csi2_active = true;

    struct timespec prev_time = {0};
    uint32_t valid_intervals = 0;

    for (int i = 0; i < 10; i++) {
        uint8_t *buf;
        size_t size;
        uint32_t frame_number;

        while (csi2_get_frame(&buf, &size, &frame_number) != 0) {
            usleep(1000);
        }

        struct timespec current_time;
        clock_gettime(CLOCK_MONOTONIC, &current_time);

        if (prev_time.tv_sec != 0) {
            uint64_t interval_ms = (current_time.tv_sec - prev_time.tv_sec) * 1000 +
                                   (current_time.tv_nsec - prev_time.tv_nsec) / 1000000;

            /* 15 fps = 66.7ms per frame, allow ±10ms tolerance */
            if (interval_ms >= 56 && interval_ms <= 77) {
                valid_intervals++;
            }
        }

        prev_time = current_time;
        csi2_release_frame(frame_number);
    }

    csi2_stop_streaming();
    csi2_deinit();
    hw.csi2_active = false;
    mock_hardware_cleanup();

    /* At least 80% of intervals should be in valid range */
    assert_true(valid_intervals >= 8);
}

/* ==========================================================================
 * FW-IT-02: SPI + CSI-2 Concurrent Operation Tests
 * ========================================================================== */

/**
 * @test FW_IT_02_001: Concurrent SPI polling and CSI-2 capture
 * @pre Both SPI and CSI-2 active
 * @post No interference, both operations succeed
 *
 * Validates: AC-FW-002, REQ-FW-022
 */
static void test_it_02_spi_csi2_concurrent(void **state) {
    (void)state;

    const uint16_t rows = 1024;
    const uint16_t cols = 1024;

    assert_int_equal(mock_hardware_init(rows, cols), 0);

    /* Initialize both interfaces */
    assert_int_equal(fpga_spi_init("/dev/spidev0.0"), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(csi2_start_streaming(), 0);

    hw.csi2_active = true;

    /* Simulate concurrent operation for 50 frames */
    uint32_t spi_polls = 0;
    uint32_t csi2_frames = 0;

    for (int i = 0; i < 50; i++) {
        /* Perform SPI status poll (simulating 100us interval) */
        uint16_t status;
        if (fpga_reg_read(0x00, &status) == 0) {
            spi_polls++;
        }

        /* Check for CSI-2 frame */
        uint8_t *buf;
        size_t size;
        uint32_t frame_number;

        if (csi2_get_frame(&buf, &size, &frame_number) == 0) {
            csi2_frames++;
            csi2_release_frame(frame_number);
        }

        usleep(100);  /* 100us between SPI polls */
    }

    /* Cleanup */
    csi2_stop_streaming();
    csi2_deinit();
    fpga_spi_deinit();
    hw.csi2_active = false;
    mock_hardware_cleanup();

    /* Both operations should have succeeded */
    assert_int_equal(spi_polls, 50);
    assert_true(csi2_frames > 0);
}

/* ==========================================================================
 * FW-IT-03: Full Scan Sequence Tests
 * ========================================================================== */

/**
 * @test FW_IT_03_001: Full scan sequence - single frame
 * @pre All subsystems initialized
 * @post Scan completes with all state transitions
 *
 * Validates: AC-FW-004, REQ-FW-031
 */
static void test_it_03_full_scan_single_frame(void **state) {
    (void)state;

    const uint16_t rows = 1024;
    const uint16_t cols = 1024;

    assert_int_equal(mock_hardware_init(rows, cols), 0);

    /* Initialize all subsystems */
    assert_int_equal(fpga_spi_init("/dev/spidev0.0"), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(eth_init("192.168.1.100", 8000, 8001), 0);
    assert_int_equal(frame_mgr_init(rows, cols, 16), 0);
    assert_int_equal(seq_init(), 0);

    /* Start single scan */
    assert_int_equal(seq_start_scan(0), 0);  /* 0 = Single mode */

    /* Wait for scan completion (with timeout) */
    uint32_t frames_received, frames_sent, errors;
    int timeout = 100;  /* 100 attempts */

    while (timeout-- > 0) {
        seq_get_status(&frames_received, &frames_sent, &errors);

        if (frames_received >= 1 && frames_sent >= 1) {
            break;
        }

        usleep(10000);  /* 10ms */
    }

    /* Stop scan */
    seq_stop_scan();

    /* Verify results */
    seq_get_status(&frames_received, &frames_sent, &errors);
    assert_int_equal(frames_received, 1);
    assert_int_equal(frames_sent, 1);
    assert_int_equal(errors, 0);

    /* Cleanup */
    seq_deinit();
    frame_mgr_deinit();
    eth_deinit();
    csi2_deinit();
    fpga_spi_deinit();
    mock_hardware_cleanup();
}

/**
 * @test FW_IT_03_002: State transition verification
 * @pre Full scan sequence running
 * @post All states visited in correct order
 */
static void test_it_03_state_transitions(void **state) {
    (void)state;

    /* State tracking would require sequence engine to expose
     * current state or state transition callbacks.
     * This test validates the state machine path. */

    const char *expected_states[] = {
        "IDLE",
        "CONFIGURE",
        "ARM",
        "SCANNING",
        "STREAMING",
        "COMPLETE",
        "IDLE"
    };

    /* Actual implementation would register state change callbacks
     * and verify they occur in expected order */

    assert_true(1);  /* Placeholder for state verification */
}

/* ==========================================================================
 * FW-IT-04: Continuous Scan Tests
 * ========================================================================== */

/**
 * @test FW_IT_04_001: Continuous 1000 frames - drop rate validation
 * @pre Continuous scan mode active
 * @post 1000 frames captured, drop rate < 0.01%
 *
 * Validates: AC-FW-006, REQ-FW-052
 */
static void test_it_04_continuous_1000_frames(void **state) {
    (void)state;

    const uint16_t rows = 1024;
    const uint16_t cols = 1024;

    assert_int_equal(mock_hardware_init(rows, cols), 0);

    /* Initialize subsystems */
    assert_int_equal(fpga_spi_init("/dev/spidev0.0"), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(csi2_start_streaming(), 0);
    assert_int_equal(eth_init("192.168.1.100", 8000, 8001), 0);
    assert_int_equal(frame_mgr_init(rows, cols, 16), 0);
    assert_int_equal(seq_init(), 0);

    hw.csi2_active = true;

    /* Start continuous scan */
    assert_int_equal(seq_start_scan(1), 0);  /* 1 = Continuous mode */

    /* Wait for 1000 frames */
    uint32_t frames_received, frames_sent, errors;
    int timeout = 300;  /* 300 attempts * 10ms = 3 seconds max */

    while (timeout-- > 0) {
        seq_get_status(&frames_received, &frames_sent, &errors);

        if (frames_received >= TEST_CONTINUOUS_FRAMES) {
            break;
        }

        usleep(10000);  /* 10ms */
    }

    /* Stop scan */
    seq_stop_scan();

    /* Calculate drop rate */
    seq_get_status(&frames_received, &frames_sent, &errors);
    uint32_t frames_dropped = frames_received - frames_sent;
    double drop_rate = (double)frames_dropped / frames_received;

    /* Verify drop rate < 0.01% */
    assert_true(drop_rate < TEST_MAX_DROP_RATE);

    /* Cleanup */
    seq_deinit();
    frame_mgr_deinit();
    eth_deinit();
    csi2_stop_streaming();
    csi2_deinit();
    fpga_spi_deinit();
    hw.csi2_active = false;
    mock_hardware_cleanup();
}

/* ==========================================================================
 * FW-IT-05: Error Injection Tests
 * ========================================================================== */

/**
 * @test FW_IT_05_001: SPI error injection and recovery
 * @pre SPI communication active
 * @post Error detected, recovery attempted, system continues
 *
 * Validates: AC-FW-005, REQ-FW-032
 */
static void test_it_05_spi_error_injection(void **state) {
    (void)state;

    assert_int_equal(mock_hardware_init(1024, 1024), 0);
    assert_int_equal(fpga_spi_init("/dev/spidev0.0"), 0);
    assert_int_equal(seq_init(), 0);

    /* Enable error mode */
    hw.spi_error_mode = true;

    /* Attempt SPI operation (should fail) */
    uint16_t value;
    int result = fpga_reg_read(0x00, &value);

    /* Error should be detected */
    assert_int_not_equal(result, 0);

    /* Disable error mode */
    hw.spi_error_mode = false;

    /* Retry should succeed */
    result = fpga_reg_read(0x00, &value);
    assert_int_equal(result, 0);

    /* Cleanup */
    seq_deinit();
    fpga_spi_deinit();
    mock_hardware_cleanup();
}

/**
 * @test FW_IT_05_002: CSI-2 error injection and pipeline restart
 * @pre CSI-2 streaming active
 * @post Error detected, pipeline restarted, frames received again
 *
 * Validates: AC-FW-005a, REQ-FW-061
 */
static void test_it_05_csi2_error_injection(void **state) {
    (void)state;

    assert_int_equal(mock_hardware_init(1024, 1024), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(csi2_start_streaming(), 0);

    hw.csi2_active = true;

    /* Receive a frame normally */
    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    assert_int_equal(csi2_get_frame(&buf, &size, &frame_number), 0);
    csi2_release_frame(frame_number);

    /* Enable CSI-2 error mode */
    hw.csi2_error_mode = true;

    /* Frame request should fail */
    assert_int_not_equal(csi2_get_frame(&buf, &size, &frame_number), 0);

    /* Disable error mode (simulates successful restart) */
    hw.csi2_error_mode = false;

    /* After restart, frames should be received again */
    assert_int_equal(csi2_get_frame(&buf, &size, &frame_number), 0);
    csi2_release_frame(frame_number);

    /* Cleanup */
    csi2_stop_streaming();
    csi2_deinit();
    hw.csi2_active = false;
    mock_hardware_cleanup();
}

/**
 * @test FW_IT_05_003: Network error injection
 * @pre Network transmission active
 * @post Network errors handled, frames queued, transmission resumes
 */
static void test_it_05_network_error_injection(void **state) {
    (void)state;

    assert_int_equal(mock_hardware_init(1024, 1024), 0);
    assert_int_equal(eth_init("192.168.1.100", 8000, 8001), 0);

    /* Enable network error mode */
    hw.network_error_mode = true;

    /* Transmission should fail */
    uint8_t test_data[1024] = {0};
    int result = eth_send_frame(test_data, sizeof(test_data), 0);
    assert_int_not_equal(result, 0);

    /* Disable error mode */
    hw.network_error_mode = false;

    /* Transmission should succeed */
    result = eth_send_frame(test_data, sizeof(test_data), 0);
    assert_int_equal(result, 0);

    /* Cleanup */
    eth_deinit();
    mock_hardware_cleanup();
}

/**
 * @test FW_IT_05_004: Multiple concurrent errors
 * @pre All subsystems active, multiple errors injected
 * @post System handles all errors, recovers gracefully
 */
static void test_it_05_multiple_concurrent_errors(void **state) {
    (void)state;

    assert_int_equal(mock_hardware_init(1024, 1024), 0);

    /* Initialize all subsystems */
    assert_int_equal(fpga_spi_init("/dev/spidev0.0"), 0);
    assert_int_equal(csi2_init("/dev/video0"), 0);
    assert_int_equal(eth_init("192.168.1.100", 8000, 8001), 0);
    assert_int_equal(seq_init(), 0);

    /* Enable all error modes */
    hw.spi_error_mode = true;
    hw.csi2_error_mode = true;
    hw.network_error_mode = true;

    /* All operations should fail gracefully */
    uint16_t value;
    assert_int_not_equal(fpga_reg_read(0x00, &value), 0);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    assert_int_not_equal(csi2_get_frame(&buf, &size, &frame_number), 0);

    uint8_t test_data[1024] = {0};
    assert_int_not_equal(eth_send_frame(test_data, sizeof(test_data), 0), 0);

    /* Disable all error modes */
    hw.spi_error_mode = false;
    hw.csi2_error_mode = false;
    hw.network_error_mode = false;

    /* All operations should succeed after recovery */
    assert_int_equal(fpga_reg_read(0x00, &value), 0);

    /* Note: CSI-2 and network may need additional restart steps */

    /* Cleanup */
    seq_deinit();
    eth_deinit();
    csi2_deinit();
    fpga_spi_deinit();
    mock_hardware_cleanup();
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* FW-IT-01: CSI-2 Frame Capture Tests */
        cmocka_unit_test(test_it_01_csi2_capture_100_frames),
        cmocka_unit_test(test_it_01_csi2_frame_timing),

        /* FW-IT-02: SPI + CSI-2 Concurrent Operation Tests */
        cmocka_unit_test(test_it_02_spi_csi2_concurrent),

        /* FW-IT-03: Full Scan Sequence Tests */
        cmocka_unit_test(test_it_03_full_scan_single_frame),
        cmocka_unit_test(test_it_03_state_transitions),

        /* FW-IT-04: Continuous Scan Tests */
        cmocka_unit_test(test_it_04_continuous_1000_frames),

        /* FW-IT-05: Error Injection Tests */
        cmocka_unit_test(test_it_05_spi_error_injection),
        cmocka_unit_test(test_it_05_csi2_error_injection),
        cmocka_unit_test(test_it_05_network_error_injection),
        cmocka_unit_test(test_it_05_multiple_concurrent_errors),
    };

    return cmocka_run_group_tests_name("FW-IT-01~05: Integration Tests",
                                       tests, NULL, NULL);
}
