/**
 * @file sequence_engine.c
 * @brief Sequence Engine for scan control FSM
 *
 * REQ-FW-030~033: State machine for scan control.
 * - 7-state FSM (IDLE, CONFIGURE, CONFIGURE, ARM, SCANNING, STREAMING, COMPLETE, ERROR)
 * - StartScan sequence (configure, arm, scan, stream)
 * - Error recovery with 3 retry limit
 * - 3 modes (Single, Continuous, Calibration)
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "sequence_engine.h"
#include <errno.h>
#include <string.h>

/* Maximum retry count for error recovery */
#define MAX_RETRY_COUNT 3

/* Sequence Engine context */
static struct {
    seq_state_t state;
    scan_mode_t mode;
    uint32_t retry_count;
    seq_stats_t stats;
    bool initialized;
} seq_ctx = {
    .state = SEQ_STATE_IDLE,
    .mode = SCAN_MODE_SINGLE,
    .retry_count = 0,
    .stats = {0},
    .initialized = false
};

/* State transition table */
typedef struct {
    seq_state_t current_state;
    seq_event_t event;
    seq_state_t next_state;
} state_transition_t;

/* Forward declarations */
static int transition_to(seq_state_t new_state);
static int handle_configure_state(void);
static int handle_arm_state(void);
static int handle_scanning_state(void);
static int handle_streaming_state(void);
static int handle_complete_state(void);
static int handle_error_state(void);

/* ==========================================================================
 * State Transition Logic
 * ========================================================================== */

/**
 * @brief Transition to new state
 */
static int transition_to(seq_state_t new_state) {
    if (new_state >= SEQ_STATE_MAX) {
        return -EINVAL;
    }

    seq_ctx.state = new_state;
    return 0;
}

/**
 * @brief Handle CONFIGURE state
 *
 * Configure FPGA registers for current scan mode
 */
static int handle_configure_state(void) {
    /* TODO: Write FPGA configuration registers via SPI */
    /* For now, just transition to ARM state */
    return transition_to(SEQ_STATE_ARM);
}

/**
 * @brief Handle ARM state
 *
 * Arm FPGA for scanning
 */
static int handle_arm_state(void) {
    /* TODO: Write FPGA ARM register via SPI */
    /* For now, just transition to SCANNING state */
    return transition_to(SEQ_STATE_SCANNING);
}

/**
 * @brief Handle SCANNING state
 *
 * Wait for frame from CSI-2 RX
 */
static int handle_scanning_state(void) {
    /* Frame ready event will trigger transition to STREAMING */
    return 0;
}

/**
 * @brief Handle STREAMING state
 *
 * Transmit frame via Ethernet
 */
static int handle_streaming_state(void) {
    /* Frame transmission complete will trigger COMPLETE event */
    return 0;
}

/**
 * @brief Handle COMPLETE state
 *
 * Frame transmission complete
 */
static int handle_complete_state(void) {
    if (seq_ctx.mode == SCAN_MODE_SINGLE) {
        /* Single scan mode: stay in COMPLETE state */
        return 0;
    } else if (seq_ctx.mode == SCAN_MODE_CONTINUOUS) {
        /* Continuous mode: return to SCANNING for next frame */
        seq_ctx.stats.frames_sent++;
        return transition_to(SEQ_STATE_SCANNING);
    } else {
        /* Calibration mode: return to ARM for next calibration cycle */
        seq_ctx.stats.frames_sent++;
        return transition_to(SEQ_STATE_ARM);
    }
}

/**
 * @brief Handle ERROR state
 *
 * Error recovery with retry logic
 */
static int handle_error_state(void) {
    if (seq_ctx.retry_count >= MAX_RETRY_COUNT) {
        /* Max retries exceeded, stay in ERROR state */
        return -ETIMEDOUT;
    }

    /* Attempt recovery */
    seq_ctx.retry_count++;
    seq_ctx.stats.retries++;

    /* TODO: Reset FPGA and retry */
    return transition_to(SEQ_STATE_SCANNING);
}

/* ==========================================================================
 * Public API
 * ========================================================================== */

/**
 * @brief Initialize Sequence Engine
 */
int seq_init(void) {
    memset(&seq_ctx, 0, sizeof(seq_ctx));
    seq_ctx.state = SEQ_STATE_IDLE;
    seq_ctx.mode = SCAN_MODE_SINGLE;
    seq_ctx.initialized = true;
    return 0;
}

/**
 * @brief Deinitialize Sequence Engine
 */
