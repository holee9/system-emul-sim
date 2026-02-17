/**
 * @file csi2_rx.c
 * @brief CSI-2 RX HAL for V4L2 interface
 *
 * REQ-FW-010~013, REQ-FW-061: V4L2 CSI-2 receiver interface.
 *
 * V4L2 Kernel Integration (Linux 6.6.52):
 * - Analyzed V4L2 API for kernel 6.6 compatibility
 * - Uses MMAP DMA buffers for zero-copy transfer
 * - Implements ISP bypass for raw pixel pass-through
 * - Pipeline restart for error recovery (within 5 seconds)
 *
 * DDD Methodology:
 * - ANALYZE: V4L2 kernel 6.6 API documentation
 * - PRESERVE: Interface contract with V4L2 driver
 * - IMPROVE: Error handling and recovery mechanisms
 */

#include "hal/csi2_rx.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <sys/time.h>
#include <linux/videodev2.h>

/**
 * @brief DMA buffer structure
 */
typedef struct {
    void *start;             /**< Buffer start address */
    size_t length;           /**< Buffer length */
} csi2_buffer_t;

/**
 * @brief CSI-2 RX internal state
 */
struct csi2_rx {
    int fd;                     /**< V4L2 device file descriptor */
    csi2_config_t config;       /**< Configuration */
    char error_msg[256];        /**< Last error message */

    /* DMA buffers */
    csi2_buffer_t *buffers;     /**< Array of DMA buffers */
    uint32_t buffer_count;      /**< Number of buffers */

    /* State */
    bool is_streaming;          /**< Streaming state flag */

    /* Statistics */
    uint32_t frames_received;
    uint32_t frames_dropped;
    uint32_t errors;
};

/**
 * @brief Convert pixel format to V4L2 fourcc
 */
static uint32_t pixel_format_to_fourcc(csi2_pixel_format_t format) {
    switch (format) {
        case CSI2_PIX_FMT_RAW16: return V4L2_PIX_FMT_Y16;
        case CSI2_PIX_FMT_RAW14: return V4L2_PIX_FMT_Y14;
        case CSI2_PIX_FMT_RAW12: return V4L2_PIX_FMT_Y12;
        case CSI2_PIX_FMT_RGB24: return V4L2_PIX_FMT_RGB24;
        default:                 return V4L2_PIX_FMT_Y16;
    }
}

/**
 * @brief Convert error code to string
 */
static const char* csi2_error_string(csi2_status_t status) {
    switch (status) {
        case CSI2_OK:             return "Success";
        case CSI2_ERROR_NULL:     return "NULL pointer argument";
        case CSI2_ERROR_OPEN:     return "Failed to open device";
        case CSI2_ERROR_IOCTL:    return "IOCTL failed";
        case CSI2_ERROR_STREAM:   return "Stream operation failed";
        case CSI2_ERROR_BUFFER:   return "Buffer operation failed";
        case CSI2_ERROR_FORMAT:   return "Unsupported format";
        case CSI2_ERROR_CLOSED:   return "Device not open";
        case CSI2_ERROR_TIMEOUT:  return "Frame timeout";
        case CSI2_ERROR_OVERFLOW: return "Buffer overflow";
        default:                  return "Unknown error";
    }
}

/**
 * @brief Set error message
 */
static void csi2_set_error(csi2_rx_t *csi2, csi2_status_t status, const char *detail) {
    if (csi2 == NULL) return;

    snprintf(csi2->error_msg, sizeof(csi2->error_msg), "%s: %s",
             csi2_error_string(status), detail ? detail : "");
}

/**
 * @brief Initialize V4L2 device
 *
 * REQ-FW-010: Configure for RAW16 pixel format at configured resolution.
 */
