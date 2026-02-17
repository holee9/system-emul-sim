/**
 * @file mock_v4l2.c
 * @brief Mock V4L2 ioctl implementation
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "mock_v4l2.h"
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <fcntl.h>

/* Global mock instance */
static mock_v4l2_t g_mock_v4l2 = {0};

void mock_v4l2_init(mock_v4l2_t *mock) {
    if (mock == NULL) {
        mock = &g_mock_v4l2;
    }

    memset(mock, 0, sizeof(mock_v4l2_t));
    mock->open_count = 0;
    mock->close_count = 0;
    mock->ioctl_count = 0;
    mock->is_open = false;
    mock->is_streaming = false;
    mock->buffer_count = 0;
    mock->buffers = NULL;
    mock->buffer_lengths = NULL;
    mock->buffer_queued = NULL;
    mock->frame_sequence = 0;
    mock->frame_ready = false;
    mock->filled_buffer_index = 0;
    mock->fail_next_ioctl = 0;
    mock->fail_open = false;
    mock->fail_mmap = false;

    /* Set default format */
    mock->current_format.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    mock->current_format.fmt.pix.width = 2048;
    mock->current_format.fmt.pix.height = 2048;
    mock->current_format.fmt.pix.pixelformat = V4L2_PIX_FMT_Y16;
    mock->current_format.fmt.pix.field = V4L2_FIELD_NONE;
    mock->current_format.fmt.pix.bytesperline = 4096;
    mock->current_format.fmt.pix.sizeimage = 2048 * 2048 * 2;
}

void mock_v4l2_cleanup(mock_v4l2_t *mock) {
    if (mock == NULL) {
        mock = &g_mock_v4l2;
    }

    if (mock->buffers != NULL) {
        for (uint32_t i = 0; i < mock->buffer_count; i++) {
            if (mock->buffers[i] != NULL) {
                free(mock->buffers[i]);
            }
        }
        free(mock->buffers);
        mock->buffers = NULL;
    }

    if (mock->buffer_lengths != NULL) {
        free(mock->buffer_lengths);
        mock->buffer_lengths = NULL;
    }

    if (mock->buffer_queued != NULL) {
        free(mock->buffer_queued);
        mock->buffer_queued = NULL;
    }
}

void mock_v4l2_reset(mock_v4l2_t *mock) {
    if (mock == NULL) {
        mock = &g_mock_v4l2;
    }

    mock->ioctl_count = 0;
    mock->is_streaming = false;
    mock->frame_sequence = 0;
    mock->frame_ready = false;
    mock->fail_next_ioctl = 0;

    /* Reset queued state */
    if (mock->buffer_queued != NULL) {
        memset(mock->buffer_queued, 0, mock->buffer_count * sizeof(bool));
    }
}

void mock_v4l2_set_frame_ready(mock_v4l2_t *mock, uint32_t buffer_index, size_t bytesused) {
    if (mock == NULL) {
        mock = &g_mock_v4l2;
    }

    mock->frame_ready = true;
    mock->filled_buffer_index = buffer_index;
    mock->current_format.fmt.pix.bytesused = bytesused;
}

mock_v4l2_t *mock_v4l2_get_instance(void) {
    return &g_mock_v4l2;
}

/* ==========================================================================
 * Mock V4L2 API Functions
 * These intercept V4L2 calls in test code
 * ========================================================================== */

int mock_v4l2_open(const char *pathname, int flags) {
    mock_v4l2_t *mock = &g_mock_v4l2;

    mock->open_count++;

    if (mock->fail_open) {
        return -1;
    }

    mock->is_open = true;
    return 42; /* Mock file descriptor */
}

int mock_v4l2_close(int fd) {
    mock_v4l2_t *mock = &g_mock_v4l2;

    if (fd == 42) {
        mock->close_count++;
        mock->is_open = false;
        mock->is_streaming = false;
        return 0;
    }

    return -1;
}

