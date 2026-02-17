/**
 * @file sequence_engine.h
 * @brief Sequence Engine for scan control FSM
 *
 * REQ-FW-030~033: State machine for scan control.
 * - 7-state FSM (IDLE, CONFIGURE, ARM, SCANNING, STREAMING, COMPLETE, ERROR)
 * - StartScan sequence (configure, arm, scan, stream)
 * - Error recovery with 3 retry limit
 * - 3 modes (Single, Continuous, Calibration)
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_SEQUENCE_ENGINE_H
#define DETECTOR_SEQUENCE_ENGINE_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Sequence Engine states
 */
typedef enum {
    SEQ_STATE_IDLE = 0,
    SEQ_STATE_CONFIGURE,
    SEQ_STATE_ARM,
    SEQ_STATE_SCANNING,
    SEQ_STATE_STREAMING,
    SEQ_STATE_COMPLETE,
    SEQ_STATE_ERROR,
    SEQ_STATE_MAX
} seq_state_t;

/**
 * @brief Scan modes
 */
typedef enum {
    SCAN_MODE_SINGLE = 0,
    SCAN_MODE_CONTINUOUS,
    SCAN_MODE_CALIBRATION,
    SCAN_MODE_MAX
} scan_mode_t;

/**
 * @brief Sequence Engine events
 */
typedef enum {
    EVT_START_SCAN = 0,
    EVT_CONFIG_DONE,
    EVT_ARM_DONE,
    EVT_FRAME_READY,
    EVT_STOP_SCAN,
    EVT_ERROR,
    EVT_ERROR_CLEARED,
    EVT_COMPLETE,
    EVT_MAX
} seq_event_t;

/**
 * @brief FPGA Status Register bits
 */
#define FPGA_STATUS_BUSY    (1U << 0)
#define FPGA_STATUS_ERROR   (1U << 1)
#define FPGA_STATUS_READY   (1U << 2)

/**
 * @brief FPGA Control Register bits
 */
#define FPGA_CTRL_START     (1U << 0)
#define FPGA_CTRL_STOP      (1U << 1)
#define FPGA_CTRL_MODE_MASK (0x3U << 2)
#define FPGA_CTRL_MODE_SINGLE   (0x0U << 2)
#define FPGA_CTRL_MODE_CONTINUOUS (0x1U << 2)
#define FPGA_CTRL_MODE_CALIBRATION (0x2U << 2)

/**
 * @brief Sequence Engine statistics
 */
typedef struct {
    uint32_t frames_received;
    uint32_t frames_sent;
    uint32_t errors;
    uint32_t retries;
} seq_stats_t;

/**
 * @brief Initialize Sequence Engine
 *
 * @return 0 on success, -errno on failure
 */
int seq_init(void);

/**
 * @brief Deinitialize Sequence Engine
 */
void seq_deinit(void);

/**
 * @brief Get current state
 *
 * @return Current state
 */
seq_state_t seq_get_state(void);

/**
 * @brief Convert state to string
 *
 * @param state State to convert
 * @return String representation
 */
const char *seq_state_to_string(seq_state_t state);

/**
 * @brief Start scan in specified mode
 *
 * @param mode Scan mode
 * @return 0 on success, -errno on failure
 */
int seq_start_scan(scan_mode_t mode);

/**
 * @brief Stop scan
 *
 * @return 0 on success, -errno on failure
 */
int seq_stop_scan(void);

/**
 * @brief Handle event
 *
 * @param event Event to handle
 * @param data Event-specific data
 * @return 0 on success, -errno on failure
 */
int seq_handle_event(seq_event_t event, void *data);

/**
 * @brief Get statistics
 *
 * @param stats Pointer to store statistics
 * @return 0 on success, -errno on failure
 */
int seq_get_stats(seq_stats_t *stats);

/**
 * @brief Get retry count
 *
 * @return Current retry count
 */
uint32_t seq_get_retry_count(void);

/**
 * @brief Reset retry count
 */
void seq_reset_retry_count(void);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_SEQUENCE_ENGINE_H */
