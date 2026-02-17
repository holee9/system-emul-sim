/**
 * @file mock_v4l2.h
 * @brief Mock V4L2 ioctl for testing CSI-2 RX HAL
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_TEST_MOCK_MOCK_V4L2_H
#define DETECTOR_TEST_MOCK_MOCK_V4L2_H

#include <stdint.h>
#include <stdbool.h>
#include <linux/videodev2.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Mock V4L2 device state
 */
typedef struct {
    int open_count;              /**< Number of times device opened */
    int close_count;             /**< Number of times device closed */
    int ioctl_count;             /**< Number of ioctl calls */

    /* Device state */
    bool is_open;                /**< Device is open */
    bool is_streaming;           /**< Streaming is active */

    /* Format state */
    struct v4l2_format current_format; /**< Current format */

    /* Buffer state */
    uint32_t buffer_count;       /**< Number of buffers */
    void **buffers;              /**< Buffer pointers */
    size_t *buffer_lengths;      /**< Buffer lengths */
    bool *buffer_queued;         /**< Buffer queued state */

    /* Capture state */
    uint32_t frame_sequence;     /**< Current frame sequence */
    bool frame_ready;            /**< Frame ready flag */
    uint32_t filled_buffer_index; /**< Index of filled buffer */

    /* Error injection */
    int fail_next_ioctl;         /**< Fail next ioctl with this error */
    bool fail_open;              /**< Fail open() */
    bool fail_mmap;              /**< Fail mmap() */

    /* Statistics */
    uint32_t qbuf_count;         /**< QBUF calls */
    uint32_t dqbuf_count;        /**< DQBUF calls */
    uint32_t streamon_count;     /**< STREAMON calls */
    uint32_t streamoff_count;    /**< STREAMOFF calls */
} mock_v4l2_t;

/**
 * @brief Initialize mock V4L2 device
 */
void mock_v4l2_init(mock_v4l2_t *mock);

/**
 * @brief Cleanup mock V4L2 device
 */
void mock_v4l2_cleanup(mock_v4l2_t *mock);

/**
 * @brief Reset mock state (keep device open)
 */
void mock_v4l2_reset(mock_v4l2_t *mock);

/**
 * @brief Set next frame ready for capture
 */
void mock_v4l2_set_frame_ready(mock_v4l2_t *mock, uint32_t buffer_index, size_t bytesused);

/**
 * @brief Get global mock instance
 */
mock_v4l2_t *mock_v4l2_get_instance(void);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_TEST_MOCK_MOCK_V4L2_H */
