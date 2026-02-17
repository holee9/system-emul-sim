/**
 * @file csi2_rx.h
 * @brief CSI-2 RX HAL for V4L2 interface
 *
 * REQ-FW-010~013: V4L2 CSI-2 receiver interface for frame capture.
 * Uses Linux V4L2 API for MIPI CSI-2 RAW16 pixel format.
 */

#ifndef DETECTOR_HAL_CSI2_RX_H
#define DETECTOR_HAL_CSI2_RX_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief V4L2 pixel format
 */
typedef enum {
    CSI2_PIX_FMT_RAW16 = 0,   /**< RAW16 (V4L2_PIX_FMT_Y16) */
    CSI2_PIX_FMT_RAW14,       /**< RAW14 */
    CSI2_PIX_FMT_RAW12,       /**< RAW12 */
    CSI2_PIX_FMT_RGB24,       /**< RGB24 */
} csi2_pixel_format_t;

/**
 * @brief Frame buffer metadata
 */
typedef struct {
    void *data;               /**< Pointer to frame data */
    size_t length;            /**< Buffer length in bytes */
    size_t bytesused;         /**< Bytes used in buffer */
    uint32_t sequence;        /**< Frame sequence number */
    uint64_t timestamp;       /**< Timestamp in nanoseconds */
    uint32_t width;           /**< Frame width */
    uint32_t height;          /**< Frame height */
    uint32_t pixel_format;    /**< Pixel format (fourcc) */
} csi2_frame_buffer_t;

/**
 * @brief CSI-2 RX configuration
 */
typedef struct {
    const char *device;       /**< V4L2 device path (e.g., "/dev/video0") */
    uint32_t width;           /**< Frame width */
    uint32_t height;          /**< Frame height */
    csi2_pixel_format_t format; /**< Pixel format */
    uint32_t buffer_count;    /**< Number of DMA buffers (recommended: 4) */
    uint32_t fps;             /**< Frames per second (for timing validation) */
} csi2_config_t;

/**
 * @brief CSI-2 RX result codes
 */
typedef enum {
    CSI2_OK = 0,              /**< Success */
    CSI2_ERROR_NULL = -1,     /**< NULL pointer argument */
    CSI2_ERROR_OPEN = -2,     /**< Failed to open device */
    CSI2_ERROR_IOCTL = -3,    /**< IOCTL failed */
    CSI2_ERROR_STREAM = -4,   /**< Stream operation failed */
    CSI2_ERROR_BUFFER = -5,   /**< Buffer operation failed */
    CSI2_ERROR_FORMAT = -6,   /**< Unsupported format */
    CSI2_ERROR_CLOSED = -7,   /**< Device not open */
    CSI2_ERROR_TIMEOUT = -8,  /**< Frame timeout */
    CSI2_ERROR_OVERFLOW = -9, /**< Buffer overflow */
} csi2_status_t;

/**
 * @brief CSI-2 RX handle (opaque)
 */
typedef struct csi2_rx csi2_rx_t;

/* Default configuration */
#define CSI2_DEFAULT_DEVICE     "/dev/video0"
#define CSI2_DEFAULT_WIDTH      2048
#define CSI2_DEFAULT_HEIGHT     2048
#define CSI2_DEFAULT_FORMAT     CSI2_PIX_FMT_RAW16
#define CSI2_DEFAULT_BUFFERS    4
#define CSI2_DEFAULT_FPS        15
#define CSI2_FRAME_TIMEOUT_MS   1000  /**< Max wait time for one frame */

/**
 * @brief Create and initialize CSI-2 RX
 *
 * @param config CSI-2 configuration parameters
 * @return Pointer to CSI-2 RX handle, or NULL on error
 *
 * Initializes V4L2 device, configures format, allocates DMA buffers.
 * Per REQ-FW-010: Configures for RAW16 pixel format at configured resolution.
 * Per REQ-FW-011: Uses MMAP DMA buffers for zero-copy.
 */
