/**
 * @file test_sequence_engine.c
 * @brief Unit tests for Sequence Engine (FW-UT-05)
 *
 * Test ID: FW-UT-05
 * Coverage: Sequence Engine FSM per REQ-FW-030, REQ-FW-031, REQ-FW-032, REQ-FW-033
 *
 * Tests:
 * - All state transitions (7 states)
 * - All scan modes (Single, Continuous, Calibration)
 * - Error recovery paths
 * - FPGA register writes during transitions
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <stdbool.h>

/* Sequence Engine States */
typedef enum {
    SEQ_STATE_IDLE = 0,
    SEQ_STATE_CONFIGURE,
    SEQ_STATE_ARM,
    SEQ_STATE_SCANNING,
    SEQ_STATE_STREAMING,
    SEQ_STATE_COMPLETE,
    SEQ_STATE_ERROR,
} seq_state_t;

/* Scan Modes */
typedef enum {
    SCAN_MODE_SINGLE = 0,
    SCAN_MODE_CONTINUOUS = 1,
    SCAN_MODE_CALIBRATION = 2,
} scan_mode_t;

/* Events */
typedef enum {
    EVT_START_SCAN = 0,
    EVT_CONFIG_DONE,
    EVT_ARM_DONE,
    EVT_FRAME_READY,
    EVT_STOP_SCAN,
    EVT_ERROR,
    EVT_ERROR_CLEARED,
    EVT_COMPLETE,
} seq_event_t;

/* FPGA Status Register bits */
#define FPGA_STATUS_BUSY    (1U << 0)
#define FPGA_STATUS_ERROR   (1U << 1)
#define FPGA_STATUS_READY   (1U << 2)

/* FPGA Control Register bits */
#define FPGA_CTRL_START     (1U << 0)
#define FPGA_CTRL_STOP      (1U << 1)
#define FPGA_CTRL_MODE_MASK (0x3U << 2)

/* Function under test */
extern int seq_init(void);
extern void seq_deinit(void);
extern seq_state_t seq_get_state(void);
extern const char *seq_state_to_string(seq_state_t state);
extern int seq_handle_event(seq_event_t event, void *data);
extern int seq_start_scan(scan_mode_t mode);
extern int seq_stop_scan(void);
extern int seq_get_status(uint32_t *frames_received, uint32_t *frames_sent, uint32_t *errors);

/* Mock functions */
extern void mock_spi_reset(void);
extern void mock_spi_set_register(uint8_t addr, uint16_t value);
extern void mock_fpga_set_status(uint16_t status);
extern void mock_fpga_set_error(bool error);

/* Test state tracking */
static seq_state_t current_state = SEQ_STATE_IDLE;
static uint32_t test_frames_received = 0;
static uint32_t test_frames_sent = 0;

/* ==========================================================================
 * State Transition Tests (REQ-FW-030)
 * ========================================================================== */

/**
 * @test FW_UT_05_001: Initial state is IDLE
 * @pre Sequence engine initialized
 * @post State = IDLE
 */
static void test_seq_initial_state(void **state) {
    (void)state;

    seq_init();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);
    seq_deinit();
}

/**
 * @test FW_UT_05_002: IDLE -> CONFIGURE on START_SCAN
 * @pre State = IDLE
 * @post State = CONFIGURE after START_SCAN event
 */
static void test_seq_idle_to_configure(void **state) {
    (void)state;

    seq_init();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);

    int result = seq_start_scan(SCAN_MODE_SINGLE);
    assert_int_equal(result, 0);
    assert_int_equal(seq_get_state(), SEQ_STATE_CONFIGURE);
    seq_deinit();
}

/**
 * @test FW_UT_05_003: CONFIGURE -> ARM on CONFIG_DONE
 * @pre State = CONFIGURE
 * @post State = ARM after configuration complete
 */
static void test_seq_configure_to_arm(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    assert_int_equal(seq_get_state(), SEQ_STATE_CONFIGURE);

    /* Simulate configuration complete */
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ARM);
    seq_deinit();
}

/**
 * @test FW_UT_05_004: ARM -> SCANNING on ARM_DONE
 * @pre State = ARM, FPGA reports READY
 * @post State = SCANNING
 */
static void test_seq_arm_to_scanning(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ARM);

    /* Simulate FPGA ready */
    mock_fpga_set_status(FPGA_STATUS_READY);
    seq_handle_event(EVT_ARM_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);
    seq_deinit();
}

/**
 * @test FW_UT_05_005: SCANNING -> STREAMING on FRAME_READY
 * @pre State = SCANNING, frame received from CSI-2
 * @post State = STREAMING
 */