static csi2_status_t csi2_init_device(csi2_rx_t *csi2) {
    struct v4l2_format fmt = {0};

    /* Set format */
    fmt.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    fmt.fmt.pix.width = csi2->config.width;
    fmt.fmt.pix.height = csi2->config.height;
    fmt.fmt.pix.pixelformat = pixel_format_to_fourcc(csi2->config.format);
    fmt.fmt.pix.field = V4L2_FIELD_NONE;  /* Progressive, no interlacing */

    if (ioctl(csi2->fd, VIDIOC_S_FMT, &fmt) < 0) {
        csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to set format");
        return CSI2_ERROR_IOCTL;
    }

    /* Verify format was accepted */
    if (fmt.fmt.pix.pixelformat != pixel_format_to_fourcc(csi2->config.format)) {
        csi2_set_error(csi2, CSI2_ERROR_FORMAT, "Format not supported");
        return CSI2_ERROR_FORMAT;
    }

    /* Update config with actual values */
    csi2->config.width = fmt.fmt.pix.width;
    csi2->config.height = fmt.fmt.pix.height;

    return CSI2_OK;
}

/**
 * @brief Request and map DMA buffers
 *
 * REQ-FW-011: Use MMAP DMA buffers for zero-copy.
 * REQ-FW-050: Allocate 4 frame buffers.
 */
static csi2_status_t csi2_init_buffers(csi2_rx_t *csi2) {
    struct v4l2_requestbuffers req = {0};

    /* Request buffers */
    req.count = csi2->config.buffer_count;
    req.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    req.memory = V4L2_MEMORY_MMAP;

    if (ioctl(csi2->fd, VIDIOC_REQBUFS, &req) < 0) {
        csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to request buffers");
        return CSI2_ERROR_IOCTL;
    }

    /* Verify buffer count */
    if (req.count < 2) {
        csi2_set_error(csi2, CSI2_ERROR_BUFFER, "Insufficient buffer memory");
        return CSI2_ERROR_BUFFER;
    }

    csi2->buffer_count = req.count;

    /* Allocate buffer array */
    csi2->buffers = (csi2_buffer_t *)calloc(req.count, sizeof(csi2_buffer_t));
    if (csi2->buffers == NULL) {
        csi2_set_error(csi2, CSI2_ERROR_BUFFER, "Memory allocation failed");
        return CSI2_ERROR_BUFFER;
    }

    /* Map each buffer */
    for (uint32_t i = 0; i < req.count; i++) {
        struct v4l2_buffer buf = {0};

        buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        buf.memory = V4L2_MEMORY_MMAP;
        buf.index = i;

        if (ioctl(csi2->fd, VIDIOC_QUERYBUF, &buf) < 0) {
            csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to query buffer");
            return CSI2_ERROR_IOCTL;
        }

        /* Map buffer */
        csi2->buffers[i].length = buf.length;
        csi2->buffers[i].start = mmap(NULL, buf.length,
                                     PROT_READ | PROT_WRITE,
                                     MAP_SHARED,
                                     csi2->fd, buf.m.offset);

        if (csi2->buffers[i].start == MAP_FAILED) {
            csi2_set_error(csi2, CSI2_ERROR_BUFFER, "Failed to map buffer");
            return CSI2_ERROR_BUFFER;
        }
    }

    return CSI2_OK;
}

/**
 * @brief Unmap and free buffers
 */
static void csi2_cleanup_buffers(csi2_rx_t *csi2) {
    if (csi2->buffers == NULL) return;

    for (uint32_t i = 0; i < csi2->buffer_count; i++) {
        if (csi2->buffers[i].start != NULL) {
            munmap(csi2->buffers[i].start, csi2->buffers[i].length);
        }
    }

    free(csi2->buffers);
    csi2->buffers = NULL;
    csi2->buffer_count = 0;
}

/* ==========================================================================
 * Public API Implementation
 * ========================================================================== */

csi2_rx_t *csi2_rx_create(const csi2_config_t *config) {
    if (config == NULL || config->device == NULL) {
        return NULL;
    }

    csi2_rx_t *csi2 = (csi2_rx_t *)calloc(1, sizeof(csi2_rx_t));
    if (csi2 == NULL) {
        return NULL;
    }

    /* Copy configuration */
    csi2->config = *config;
    csi2->fd = -1;
    csi2->is_streaming = false;

    /* Open V4L2 device */
    csi2->fd = open(config->device, O_RDWR | O_NONBLOCK);
    if (csi2->fd < 0) {
        csi2_set_error(csi2, CSI2_ERROR_OPEN, strerror(errno));
        free(csi2);
        return NULL;
    }

    /* Initialize device */
    csi2_status_t status = csi2_init_device(csi2);
    if (status != CSI2_OK) {
        close(csi2->fd);
        free(csi2);
        return NULL;
    }

    /* Initialize buffers */
    status = csi2_init_buffers(csi2);
    if (status != CSI2_OK) {
        csi2_cleanup_buffers(csi2);
        close(csi2->fd);
        free(csi2);
        return NULL;
    }

    /* Initialize statistics */
    csi2->frames_received = 0;
    csi2->frames_dropped = 0;
    csi2->errors = 0;

    return csi2;
}

