/**
 * @file frame_manager.c
 * @brief Frame Manager implementation
 *
 * REQ-FW-050~052: 4-buffer ring with oldest-drop policy.
 * REQ-FW-111: Runtime statistics.
 *
 * Implementation (TDD):
 * - RED: Tests already written (test_frame_manager.c)
 * - GREEN: Implementation to satisfy tests
 * - REFACTOR: Code structure optimized
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "frame_manager.h"
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <limits.h>  /* For UINT32_MAX */

/**
 * @brief Frame Manager instance
 */
typedef struct {
    frame_buffer_t *buffers;  /**< Array of buffer descriptors */
    uint32_t num_buffers;     /**< Number of buffers */
    uint32_t oldest_index;    /**< Index of oldest buffer for drop policy */
    frame_stats_t stats;      /**< Statistics */
    bool initialized;         /**< Initialization flag */
} frame_mgr_t;

/* Global instance (singleton pattern) */
static frame_mgr_t g_frame_mgr = {0};

/**
 * @brief Find buffer index by frame number
 *
 * Uses modulo arithmetic to map frame_number to buffer index.
 */
static inline uint32_t frame_number_to_index(uint32_t frame_number) {
    return frame_number % g_frame_mgr.num_buffers;
}

/* ==========================================================================
 * Public API Implementation
 * ========================================================================== */

int frame_mgr_init(const frame_mgr_config_t *config) {
    if (config == NULL) {
        return -EINVAL;
    }

    if (config->num_buffers != 4) {
        return -EINVAL;  /* Only 4 buffers supported per REQ-FW-050 */
    }

    /* Deinitialize if already initialized */
    if (g_frame_mgr.initialized) {
        frame_mgr_deinit();
    }

    /* Allocate buffer descriptors */
    g_frame_mgr.buffers = (frame_buffer_t *)calloc(config->num_buffers, sizeof(frame_buffer_t));
    if (g_frame_mgr.buffers == NULL) {
        return -ENOMEM;
    }

    /* Allocate actual buffers */
    size_t frame_size = config->rows * config->cols * (config->bit_depth / 8);
    if (config->frame_size > 0) {
        frame_size = config->frame_size;
    }

    for (uint32_t i = 0; i < config->num_buffers; i++) {
        g_frame_mgr.buffers[i].data = (uint8_t *)calloc(1, frame_size);
        if (g_frame_mgr.buffers[i].data == NULL) {
            /* Cleanup on allocation failure */
            for (uint32_t j = 0; j < i; j++) {
                free(g_frame_mgr.buffers[j].data);
            }
            free(g_frame_mgr.buffers);
            g_frame_mgr.buffers = NULL;
            return -ENOMEM;
        }

        g_frame_mgr.buffers[i].size = frame_size;
        g_frame_mgr.buffers[i].state = BUF_STATE_FREE;
        g_frame_mgr.buffers[i].frame_number = 0;
        g_frame_mgr.buffers[i].total_packets = 0;
        g_frame_mgr.buffers[i].sent_packets = 0;
    }

    /* Initialize instance state */
    g_frame_mgr.num_buffers = config->num_buffers;
    g_frame_mgr.oldest_index = 0;
    g_frame_mgr.initialized = true;

    /* Initialize statistics */
    memset(&g_frame_mgr.stats, 0, sizeof(frame_stats_t));

    return 0;
}

void frame_mgr_deinit(void) {
    if (!g_frame_mgr.initialized) {
        return;
    }

    /* Free buffers */
    if (g_frame_mgr.buffers != NULL) {
        for (uint32_t i = 0; i < g_frame_mgr.num_buffers; i++) {
            if (g_frame_mgr.buffers[i].data != NULL) {
                free(g_frame_mgr.buffers[i].data);
            }
        }
        free(g_frame_mgr.buffers);
        g_frame_mgr.buffers = NULL;
    }

    /* Reset state */
    g_frame_mgr.num_buffers = 0;
    g_frame_mgr.oldest_index = 0;
    g_frame_mgr.initialized = false;
    memset(&g_frame_mgr.stats, 0, sizeof(frame_stats_t));
}