csi2_rx_t *csi2_rx_create(const csi2_config_t *config);

/**
 * @brief Destroy and cleanup CSI-2 RX
 *
 * @param csi2 CSI-2 RX handle (can be NULL)
 *
 * Stops streaming, frees buffers, closes device.
 */
void csi2_rx_destroy(csi2_rx_t *csi2);

/**
 * @brief Start streaming
 *
 * @param csi2 CSI-2 RX handle
 * @return CSI2_OK on success, error code on failure
 *
 * Enables V4L2 streaming pipeline.
 * Per REQ-FW-013: ISP bypassed for raw pixel pass-through.
 */
csi2_status_t csi2_rx_start(csi2_rx_t *csi2);

/**
 * @brief Stop streaming
 *
 * @param csi2 CSI-2 RX handle
 * @return CSI2_OK on success, error code on failure
 *
 * Disables V4L2 streaming pipeline.
 */
csi2_status_t csi2_rx_stop(csi2_rx_t *csi2);

/**
 * @brief Capture a frame (blocking)
 *
 * @param csi2 CSI-2 RX handle
 * @param frame Pointer to store frame buffer metadata
 * @param timeout_ms Timeout in milliseconds (0 = blocking, -1 = non-blocking try)
 * @return CSI2_OK on success, error code on failure
 *
 * Per REQ-FW-012: Delivers frame within 1 ms of receipt.
 * Uses DQBUF to dequeue filled buffer from V4L2.
 */
csi2_status_t csi2_rx_capture(csi2_rx_t *csi2, csi2_frame_buffer_t *frame, int timeout_ms);

/**
 * @brief Release a captured frame
 *
 * @param csi2 CSI-2 RX handle
 * @param frame Frame buffer to release
 * @return CSI2_OK on success, error code on failure
 *
 * Requeues buffer back to V4L2 driver using QBUF.
 */
csi2_status_t csi2_rx_release(csi2_rx_t *csi2, const csi2_frame_buffer_t *frame);

/**
 * @brief Restart streaming pipeline (error recovery)
 *
 * @param csi2 CSI-2 RX handle
 * @return CSI2_OK on success, error code on failure
 *
 * Per REQ-FW-061: Restarts V4L2 streaming pipeline on error.
 * Closes device, re-initializes, resumes streaming.
 * Pipeline restart should complete within 5 seconds.
 */
csi2_status_t csi2_rx_restart(csi2_rx_t *csi2);

/**
 * @brief Get last error message
 *
 * @param csi2 CSI-2 RX handle
 * @return Error message string, or NULL if no error
 */
const char *csi2_get_error(csi2_rx_t *csi2);

/**
 * @brief Get statistics about CSI-2 RX operations
 *
 * @param csi2 CSI-2 RX handle
 * @param frames_received Pointer to store total frames received
 * @param frames_dropped Pointer to store frames dropped
 * @param errors Pointer to store error count
 * @return CSI2_OK on success, error code on failure
 */
csi2_status_t csi2_get_stats(csi2_rx_t *csi2,
                            uint32_t *frames_received,
                            uint32_t *frames_dropped,
                            uint32_t *errors);

/**
 * @brief Get current format information
 *
 * @param csi2 CSI-2 RX handle
 * @param width Pointer to store width
 * @param height Pointer to store height
 * @param format Pointer to store pixel format
 * @return CSI2_OK on success, error code on failure
 */
csi2_status_t csi2_get_format(csi2_rx_t *csi2,
                             uint32_t *width,
                             uint32_t *height,
                             csi2_pixel_format_t *format);

/**
 * @brief Check if streaming is active
 *
 * @param csi2 CSI-2 RX handle
 * @return true if streaming, false otherwise
 */
bool csi2_is_streaming(csi2_rx_t *csi2);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_HAL_CSI2_RX_H */