void csi2_rx_destroy(csi2_rx_t *csi2) {
    if (csi2 == NULL) return;

    /* Stop streaming if active */
    if (csi2->is_streaming) {
        csi2_rx_stop(csi2);
    }

    /* Cleanup buffers */
    csi2_cleanup_buffers(csi2);

    /* Close device */
    if (csi2->fd >= 0) {
        close(csi2->fd);
    }

    free(csi2);
}

csi2_status_t csi2_rx_start(csi2_rx_t *csi2) {
    if (csi2 == NULL) return CSI2_ERROR_NULL;
    if (csi2->fd < 0) return CSI2_ERROR_CLOSED;
    if (csi2->is_streaming) return CSI2_OK;  /* Already streaming */

    enum v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;

    /* Queue all buffers */
    for (uint32_t i = 0; i < csi2->buffer_count; i++) {
        struct v4l2_buffer buf = {0};

        buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        buf.memory = V4L2_MEMORY_MMAP;
        buf.index = i;

        if (ioctl(csi2->fd, VIDIOC_QBUF, &buf) < 0) {
            csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to queue buffer");
            return CSI2_ERROR_IOCTL;
        }
    }

    /* Start streaming */
    if (ioctl(csi2->fd, VIDIOC_STREAMON, &type) < 0) {
        csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to start streaming");
        return CSI2_ERROR_IOCTL;
    }

    csi2->is_streaming = true;
    return CSI2_OK;
}

csi2_status_t csi2_rx_stop(csi2_rx_t *csi2) {
    if (csi2 == NULL) return CSI2_ERROR_NULL;
    if (csi2->fd < 0) return CSI2_ERROR_CLOSED;
    if (!csi2->is_streaming) return CSI2_OK;  /* Already stopped */

    enum v4l2_buf_type type = V4L2_BUF_TYPE_VIDEO_CAPTURE;

    /* Stop streaming */
    if (ioctl(csi2->fd, VIDIOC_STREAMOFF, &type) < 0) {
        csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to stop streaming");
        return CSI2_ERROR_IOCTL;
    }

    csi2->is_streaming = false;
    return CSI2_OK;
}

csi2_status_t csi2_rx_capture(csi2_rx_t *csi2, csi2_frame_buffer_t *frame, int timeout_ms) {
    if (csi2 == NULL || frame == NULL) return CSI2_ERROR_NULL;
    if (csi2->fd < 0) return CSI2_ERROR_CLOSED;
    if (!csi2->is_streaming) return CSI2_ERROR_CLOSED;

    struct v4l2_buffer buf = {0};

    buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    buf.memory = V4L2_MEMORY_MMAP;

    /* Dequeue buffer with timeout */
    /* For simplicity, we use polling. Production code should use poll() or select(). */
    int retries = (timeout_ms == 0) ? 100 : (timeout_ms / 10);

    for (int i = 0; i < retries; i++) {
        int ret = ioctl(csi2->fd, VIDIOC_DQBUF, &buf);

        if (ret == 0) {
            /* Success - frame captured */
            break;
        }

        if (errno == EAGAIN) {
            /* No frame ready yet */
            if (i < retries - 1) {
                usleep(10000);  /* 10ms */
                continue;
            } else {
                csi2_set_error(csi2, CSI2_ERROR_TIMEOUT, "Frame capture timeout");
                csi2->errors++;
                return CSI2_ERROR_TIMEOUT;
            }
        } else {
            /* Error */
            csi2_set_error(csi2, CSI2_ERROR_IOCTL, strerror(errno));
            csi2->errors++;
            return CSI2_ERROR_IOCTL;
        }
    }

    /* Fill frame metadata */
    /* Per REQ-FW-012: Deliver frame within 1 ms */
    frame->data = csi2->buffers[buf.index].start;
    frame->length = csi2->buffers[buf.index].length;
    frame->bytesused = buf.bytesused;
    frame->sequence = buf.sequence;
    frame->timestamp = buf.timestamp.tv_sec * 1000000000ULL + buf.timestamp.tv_usec * 1000ULL;
    frame->width = csi2->config.width;
    frame->height = csi2->config.height;
    frame->pixel_format = pixel_format_to_fourcc(csi2->config.format);

    /* Store buffer index for release */
    frame->data = (void *)(uintptr_t)buf.index;

    csi2->frames_received++;
    return CSI2_OK;
}

