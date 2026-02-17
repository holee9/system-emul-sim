/**
 * @file health_monitor.c
 * @brief Health monitor implementation
 *
 * REQ-FW-060: Watchdog and health monitoring (1s pet, 5s timeout)
 * REQ-FW-061: V4L2 restart delegation
 * REQ-FW-110: Structured syslog logging
 * REQ-FW-111: Runtime statistics aggregation
 * REQ-FW-112: GET_STATUS response < 50ms
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "health_monitor.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <sys/time.h>
#include <syslog.h>
#include <stdarg.h>
#include <errno.h>

/* ==========================================================================
 * Internal State
 * ========================================================================== */

static health_monitor_context_t g_health_ctx = {0};

/* ==========================================================================
 * Internal Helper Functions
 * ========================================================================== */

/**
 * @brief Get current time in milliseconds
 */
static uint64_t get_time_ms_impl(void) {
#ifdef TESTING
    /* Use mock time when testing */
    extern uint64_t mock_time_ms;
    if (mock_time_ms != 0) {
        return mock_time_ms;
    }
#endif
    struct timeval tv;
    gettimeofday(&tv, NULL);
    return (uint64_t)tv.tv_sec * 1000ULL + (uint64_t)tv.tv_usec / 1000ULL;
}

/**
 * @brief Convert log level to syslog priority
 */
static int log_level_to_syslog(log_level_t level) {
    switch (level) {
        case LOG_DEBUG:    return LOG_DEBUG;
        case LOG_INFO:     return LOG_INFO;
        case LOG_WARNING:  return LOG_WARNING;
        case LOG_ERROR:    return LOG_ERR;
        case LOG_CRITICAL: return LOG_CRIT;
        default:           return LOG_INFO;
    }
}

/**
 * @brief Convert log level to string
 */
static const char* log_level_to_string(log_level_t level) {
    switch (level) {
        case LOG_DEBUG:    return "DEBUG";
        case LOG_INFO:     return "INFO";
        case LOG_WARNING:  return "WARNING";
        case LOG_ERROR:    return "ERROR";
        case LOG_CRITICAL: return "CRITICAL";
        default:           return "UNKNOWN";
    }
}

/**
 * @brief Find statistic by name
 */
static uint64_t* find_stat(const char *name) {
    if (strcmp(name, "frames_received") == 0) return &g_health_ctx.stats.frames_received;
    if (strcmp(name, "frames_sent") == 0) return &g_health_ctx.stats.frames_sent;
    if (strcmp(name, "frames_dropped") == 0) return &g_health_ctx.stats.frames_dropped;
    if (strcmp(name, "spi_errors") == 0) return &g_health_ctx.stats.spi_errors;
    if (strcmp(name, "csi2_errors") == 0) return &g_health_ctx.stats.csi2_errors;
    if (strcmp(name, "packets_sent") == 0) return &g_health_ctx.stats.packets_sent;
    if (strcmp(name, "bytes_sent") == 0) return &g_health_ctx.stats.bytes_sent;
    if (strcmp(name, "auth_failures") == 0) return &g_health_ctx.stats.auth_failures;
    if (strcmp(name, "watchdog_resets") == 0) return &g_health_ctx.stats.watchdog_resets;
    return NULL;
}

/* ==========================================================================
 * API Implementation
 * ========================================================================== */

int health_monitor_init(void) {
    if (g_health_ctx.initialized) {
        return 0;  /* Already initialized */
    }

    memset(&g_health_ctx, 0, sizeof(g_health_ctx));

    g_health_ctx.start_time = time(NULL);
    g_health_ctx.last_pet_ms = get_time_ms_impl();
    g_health_ctx.is_alive = true;
    g_health_ctx.log_level = LOG_INFO;  /* Default log level */

    /* Initialize syslog */
    openlog("detector_daemon", LOG_PID | LOG_NDELAY, LOG_DAEMON);

    g_health_ctx.initialized = true;

    health_monitor_log(LOG_INFO, "health_monitor", "Health monitor initialized");

    return 0;
}

void health_monitor_deinit(void) {
    if (!g_health_ctx.initialized) {
        return;
    }

    health_monitor_log(LOG_INFO, "health_monitor", "Health monitor shutting down");

    g_health_ctx.initialized = false;

    /* Close syslog */
    closelog();
}

void health_monitor_pet_watchdog(void) {
    if (!g_health_ctx.initialized) {
        return;
    }

    uint64_t now = get_time_ms_impl();

    /* Check if watchdog has timed out */
    if (g_health_ctx.is_alive) {
        uint64_t elapsed = now - g_health_ctx.last_pet_ms;
        if (elapsed > WATCHDOG_TIMEOUT_MS) {
            /* Watchdog timeout detected */
            g_health_ctx.is_alive = false;
            g_health_ctx.stats.watchdog_resets++;
            health_monitor_log(LOG_WARNING, "health_monitor",
                             "Watchdog timeout detected (%llu ms)", elapsed);
        }
    }

    /* Pet the watchdog - reset timer and mark alive */
    g_health_ctx.last_pet_ms = now;
    if (!g_health_ctx.is_alive) {
        /* Recovering from timeout */
        g_health_ctx.is_alive = true;
        health_monitor_log(LOG_INFO, "health_monitor", "Watchdog recovered");
    }
}