int mock_v4l2_ioctl(int fd, unsigned long request, void *arg) {
    mock_v4l2_t *mock = &g_mock_v4l2;

    mock->ioctl_count++;

    if (fd != 42) {
        errno = EBADF;
        return -1;
    }

    if (mock->fail_next_ioctl != 0) {
        errno = mock->fail_next_ioctl;
        mock->fail_next_ioctl = 0;
        return -1;
    }

    switch (request) {
        case VIDIOC_S_FMT: {
            struct v4l2_format *fmt = (struct v4l2_format *)arg;
            if (fmt->type == V4L2_BUF_TYPE_VIDEO_CAPTURE) {
                /* Accept requested format */
                mock->current_format = *fmt;
                return 0;
            }
            errno = EINVAL;
            return -1;
        }

        case VIDIOC_G_FMT: {
            struct v4l2_format *fmt = (struct v4l2_format *)arg;
            if (fmt->type == V4L2_BUF_TYPE_VIDEO_CAPTURE) {
                *fmt = mock->current_format;
                return 0;
            }
            errno = EINVAL;
            return -1;
        }

        case VIDIOC_REQBUFS: {
            struct v4l2_requestbuffers *req = (struct v4l2_requestbuffers *)arg;
            if (req->count == 0) {
                /* Free buffers */
                mock_v4l2_cleanup(mock);
                return 0;
            }

            /* Allocate buffers */
            mock->buffer_count = req->count;
            mock->buffers = (void **)calloc(req->count, sizeof(void *));
            mock->buffer_lengths = (size_t *)calloc(req->count, sizeof(size_t));
            mock->buffer_queued = (bool *)calloc(req->count, sizeof(bool));

            for (uint32_t i = 0; i < req->count; i++) {
                size_t buf_size = mock->current_format.fmt.pix.sizeimage;
                mock->buffers[i] = calloc(1, buf_size);
                mock->buffer_lengths[i] = buf_size;
                mock->buffer_queued[i] = false;
            }

            return 0;
        }

        case VIDIOC_QUERYBUF: {
            struct v4l2_buffer *buf = (struct v4l2_buffer *)arg;
            if (buf->index >= mock->buffer_count) {
                errno = EINVAL;
                return -1;
            }

            buf->length = mock->buffer_lengths[buf->index];
            buf->m.offset = buf->index * mock->buffer_lengths[buf->index]; /* Fake offset */
            return 0;
        }

        case VIDIOC_QBUF: {
            struct v4l2_buffer *buf = (struct v4l2_buffer *)arg;
            if (buf->index >= mock->buffer_count) {
                errno = EINVAL;
                return -1;
            }

            mock->buffer_queued[buf->index] = true;
            mock->qbuf_count++;
            return 0;
        }

        case VIDIOC_DQBUF: {
            struct v4l2_buffer *buf = (struct v4l2_buffer *)arg;

            if (!mock->frame_ready) {
                errno = EAGAIN;
                return -1;
            }

            buf->index = mock->filled_buffer_index;
            buf->bytesused = mock->current_format.fmt.pix.bytesused;
            buf->sequence = mock->frame_sequence++;
            buf->flags = 0;
            buf->field = V4L2_FIELD_NONE;

            /* Fake timestamp */
            struct timeval tv;
            gettimeofday(&tv, NULL);
            buf->timestamp = tv;

            mock->buffer_queued[buf->index] = false;
            mock->frame_ready = false;
            mock->dqbuf_count++;

            return 0;
        }

        case VIDIOC_STREAMON: {
            enum v4l2_buf_type *type = (enum v4l2_buf_type *)arg;
            if (*type == V4L2_BUF_TYPE_VIDEO_CAPTURE) {
                mock->is_streaming = true;
                mock->streamon_count++;
                return 0;
            }
            errno = EINVAL;
            return -1;
        }

        case VIDIOC_STREAMOFF: {
            enum v4l2_buf_type *type = (enum v4l2_buf_type *)arg;
            if (*type == V4L2_BUF_TYPE_VIDEO_CAPTURE) {
                mock->is_streaming = false;
                mock->streamoff_count++;
                return 0;
            }
            errno = EINVAL;
            return -1;
        }

        default:
            errno = ENOTTY;
            return -1;
    }
}

void *mock_v4l2_mmap(void *addr, size_t length, int prot, int flags,
                     int fd, off_t offset) {
    mock_v4l2_t *mock = &g_mock_v4l2;

    if (fd != 42) {
        return MAP_FAILED;
    }

    if (mock->fail_mmap) {
        return MAP_FAILED;
    }

    /* Calculate buffer index from offset */
    uint32_t index = offset / mock->current_format.fmt.pix.sizeimage;

    if (index >= mock->buffer_count) {
        return MAP_FAILED;
    }

    return mock->buffers[index];
}

int mock_v4l2_munmap(void *addr, size_t length) {
    mock_v4l2_t *mock = &g_mock_v4l2;

    /* Find and free buffer */
    for (uint32_t i = 0; i < mock->buffer_count; i++) {
        if (mock->buffers[i] == addr) {
            /* Don't actually free, just mark as unmapped */
            return 0;
        }
    }

    return -1;
}