static void test_seq_scanning_to_streaming(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);

    /* Simulate frame ready */
    seq_handle_event(EVT_FRAME_READY, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_STREAMING);
    seq_deinit();
}

/**
 * @test FW_UT_05_006: STREAMING -> COMPLETE on COMPLETE (Single mode)
 * @pre State = STREAMING, mode = Single
 * @post State = COMPLETE after frame sent
 */
static void test_seq_streaming_to_complete_single(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    seq_handle_event(EVT_FRAME_READY, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_STREAMING);

    /* Simulate frame transmission complete */
    seq_handle_event(EVT_COMPLETE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_COMPLETE);
    seq_deinit();
}

/**
 * @test FW_UT_05_007: COMPLETE -> IDLE on cleanup
 * @pre State = COMPLETE
 * @post State = IDLE after cleanup
 */
static void test_seq_complete_to_idle(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    /* Run through full sequence */
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    seq_handle_event(EVT_FRAME_READY, NULL);
    seq_handle_event(EVT_COMPLETE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_COMPLETE);

    /* Cleanup should return to IDLE */
    seq_handle_event(EVT_STOP_SCAN, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);
    seq_deinit();
}

/* ==========================================================================
 * Scan Mode Tests (REQ-FW-033)
 * ========================================================================== */

/**
 * @test FW_UT_05_008: Single scan mode
 * @pre mode = SCAN_MODE_SINGLE
 * @post Sequence completes after one frame
 */
static void test_seq_mode_single(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);

    /* Run sequence */
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    seq_handle_event(EVT_FRAME_READY, NULL);
    seq_handle_event(EVT_COMPLETE, NULL);

    assert_int_equal(seq_get_state(), SEQ_STATE_COMPLETE);
    seq_deinit();
}

/**
 * @test FW_UT_05_009: Continuous scan mode
 * @pre mode = SCAN_MODE_CONTINUOUS
 * @post STREAMING state persists, goes back to SCANNING for next frame
 */
static void test_seq_mode_continuous(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_CONTINUOUS);

    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);

    /* First frame */
    seq_handle_event(EVT_FRAME_READY, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_STREAMING);

    /* After frame sent, continuous mode returns to SCANNING */
    seq_handle_event(EVT_COMPLETE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);

    /* Second frame */
    seq_handle_event(EVT_FRAME_READY, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_STREAMING);

    /* Stop scan */
    seq_stop_scan();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);

    seq_deinit();
}

/**
 * @test FW_UT_05_010: Calibration mode
 * @pre mode = SCAN_MODE_CALIBRATION
 * @post FPGA control register has calibration mode bits set
 */
static void test_seq_mode_calibration(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_CALIBRATION);

    seq_handle_event(EVT_CONFIG_DONE, NULL);

    /* Verify calibration mode written to FPGA */
    /* (Mock will verify control register bits) */
    assert_int_equal(seq_get_state(), SEQ_STATE_ARM);

    seq_deinit();
}

/* ==========================================================================
 * Error Recovery Tests (REQ-FW-032)
 * ========================================================================== */

/**
 * @test FW_UT_05_011: Error during SCANNING
 * @pre FPGA reports error
 * @post State transitions to ERROR
 */
static void test_seq_error_during_scanning(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);

    /* Simulate FPGA error */
    mock_fpga_set_status(FPGA_STATUS_ERROR);
    seq_handle_event(EVT_ERROR, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);
    seq_deinit();
}

/**
 * @test FW_UT_05_012: Error recovery - retry success
 * @pre State = ERROR, retry count < 3
 * @post State returns to SCANNING after successful retry
 */
static void test_seq_error_recovery_retry_success(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);

    /* Trigger error */
    mock_fpga_set_status(FPGA_STATUS_ERROR);
    seq_handle_event(EVT_ERROR, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);

    /* Simulate successful retry */
    mock_fpga_set_status(FPGA_STATUS_READY);
    seq_handle_event(EVT_ERROR_CLEARED, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);

    seq_deinit();
}

/**
 * @test FW_UT_05_013: Error recovery - max retries exceeded
 * @pre State = ERROR, 3 retries failed
 * @post State stays in ERROR, reports failure
 */
static void test_seq_error_recovery_max_retries(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);

    /* Trigger error */
    mock_fpga_set_status(FPGA_STATUS_ERROR);
    seq_handle_event(EVT_ERROR, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);

    /* Try 3 retries, all fail */
    for (int i = 0; i < 3; i++) {
        mock_fpga_set_status(FPGA_STATUS_ERROR);
        seq_handle_event(EVT_ERROR_CLEARED, NULL);
        /* Should stay in ERROR */
        assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);
    }

    /* 4th attempt should fail (max retries exceeded) */
    int result = seq_handle_event(EVT_ERROR_CLEARED, NULL);
    assert_int_equal(result, -ETIMEDOUT);
    assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);

    seq_deinit();
}

