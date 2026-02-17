/**
 * @file test_frame_manager.c
 * @brief Unit tests for Frame Manager (FW-UT-06)
 *
 * Test ID: FW-UT-06
 * Coverage: Frame Manager per REQ-FW-050, REQ-FW-051, REQ-FW-052
 *
 * Tests:
 * - Buffer state transitions (4-buffer ring)
 * - Producer (CSI-2 RX) and consumer (Ethernet TX) coordination
 * - Oldest-drop policy (REQ-FW-051)
 * - Drop counter and statistics (REQ-FW-052, REQ-FW-111)
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

/* Buffer states */
typedef enum {
    BUF_STATE_FREE = 0,
    BUF_STATE_FILLING,
    BUF_STATE_READY,
    BUF_STATE_SENDING,
} buf_state_t;

/* Frame buffer descriptor */
typedef struct {
    uint8_t *data;
    size_t size;
    buf_state_t state;
    uint32_t frame_number;
    uint16_t total_packets;
    uint16_t sent_packets;
} frame_buffer_t;

/* Frame Manager configuration */
typedef struct {
    uint16_t rows;
    uint16_t cols;
    uint8_t bit_depth;
    size_t frame_size;
    uint32_t num_buffers;
} frame_mgr_config_t;

/* Statistics counters */
typedef struct {
    uint64_t frames_received;
    uint64_t frames_sent;
    uint64_t frames_dropped;
    uint64_t packets_sent;
    uint64_t bytes_sent;
    uint64_t overruns;
} frame_stats_t;

/* Function under test */
extern int frame_mgr_init(const frame_mgr_config_t *config);
extern void frame_mgr_deinit(void);
extern int frame_mgr_get_buffer(uint32_t frame_number, uint8_t **buf, size_t *size);
extern int frame_mgr_commit_buffer(uint32_t frame_number);
extern int frame_mgr_get_ready_buffer(uint8_t **buf, size_t *size, uint32_t *frame_number);
extern int frame_mgr_release_buffer(uint32_t frame_number);
extern void frame_mgr_get_stats(frame_stats_t *stats);
extern buf_state_t frame_mgr_get_buffer_state(uint32_t frame_number);
extern const char *frame_mgr_state_to_string(buf_state_t state);

/* Test configuration */
static frame_mgr_config_t test_config = {
    .rows = 2048,
    .cols = 2048,
    .bit_depth = 16,
    .num_buffers = 4,
};

static size_t test_frame_size = 2048 * 2048 * 2;  /* 8 MB */

/* ==========================================================================
 * Initialization Tests (REQ-FW-050)
 * ========================================================================== */

/**
 * @test FW_UT_06_001: Initialize frame manager
 * @pre Valid configuration
 * @post All buffers allocated in FREE state
 */
static void test_frame_mgr_init(void **state) {
    (void)state;

    test_config.frame_size = test_frame_size;

    int result = frame_mgr_init(&test_config);
    assert_int_equal(result, 0);

    /* Verify all buffers are in FREE state */
    for (uint32_t i = 0; i < test_config.num_buffers; i++) {
        assert_int_equal(frame_mgr_get_buffer_state(i), BUF_STATE_FREE);
    }

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_002: Deinitialize frame manager
 * @pre Initialized frame manager
 * @post All buffers freed
 */
static void test_frame_mgr_deinit(void **state) {
    (void)state;

    test_config.frame_size = test_frame_size;

    frame_mgr_init(&test_config);
    frame_mgr_deinit();
    /* If we get here without crash, deinit succeeded */
}

/**
 * @test FW_UT_06_003: Initialize with NULL configuration
 * @pre config = NULL
 * @post Returns error
 */
static void test_frame_mgr_init_null_config(void **state) {
    (void)state;

    int result = frame_mgr_init(NULL);
    assert_int_equal(result, -EINVAL);
}

/* ==========================================================================
 * Buffer State Transition Tests
 * ========================================================================== */

/**
 * @test FW_UT_06_004: FREE -> FILLING state transition
 * @pre Buffer in FREE state
 * @post Buffer transitions to FILLING when get_buffer called
 */
static void test_frame_mgr_free_to_filling(void **state) {
    (void)state;

    test_config.frame_size = 1024;  /* Small size for testing */
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    int result = frame_mgr_get_buffer(0, &buf, &size);

    assert_int_equal(result, 0);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_FILLING);
    assert_non_null(buf);
    assert_int_equal(size, 1024);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_005: FILLING -> READY state transition
 * @pre Buffer in FILLING state
 * @post Buffer transitions to READY when commit_buffer called
 */
static void test_frame_mgr_filling_to_ready(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    frame_mgr_get_buffer(0, &buf, &size);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_FILLING);

    int result = frame_mgr_commit_buffer(0);
    assert_int_equal(result, 0);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_READY);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_006: READY -> SENDING state transition
 * @pre Buffer in READY state
 * @post Buffer transitions to SENDING when get_ready_buffer called
 */