bool health_monitor_is_alive(void) {
    if (!g_health_ctx.initialized) {
        return false;
    }

    /* Check if current time exceeds timeout */
    uint64_t now = get_time_ms_impl();
    uint64_t elapsed = now - g_health_ctx.last_pet_ms;

    if (elapsed > WATCHDOG_TIMEOUT_MS) {
        g_health_ctx.is_alive = false;
    }

    return g_health_ctx.is_alive;
}

void health_monitor_get_stats(runtime_stats_t *stats) {
    if (stats == NULL) {
        return;
    }

    if (!g_health_ctx.initialized) {
        memset(stats, 0, sizeof(*stats));
        return;
    }

    memcpy(stats, &g_health_ctx.stats, sizeof(*stats));
}

void health_monitor_update_stat(const char *name, int64_t delta) {
    if (name == NULL || !g_health_ctx.initialized) {
        return;
    }

    uint64_t *stat = find_stat(name);
    if (stat == NULL) {
        /* Unknown statistic - ignore */
        return;
    }

    /* Handle negative deltas */
    if (delta < 0) {
        uint64_t abs_delta = (uint64_t)(-delta);
        if (*stat >= abs_delta) {
            *stat -= abs_delta;
        } else {
            *stat = 0;
        }
    } else {
        *stat += (uint64_t)delta;
    }
}

void health_monitor_log(log_level_t level, const char *module, const char *format, ...) {
    if (module == NULL || format == NULL) {
        return;
    }

    if (!g_health_ctx.initialized) {
        return;
    }

    /* Filter messages below current log level */
    if (level < g_health_ctx.log_level) {
        return;
    }

    /* Get timestamp */
    struct timeval tv;
    gettimeofday(&tv, NULL);
    struct tm *tm_info = localtime(&tv.tv_sec);

    char timestamp[64];
    strftime(timestamp, sizeof(timestamp), "%Y-%m-%d %H:%M:%S", tm_info);
    char timestamp_ms[128];
    snprintf(timestamp_ms, sizeof(timestamp_ms), "%s.%03ld", timestamp, tv.tv_usec / 1000);

    /* Format the message */
    char message[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(message, sizeof(message), format, args);
    va_end(args);

    /* Structured log format: [timestamp] [module] [LEVEL] message */
    syslog(log_level_to_syslog(level),
           "[%s] [%s] [%s] %s",
           timestamp_ms, module, log_level_to_string(level), message);
}

int health_monitor_get_status(system_status_t *status) {
    if (status == NULL) {
        return -EINVAL;
    }

    if (!g_health_ctx.initialized) {
        return -EINVAL;
    }

    /* Get current time for uptime calculation */
    time_t now = time(NULL);
    uint32_t uptime = (uint32_t)(now - g_health_ctx.start_time);

    /* Fill status structure */
    status->state = 0;  /* TODO: Get from sequence engine */
    memcpy(&status->stats, &g_health_ctx.stats, sizeof(status->stats));

    /* TODO: Get battery metrics from BQ40z50 driver */
    status->battery_soc = 100;  /* Default: full */
    status->battery_mv = 3700;  /* Default: typical voltage */

    status->uptime_sec = uptime;

    /* TODO: Get FPGA temperature from SPI registers */
    status->fpga_temp = 350;  /* Default: 35.0 C */

    return 0;
}

int health_monitor_set_log_level(log_level_t level) {
    if (level < LOG_DEBUG || level > LOG_CRITICAL) {
        return -EINVAL;
    }

    if (!g_health_ctx.initialized) {
        return -EINVAL;
    }

    g_health_ctx.log_level = level;
    return 0;
}

log_level_t health_monitor_get_log_level(void) {
    if (!g_health_ctx.initialized) {
        return LOG_INFO;  /* Default */
    }

    return g_health_ctx.log_level;
}

/* ==========================================================================
 * Testing Support
 * ========================================================================== */

#ifdef TESTING
/* Mock time for testing */
uint64_t mock_time_ms = 0;

uint64_t health_get_time_ms(void) {
    return get_time_ms_impl();
}

void health_set_time_ms(uint64_t time_ms) {
    mock_time_ms = time_ms;
}

/* Mock functions declared in test file */
void mock_set_time_ms(uint64_t time_ms) {
    health_set_time_ms(time_ms);
}

uint64_t mock_get_time_ms(void) {
    return health_get_time_ms();
}
#endif