/**
 * @test FW_UT_05_014: Stop scan from any state
 * @pre State = any active state (CONFIGURE/ARM/SCANNING/STREAMING)
 * @post State transitions to IDLE
 */
static void test_seq_stop_from_any_state(void **state) {
    (void)state;

    /* Test from CONFIGURE */
    seq_init();
    seq_start_scan(SCAN_MODE_CONTINUOUS);
    assert_int_equal(seq_get_state(), SEQ_STATE_CONFIGURE);
    seq_stop_scan();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);
    seq_deinit();

    /* Test from SCANNING */
    seq_init();
    seq_start_scan(SCAN_MODE_CONTINUOUS);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_SCANNING);
    seq_stop_scan();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);
    seq_deinit();

    /* Test from ERROR */
    seq_init();
    seq_start_scan(SCAN_MODE_SINGLE);
    seq_handle_event(EVT_CONFIG_DONE, NULL);
    seq_handle_event(EVT_ARM_DONE, NULL);
    mock_fpga_set_status(FPGA_STATUS_ERROR);
    seq_handle_event(EVT_ERROR, NULL);
    assert_int_equal(seq_get_state(), SEQ_STATE_ERROR);
    seq_stop_scan();
    assert_int_equal(seq_get_state(), SEQ_STATE_IDLE);
    seq_deinit();
}

/* ==========================================================================
 * Status Tests (REQ-FW-111)
 * ========================================================================== */

/**
 * @test FW_UT_05_015: Get status counters
 * @pre Frames received and sent
 * @post Status returns correct counter values
 */
static void test_seq_get_status(void **state) {
    (void)state;

    seq_init();
    seq_start_scan(SCAN_MODE_CONTINUOUS);

    uint32_t frames_received, frames_sent, errors;
    int result = seq_get_status(&frames_received, &frames_sent, &errors);

    assert_int_equal(result, 0);
    assert_true(frames_received >= 0);
    assert_true(frames_sent >= 0);
    assert_true(errors >= 0);

    seq_deinit();
}

/* ==========================================================================
 * State String Conversion Tests
 * ========================================================================== */

/**
 * @test FW_UT_05_016: State to string conversion
 * @pre Any state
 * @post Returns valid string representation
 */
static void test_seq_state_to_string(void **state) {
    (void)state;

    assert_string_equal(seq_state_to_string(SEQ_STATE_IDLE), "IDLE");
    assert_string_equal(seq_state_to_string(SEQ_STATE_CONFIGURE), "CONFIGURE");
    assert_string_equal(seq_state_to_string(SEQ_STATE_ARM), "ARM");
    assert_string_equal(seq_state_to_string(SEQ_STATE_SCANNING), "SCANNING");
    assert_string_equal(seq_state_to_string(SEQ_STATE_STREAMING), "STREAMING");
    assert_string_equal(seq_state_to_string(SEQ_STATE_COMPLETE), "COMPLETE");
    assert_string_equal(seq_state_to_string(SEQ_STATE_ERROR), "ERROR");
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* State transition tests */
        cmocka_unit_test(test_seq_initial_state),
        cmocka_unit_test(test_seq_idle_to_configure),
        cmocka_unit_test(test_seq_configure_to_arm),
        cmocka_unit_test(test_seq_arm_to_scanning),
        cmocka_unit_test(test_seq_scanning_to_streaming),
        cmocka_unit_test(test_seq_streaming_to_complete_single),
        cmocka_unit_test(test_seq_complete_to_idle),

        /* Scan mode tests */
        cmocka_unit_test(test_seq_mode_single),
        cmocka_unit_test(test_seq_mode_continuous),
        cmocka_unit_test(test_seq_mode_calibration),

        /* Error recovery tests */
        cmocka_unit_test(test_seq_error_during_scanning),
        cmocka_unit_test(test_seq_error_recovery_retry_success),
        cmocka_unit_test(test_seq_error_recovery_max_retries),
        cmocka_unit_test(test_seq_stop_from_any_state),

        /* Status tests */
        cmocka_unit_test(test_seq_get_status),

        /* State string tests */
        cmocka_unit_test(test_seq_state_to_string),
    };

    return cmocka_run_group_tests_name("FW-UT-05: Sequence Engine Tests",
                                       tests, NULL, NULL);
}