static void test_frame_mgr_ready_to_sending(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    /* Fill and commit buffer */
    uint8_t *buf;
    size_t size;
    frame_mgr_get_buffer(0, &buf, &size);
    frame_mgr_commit_buffer(0);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_READY);

    /* Get ready buffer for sending */
    uint8_t *send_buf;
    size_t send_size;
    uint32_t frame_number;
    int result = frame_mgr_get_ready_buffer(&send_buf, &send_size, &frame_number);

    assert_int_equal(result, 0);
    assert_int_equal(frame_number, 0);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_SENDING);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_007: SENDING -> FREE state transition
 * @pre Buffer in SENDING state
 * @post Buffer transitions to FREE when release_buffer called
 */
static void test_frame_mgr_sending_to_free(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    /* Full cycle */
    uint8_t *buf, *send_buf;
    size_t size, send_size;
    uint32_t frame_number;

    frame_mgr_get_buffer(0, &buf, &size);
    frame_mgr_commit_buffer(0);
    frame_mgr_get_ready_buffer(&send_buf, &send_size, &frame_number);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_SENDING);

    int result = frame_mgr_release_buffer(0);
    assert_int_equal(result, 0);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_FREE);

    frame_mgr_deinit();
}

/* ==========================================================================
 * Oldest-Drop Policy Tests (REQ-FW-051)
 * ========================================================================== */

/**
 * @test FW_UT_06_008: Oldest-drop when all buffers busy
 * @pre All 4 buffers in SENDING state
 * @post New buffer request drops oldest SENDING buffer
 */