void seq_deinit(void) {
    memset(&seq_ctx, 0, sizeof(seq_ctx));
    seq_ctx.state = SEQ_STATE_IDLE;
    seq_ctx.initialized = false;
}

/**
 * @brief Get current state
 */
seq_state_t seq_get_state(void) {
    return seq_ctx.state;
}

/**
 * @brief Convert state to string
 */
const char *seq_state_to_string(seq_state_t state) {
    switch (state) {
        case SEQ_STATE_IDLE:        return "IDLE";
        case SEQ_STATE_CONFIGURE:   return "CONFIGURE";
        case SEQ_STATE_ARM:         return "ARM";
        case SEQ_STATE_SCANNING:    return "SCANNING";
        case SEQ_STATE_STREAMING:   return "STREAMING";
        case SEQ_STATE_COMPLETE:    return "COMPLETE";
        case SEQ_STATE_ERROR:       return "ERROR";
        default:                    return "UNKNOWN";
    }
}

/**
 * @brief Start scan in specified mode
 */
int seq_start_scan(scan_mode_t mode) {
    if (!seq_ctx.initialized) {
        return -EINVAL;
    }

    if (mode >= SCAN_MODE_MAX) {
        return -EINVAL;
    }

    if (seq_ctx.state != SEQ_STATE_IDLE && seq_ctx.state != SEQ_STATE_COMPLETE) {
        /* Already scanning or in error state */
        return -EBUSY;
    }

    seq_ctx.mode = mode;
    seq_ctx.retry_count = 0;

    /* Transition to CONFIGURE state */
    return transition_to(SEQ_STATE_CONFIGURE);
}

/**
 * @brief Stop scan
 */
int seq_stop_scan(void) {
    if (!seq_ctx.initialized) {
        return -EINVAL;
    }

    /* TODO: Send STOP command to FPGA via SPI */

    /* Return to IDLE state */
    return transition_to(SEQ_STATE_IDLE);
}

/**
 * @brief Handle event
 */
int seq_handle_event(seq_event_t event, void *data) {
    (void)data;  /* Unused in this implementation */

    if (!seq_ctx.initialized) {
        return -EINVAL;
    }

    int result = 0;

    switch (seq_ctx.state) {
        case SEQ_STATE_IDLE:
            if (event == EVT_START_SCAN) {
                result = transition_to(SEQ_STATE_CONFIGURE);
            }
            break;

        case SEQ_STATE_CONFIGURE:
            if (event == EVT_CONFIG_DONE) {
                result = handle_configure_state();
            } else if (event == EVT_ERROR) {
                result = transition_to(SEQ_STATE_ERROR);
            } else if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        case SEQ_STATE_ARM:
            if (event == EVT_ARM_DONE) {
                result = handle_arm_state();
            } else if (event == EVT_ERROR) {
                result = transition_to(SEQ_STATE_ERROR);
            } else if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        case SEQ_STATE_SCANNING:
            if (event == EVT_FRAME_READY) {
                seq_ctx.stats.frames_received++;
                result = transition_to(SEQ_STATE_STREAMING);
            } else if (event == EVT_ERROR) {
                result = transition_to(SEQ_STATE_ERROR);
            } else if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        case SEQ_STATE_STREAMING:
            if (event == EVT_COMPLETE) {
                result = handle_streaming_state();
                if (result == 0) {
                    result = handle_complete_state();
                }
            } else if (event == EVT_ERROR) {
                result = transition_to(SEQ_STATE_ERROR);
            } else if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        case SEQ_STATE_COMPLETE:
            if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        case SEQ_STATE_ERROR:
            if (event == EVT_ERROR_CLEARED) {
                result = handle_error_state();
            } else if (event == EVT_STOP_SCAN) {
                result = transition_to(SEQ_STATE_IDLE);
            }
            break;

        default:
            result = -EINVAL;
            break;
    }

    return result;
}

/**
 * @brief Get statistics
 */
int seq_get_stats(seq_stats_t *stats) {
    if (!seq_ctx.initialized || stats == NULL) {
        return -EINVAL;
    }

    memcpy(stats, &seq_ctx.stats, sizeof(seq_stats_t));
    return 0;
}

/**
 * @brief Get retry count
 */
uint32_t seq_get_retry_count(void) {
    return seq_ctx.retry_count;
}

/**
 * @brief Reset retry count
 */
void seq_reset_retry_count(void) {
    seq_ctx.retry_count = 0;
}