int frame_mgr_get_buffer(uint32_t frame_number, uint8_t **buf, size_t *size) {
    if (!g_frame_mgr.initialized) {
        return -EINVAL;
    }

    if (buf == NULL || size == NULL) {
        return -EINVAL;
    }

    uint32_t index = frame_number_to_index(frame_number);
    frame_buffer_t *buffer = &g_frame_mgr.buffers[index];

    /* Oldest-drop policy (REQ-FW-051) */
    if (buffer->state != BUF_STATE_FREE) {
        /* All buffers busy - drop oldest SENDING buffer */
        bool found_sending = false;
        uint32_t drop_index = g_frame_mgr.oldest_index;

        /* Find oldest SENDING buffer */
        for (uint32_t i = 0; i < g_frame_mgr.num_buffers; i++) {
            uint32_t idx = (g_frame_mgr.oldest_index + i) % g_frame_mgr.num_buffers;
            if (g_frame_mgr.buffers[idx].state == BUF_STATE_SENDING) {
                drop_index = idx;
                found_sending = true;
                break;
            }
        }

        /* If no SENDING buffer, drop oldest non-FREE buffer */
        if (!found_sending) {
            for (uint32_t i = 0; i < g_frame_mgr.num_buffers; i++) {
                uint32_t idx = (g_frame_mgr.oldest_index + i) % g_frame_mgr.num_buffers;
                if (g_frame_mgr.buffers[idx].state != BUF_STATE_FREE) {
                    drop_index = idx;
                    break;
                }
            }
        }

        /* Drop the buffer */
        g_frame_mgr.buffers[drop_index].state = BUF_STATE_FREE;
        g_frame_mgr.stats.frames_dropped++;
        g_frame_mgr.stats.overruns++;

        /* Update oldest index */
        g_frame_mgr.oldest_index = (drop_index + 1) % g_frame_mgr.num_buffers;

        /* Use this buffer now */
        index = drop_index;
        buffer = &g_frame_mgr.buffers[index];
    }

    /* Transition to FILLING */
    buffer->state = BUF_STATE_FILLING;
    buffer->frame_number = frame_number;

    *buf = buffer->data;
    *size = buffer->size;

    return 0;
}

int frame_mgr_commit_buffer(uint32_t frame_number) {
    if (!g_frame_mgr.initialized) {
        return -EINVAL;
    }

    uint32_t index = frame_number_to_index(frame_number);
    frame_buffer_t *buffer = &g_frame_mgr.buffers[index];

    /* Validate state */
    if (buffer->state != BUF_STATE_FILLING) {
        return -EINVAL;
    }

    /* Transition to READY */
    buffer->state = BUF_STATE_READY;
    g_frame_mgr.stats.frames_received++;

    return 0;
}

int frame_mgr_get_ready_buffer(uint8_t **buf, size_t *size, uint32_t *frame_number) {
    if (!g_frame_mgr.initialized) {
        return -EINVAL;
    }

    if (buf == NULL || size == NULL || frame_number == NULL) {
        return -EINVAL;
    }

    /* Find oldest READY buffer */
    uint32_t ready_index = g_frame_mgr.num_buffers;  /* Invalid */
    uint32_t oldest_frame_number = UINT32_MAX;

    for (uint32_t i = 0; i < g_frame_mgr.num_buffers; i++) {
        uint32_t idx = (g_frame_mgr.oldest_index + i) % g_frame_mgr.num_buffers;
        if (g_frame_mgr.buffers[idx].state == BUF_STATE_READY) {
            if (g_frame_mgr.buffers[idx].frame_number < oldest_frame_number) {
                oldest_frame_number = g_frame_mgr.buffers[idx].frame_number;
                ready_index = idx;
            }
        }
    }

    if (ready_index >= g_frame_mgr.num_buffers) {
        return -ENOENT;  /* No ready buffers */
    }

    frame_buffer_t *buffer = &g_frame_mgr.buffers[ready_index];

    /* Transition to SENDING */
    buffer->state = BUF_STATE_SENDING;

    *buf = buffer->data;
    *size = buffer->size;
    *frame_number = buffer->frame_number;

    return 0;
}

int frame_mgr_release_buffer(uint32_t frame_number) {
    if (!g_frame_mgr.initialized) {
        return -EINVAL;
    }

    uint32_t index = frame_number_to_index(frame_number);
    frame_buffer_t *buffer = &g_frame_mgr.buffers[index];

    /* Validate state */
    if (buffer->state != BUF_STATE_SENDING) {
        return -EINVAL;
    }

    /* Transition to FREE */
    buffer->state = BUF_STATE_FREE;
    g_frame_mgr.stats.frames_sent++;

    /* Update oldest index if this was the oldest */
    if (index == g_frame_mgr.oldest_index) {
        g_frame_mgr.oldest_index = (index + 1) % g_frame_mgr.num_buffers;
    }

    return 0;
}

void frame_mgr_get_stats(frame_stats_t *stats) {
    if (stats == NULL) {
        return;
    }

    if (g_frame_mgr.initialized) {
        *stats = g_frame_mgr.stats;
    } else {
        memset(stats, 0, sizeof(frame_stats_t));
    }
}

buf_state_t frame_mgr_get_buffer_state(uint32_t frame_number) {
    if (!g_frame_mgr.initialized) {
        return BUF_STATE_FREE;
    }

    uint32_t index = frame_number_to_index(frame_number);
    return g_frame_mgr.buffers[index].state;
}

const char *frame_mgr_state_to_string(buf_state_t state) {
    switch (state) {
        case BUF_STATE_FREE:    return "FREE";
        case BUF_STATE_FILLING: return "FILLING";
        case BUF_STATE_READY:   return "READY";
        case BUF_STATE_SENDING: return "SENDING";
        default:                return "UNKNOWN";
    }
}

bool frame_mgr_is_initialized(void) {
    return g_frame_mgr.initialized;
}