static void test_frame_mgr_oldest_drop(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    test_config.num_buffers = 4;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;

    /* Fill all buffers */
    for (uint32_t i = 0; i < 4; i++) {
        frame_mgr_get_buffer(i, &buf, &size);
        frame_mgr_commit_buffer(i);
        frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
    }

    /* All buffers should be in SENDING state */
    for (uint32_t i = 0; i < 4; i++) {
        assert_int_equal(frame_mgr_get_buffer_state(i), BUF_STATE_SENDING);
    }

    /* Request new buffer - should drop oldest (buffer 0) */
    frame_stats_t stats_before, stats_after;
    frame_mgr_get_stats(&stats_before);

    int result = frame_mgr_get_buffer(4, &buf, &size);

    frame_mgr_get_stats(&stats_after);

    /* Should succeed, buffer 0 should be dropped */
    assert_int_equal(result, 0);
    assert_int_equal(stats_after.frames_dropped, stats_before.frames_dropped + 1);
    assert_int_equal(frame_mgr_get_buffer_state(0), BUF_STATE_FREE);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_009: Drop counter increments
 * @pre Oldest-drop triggered
 * @post frames_dropped counter incremented
 */
static void test_frame_mgr_drop_counter(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    test_config.num_buffers = 4;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    frame_stats_t stats;

    /* Fill all buffers and trigger multiple drops */
    for (uint32_t i = 0; i < 4; i++) {
        frame_mgr_get_buffer(i, &buf, &size);
        frame_mgr_commit_buffer(i);
        frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
    }

    /* Trigger first drop */
    frame_mgr_get_buffer(4, &buf, &size);

    /* Trigger second drop */
    frame_mgr_get_buffer(5, &buf, &size);

    frame_mgr_get_stats(&stats);
    assert_int_equal(stats.frames_dropped, 2);

    frame_mgr_deinit();
}

/* ==========================================================================
 * Statistics Tests (REQ-FW-052, REQ-FW-111)
 * ========================================================================== */

/**
 * @test FW_UT_06_010: Frames received counter
 * @pre Buffers committed
 * @post frames_received counter increments
 */
static void test_frame_mgr_frames_received_counter(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    frame_stats_t stats;

    frame_mgr_get_buffer(0, &buf, &size);
    frame_mgr_commit_buffer(0);
    frame_mgr_get_buffer(1, &buf, &size);
    frame_mgr_commit_buffer(1);

    frame_mgr_get_stats(&stats);
    assert_int_equal(stats.frames_received, 2);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_011: Frames sent counter
 * @pre Buffers released after sending
 * @post frames_sent counter increments
 */
static void test_frame_mgr_frames_sent_counter(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    frame_stats_t stats;

    /* Complete cycle for 2 frames */
    for (uint32_t i = 0; i < 2; i++) {
        frame_mgr_get_buffer(i, &buf, &size);
        frame_mgr_commit_buffer(i);
        frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
        frame_mgr_release_buffer(i);
    }

    frame_mgr_get_stats(&stats);
    assert_int_equal(stats.frames_sent, 2);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_012: Overrun counter
 * @pre Oldest-drop triggered
 * @post overruns counter increments
 */
static void test_frame_mgr_overrun_counter(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    test_config.num_buffers = 4;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    frame_stats_t stats;

    /* Fill all buffers */
    for (uint32_t i = 0; i < 4; i++) {
        frame_mgr_get_buffer(i, &buf, &size);
        frame_mgr_commit_buffer(i);
        frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
    }

    /* Trigger overrun */
    frame_mgr_get_buffer(4, &buf, &size);

    frame_mgr_get_stats(&stats);
    assert_true(stats.overruns > 0);

    frame_mgr_deinit();
}

/* ==========================================================================
 * Error Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_06_013: Get buffer with invalid frame number
 * @pre frame_number >= num_buffers
 * @post Returns error
 */
static void test_frame_mgr_invalid_frame_number(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    test_config.num_buffers = 4;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;

    int result = frame_mgr_get_buffer(99, &buf, &size);
    assert_int_equal(result, -EINVAL);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_014: Commit buffer not in FILLING state
 * @pre Buffer in FREE state
 * @post Returns error
 */
static void test_frame_mgr_commit_invalid_state(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    int result = frame_mgr_commit_buffer(0);
    assert_int_equal(result, -EINVAL);

    frame_mgr_deinit();
}

/**
 * @test FW_UT_06_015: No ready buffers available
 * @pre All buffers in FREE/FILLING state
 * @post get_ready_buffer returns error
 */
static void test_frame_mgr_no_ready_buffers(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;

    int result = frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
    assert_int_equal(result, -ENOENT);

    frame_mgr_deinit();
}

/* ==========================================================================
 * State String Tests
 * ========================================================================== */

/**
 * @test FW_UT_06_016: State to string conversion
 * @pre Any state
 * @post Returns valid string representation
 */
static void test_frame_mgr_state_to_string(void **state) {
    (void)state;

    assert_string_equal(frame_mgr_state_to_string(BUF_STATE_FREE), "FREE");
    assert_string_equal(frame_mgr_state_to_string(BUF_STATE_FILLING), "FILLING");
    assert_string_equal(frame_mgr_state_to_string(BUF_STATE_READY), "READY");
    assert_string_equal(frame_mgr_state_to_string(BUF_STATE_SENDING), "SENDING");
}

/* ==========================================================================
 * Producer-Consumer Coordination Tests
 * ========================================================================== */

/**
 * @test FW_UT_06_017: Concurrent producer-consumer simulation
 * @pre Producer filling buffers, consumer sending
 * @post No data loss when consumer keeps up
 */
static void test_frame_mgr_producer_consumer_no_loss(void **state) {
    (void)state;

    test_config.frame_size = 1024;
    test_config.num_buffers = 4;
    frame_mgr_init(&test_config);

    uint8_t *buf;
    size_t size;
    uint32_t frame_number;
    frame_stats_t stats;

    /* Simulate producer-consumer pattern */
    for (uint32_t i = 0; i < 10; i++) {
        /* Producer: get and fill buffer */
        uint32_t buf_idx = i % 4;
        frame_mgr_get_buffer(buf_idx, &buf, &size);
        frame_mgr_commit_buffer(buf_idx);

        /* Consumer: send and release buffer */
        frame_mgr_get_ready_buffer(&buf, &size, &frame_number);
        frame_mgr_release_buffer(buf_idx);
    }

    frame_mgr_get_stats(&stats);
    assert_int_equal(stats.frames_received, 10);
    assert_int_equal(stats.frames_sent, 10);
    assert_int_equal(stats.frames_dropped, 0);

    frame_mgr_deinit();
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Initialization tests */
        cmocka_unit_test(test_frame_mgr_init),
        cmocka_unit_test(test_frame_mgr_deinit),
        cmocka_unit_test(test_frame_mgr_init_null_config),

        /* Buffer state transition tests */
        cmocka_unit_test(test_frame_mgr_free_to_filling),
        cmocka_unit_test(test_frame_mgr_filling_to_ready),
        cmocka_unit_test(test_frame_mgr_ready_to_sending),
        cmocka_unit_test(test_frame_mgr_sending_to_free),

        /* Oldest-drop policy tests */
        cmocka_unit_test(test_frame_mgr_oldest_drop),
        cmocka_unit_test(test_frame_mgr_drop_counter),

        /* Statistics tests */
        cmocka_unit_test(test_frame_mgr_frames_received_counter),
        cmocka_unit_test(test_frame_mgr_frames_sent_counter),
        cmocka_unit_test(test_frame_mgr_overrun_counter),

        /* Error handling tests */
        cmocka_unit_test(test_frame_mgr_invalid_frame_number),
        cmocka_unit_test(test_frame_mgr_commit_invalid_state),
        cmocka_unit_test(test_frame_mgr_no_ready_buffers),

        /* State string tests */
        cmocka_unit_test(test_frame_mgr_state_to_string),

        /* Producer-consumer coordination tests */
        cmocka_unit_test(test_frame_mgr_producer_consumer_no_loss),
    };

    return cmocka_run_group_tests_name("FW-UT-06: Frame Manager Tests",
                                       tests, NULL, NULL);
}
