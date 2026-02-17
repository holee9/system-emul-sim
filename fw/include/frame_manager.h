/**
 * @file frame_manager.h
 * @brief Frame Manager for 4-buffer ring management
 *
 * REQ-FW-050~052: 4-buffer ring with oldest-drop policy.
 * REQ-FW-111: Runtime statistics.
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_FRAME_MANAGER_H
#define DETECTOR_FRAME_MANAGER_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Buffer state enumeration
 */
typedef enum {
    BUF_STATE_FREE = 0,      /**< Available for CSI-2 RX */
    BUF_STATE_FILLING,       /**< Being filled by DMA */
    BUF_STATE_READY,         /**< Ready for TX */
    BUF_STATE_SENDING        /**< Being transmitted */
} buf_state_t;

/**
 * @brief Frame buffer descriptor
 */
typedef struct {
    uint8_t *data;           /**< Buffer data pointer */
    size_t size;             /**< Buffer size in bytes */
    buf_state_t state;       /**< Current buffer state */
    uint32_t frame_number;   /**< Frame sequence number */
    uint16_t total_packets;  /**< Total packets for transmission */
    uint16_t sent_packets;   /**< Packets already sent */
} frame_buffer_t;

/**
 * @brief Frame Manager configuration
 */
typedef struct {
    uint16_t rows;           /**< Frame rows (height) */
    uint16_t cols;           /**< Frame columns (width) */
    uint8_t bit_depth;       /**< Bits per pixel */
    size_t frame_size;       /**< Total frame size in bytes */
    uint32_t num_buffers;    /**< Number of buffers (fixed at 4) */
} frame_mgr_config_t;

/**
 * @brief Frame Manager statistics
 */
typedef struct {
    uint64_t frames_received;  /**< Total frames received */
    uint64_t frames_sent;      /**< Total frames sent */
    uint64_t frames_dropped;   /**< Total frames dropped (oldest-drop) */
    uint64_t packets_sent;     /**< Total packets sent */
    uint64_t bytes_sent;       /**< Total bytes sent */
    uint64_t overruns;         /**< Buffer overrun count */
} frame_stats_t;

/* Default configuration */
#define FRAME_MGR_DEFAULT_ROWS      2048
#define FRAME_MGR_DEFAULT_COLS      2048
#define FRAME_MGR_DEFAULT_BIT_DEPTH 16
#define FRAME_MGR_DEFAULT_BUFFERS   4

/**
 * @brief Initialize Frame Manager
 *
 * @param config Frame Manager configuration
 * @return 0 on success, -EINVAL on NULL config, -ENOMEM on allocation failure
 *
 * REQ-FW-050: Allocate 4 frame buffers.
 * All buffers start in FREE state.
 */
int frame_mgr_init(const frame_mgr_config_t *config);

/**
 * @brief Deinitialize Frame Manager
 *
 * Frees all buffers and resets state.
 */
void frame_mgr_deinit(void);

/**
 * @brief Acquire buffer for CSI-2 RX (Producer)
 *
 * @param frame_number Frame sequence number
 * @param buf Pointer to store buffer address
 * @param size Pointer to store buffer size
 * @return 0 on success, -EINVAL on invalid frame number, -EBUSY if all buffers busy
 *
 * Transitions buffer from FREE to FILLING state.
 * Implements oldest-drop policy (REQ-FW-051).
 */
int frame_mgr_get_buffer(uint32_t frame_number, uint8_t **buf, size_t *size);

/**
 * @brief Commit filled buffer (Producer)
 *
 * @param frame_number Frame sequence number
 * @return 0 on success, -EINVAL on invalid frame number or state
 *
 * Transitions buffer from FILLING to READY state.
 * Increments frames_received counter.
 */
int frame_mgr_commit_buffer(uint32_t frame_number);

/**
 * @brief Acquire ready buffer for TX (Consumer)
 *
 * @param buf Pointer to store buffer address
 * @param size Pointer to store buffer size
 * @param frame_number Pointer to store frame number
 * @return 0 on success, -ENOENT if no ready buffers
 *
 * Finds oldest READY buffer.
 * Transitions buffer from READY to SENDING state.
 */
int frame_mgr_get_ready_buffer(uint8_t **buf, size_t *size, uint32_t *frame_number);

/**
 * @brief Release transmitted buffer (Consumer)
 *
 * @param frame_number Frame sequence number
 * @return 0 on success, -EINVAL on invalid frame number or state
 *
 * Transitions buffer from SENDING to FREE state.
 * Increments frames_sent counter.
 */
int frame_mgr_release_buffer(uint32_t frame_number);

/**
 * @brief Get Frame Manager statistics
 *
 * @param stats Pointer to store statistics
 *
 * REQ-FW-111: Runtime statistics.
 */
void frame_mgr_get_stats(frame_stats_t *stats);

/**
 * @brief Get buffer state (for testing)
 *
 * @param frame_number Frame sequence number
 * @return Current buffer state
 */
buf_state_t frame_mgr_get_buffer_state(uint32_t frame_number);

/**
 * @brief Convert buffer state to string
 *
 * @param state Buffer state
 * @return String representation
 */
const char *frame_mgr_state_to_string(buf_state_t state);

/**
 * @brief Check if Frame Manager is initialized
 *
 * @return true if initialized, false otherwise
 */
bool frame_mgr_is_initialized(void);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_FRAME_MANAGER_H */
