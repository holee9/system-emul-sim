/**
 * @file health_monitor.h
 * @brief Health monitor for detector daemon
 *
 * REQ-FW-060: Watchdog and health monitoring (1s pet, 5s timeout)
 * REQ-FW-061: V4L2 restart delegation
 * REQ-FW-110: Structured syslog logging
 * REQ-FW-111: Runtime statistics aggregation
 * REQ-FW-112: GET_STATUS response < 50ms
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#ifndef DETECTOR_HEALTH_MONITOR_H
#define DETECTOR_HEALTH_MONITOR_H

#include <stdint.h>
#include <stdbool.h>
#include <stdarg.h>

#ifdef __cplusplus
extern "C" {
#endif

/* ==========================================================================
 * Constants
 * ========================================================================== */

#define WATCHDOG_PET_INTERVAL_MS  1000    /* 1 second */
#define WATCHDOG_TIMEOUT_MS       5000    /* 5 seconds */
#define STATUS_RESPONSE_MAX_MS    50      /* GET_STATUS max response time */

/* ==========================================================================
 * Types
 * ========================================================================== */

/**
 * @brief Log levels for structured logging
 */
typedef enum {
    LOG_DEBUG = 0,
    LOG_INFO,
    LOG_WARNING,
    LOG_ERROR,
    LOG_CRITICAL,
} log_level_t;

/**
 * @brief Runtime statistics counters
 */
typedef struct {
    uint64_t frames_received;
    uint64_t frames_sent;
    uint64_t frames_dropped;
    uint64_t spi_errors;
    uint64_t csi2_errors;
    uint64_t packets_sent;
    uint64_t bytes_sent;
    uint64_t auth_failures;
    uint64_t watchdog_resets;
} runtime_stats_t;

/**
 * @brief System status for GET_STATUS command
 */
typedef struct {
    uint8_t state;           /* Current sequence engine state */
    runtime_stats_t stats;   /* Runtime counters */
    uint8_t battery_soc;     /* Battery state of charge (%) */
    uint16_t battery_mv;     /* Battery voltage (mV) */
    uint32_t uptime_sec;     /* Daemon uptime (seconds) */
    uint16_t fpga_temp;      /* FPGA temperature (0.1 C) */
} system_status_t;

/**
 * @brief Health monitor context
 */
typedef struct {
    bool initialized;
    time_t start_time;
    uint64_t last_pet_ms;
    bool is_alive;
    uint64_t watchdog_reset_count;
    runtime_stats_t stats;
    log_level_t log_level;
    /* External references (set by main daemon) */
    void *seq_engine;
    void *frame_mgr;
} health_monitor_context_t;

/* ==========================================================================
 * API Functions
 * ========================================================================== */

/**
 * @brief Initialize health monitor
 * @return 0 on success, negative error code on failure
 */
int health_monitor_init(void);

/**
 * @brief Cleanup health monitor resources
 */
void health_monitor_deinit(void);

/**
 * @brief Pet the watchdog (call every 1 second)
 */
void health_monitor_pet_watchdog(void);

/**
 * @brief Check if watchdog is still alive
 * @return true if alive, false if timeout occurred
 */
bool health_monitor_is_alive(void);

/**
 * @brief Get current runtime statistics
 * @param stats Pointer to statistics structure to fill
 */
void health_monitor_get_stats(runtime_stats_t *stats);

/**
 * @brief Update a specific statistic counter
 * @param name Name of the statistic to update
 * @param delta Value to add (can be negative)
 */
void health_monitor_update_stat(const char *name, int64_t delta);

/**
 * @brief Log a structured message
 * @param level Log level
 * @param module Module name (e.g., "spi_master", "csi2_rx")
 * @param format Printf-style format string
 * @param ... Format arguments
 */
void health_monitor_log(log_level_t level, const char *module, const char *format, ...);

/**
 * @brief Get complete system status for GET_STATUS command
 * @param status Pointer to status structure to fill
 * @return 0 on success, negative error code on failure
 */
int health_monitor_get_status(system_status_t *status);

/**
 * @brief Set minimum log level
 * @param level New log level
 * @return 0 on success, negative error code on failure
 */
int health_monitor_set_log_level(log_level_t level);

/**
 * @brief Get current log level
 * @return Current log level
 */
log_level_t health_monitor_get_log_level(void);

/* ==========================================================================
 * Internal Functions (for testing)
 * ========================================================================== */

#ifdef TESTING
/**
 * @brief Get current time in milliseconds (mockable for testing)
 * @return Current time in milliseconds
 */
uint64_t health_get_time_ms(void);

/**
 * @brief Set current time for testing
 * @param time_ms Time in milliseconds
 */
void health_set_time_ms(uint64_t time_ms);
#endif

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_HEALTH_MONITOR_H */