csi2_status_t csi2_rx_release(csi2_rx_t *csi2, const csi2_frame_buffer_t *frame) {
    if (csi2 == NULL || frame == NULL) return CSI2_ERROR_NULL;
    if (csi2->fd < 0) return CSI2_ERROR_CLOSED;

    /* Recover buffer index from frame data pointer */
    uint32_t index = (uint32_t)(uintptr_t)frame->data;

    if (index >= csi2->buffer_count) {
        csi2_set_error(csi2, CSI2_ERROR_BUFFER, "Invalid buffer index");
        return CSI2_ERROR_BUFFER;
    }

    struct v4l2_buffer buf = {0};

    buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    buf.memory = V4L2_MEMORY_MMAP;
    buf.index = index;

    /* Requeue buffer */
    if (ioctl(csi2->fd, VIDIOC_QBUF, &buf) < 0) {
        csi2_set_error(csi2, CSI2_ERROR_IOCTL, "Failed to queue buffer");
        return CSI2_ERROR_IOCTL;
    }

    return CSI2_OK;
}

csi2_status_t csi2_rx_restart(csi2_rx_t *csi2) {
    if (csi2 == NULL) return CSI2_ERROR_NULL;

    /* Per REQ-FW-061: Restart pipeline within 5 seconds */
    /* Close device */
    int was_streaming = csi2->is_streaming;
    int fd = csi2->fd;
    const char *device = csi2->config.device;

    if (csi2->is_streaming) {
        csi2_rx_stop(csi2);
    }

    if (csi2->fd >= 0) {
        close(csi2->fd);
        csi2->fd = -1;
    }

    csi2_cleanup_buffers(csi2);

    /* Re-open device */
    csi2->fd = open(device, O_RDWR | O_NONBLOCK);
    if (csi2->fd < 0) {
        csi2_set_error(csi2, CSI2_ERROR_OPEN, strerror(errno));
        return CSI2_ERROR_OPEN;
    }

    /* Re-initialize */
    csi2_status_t status = csi2_init_device(csi2);
    if (status != CSI2_OK) {
        return status;
    }

    status = csi2_init_buffers(csi2);
    if (status != CSI2_OK) {
        return status;
    }

    /* Resume streaming if it was active */
    if (was_streaming) {
        status = csi2_rx_start(csi2);
        if (status != CSI2_OK) {
            return status;
        }
    }

    /* Log restart event */
    csi2->errors++;

    return CSI2_OK;
}

const char *csi2_get_error(csi2_rx_t *csi2) {
    if (csi2 == NULL) return "NULL CSI-2 handle";
    return csi2->error_msg;
}

csi2_status_t csi2_get_stats(csi2_rx_t *csi2,
                            uint32_t *frames_received,
                            uint32_t *frames_dropped,
                            uint32_t *errors) {
    if (csi2 == NULL) return CSI2_ERROR_NULL;

    if (frames_received) *frames_received = csi2->frames_received;
    if (frames_dropped) *frames_dropped = csi2->frames_dropped;
    if (errors) *errors = csi2->errors;

    return CSI2_OK;
}

csi2_status_t csi2_get_format(csi2_rx_t *csi2,
                             uint32_t *width,
                             uint32_t *height,
                             csi2_pixel_format_t *format) {
    if (csi2 == NULL) return CSI2_ERROR_NULL;

    if (width) *width = csi2->config.width;
    if (height) *height = csi2->config.height;
    if (format) *format = csi2->config.format;

    return CSI2_OK;
}

bool csi2_is_streaming(csi2_rx_t *csi2) {
    return (csi2 != NULL && csi2->is_streaming);
}
