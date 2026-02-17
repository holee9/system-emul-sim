/**
 * @file main.c
 * @brief Main daemon entry point - X-ray Detector Panel SoC Controller
 *
 * REQ-FW-001: Linux 6.6.52 user-space daemon
 * REQ-FW-002: C11, CMake cross-build for ARM64
 * REQ-FW-003: detector_config.yaml at startup
 * REQ-FW-120: systemd management
 * REQ-FW-121: SIGTERM graceful shutdown
 *
 * Architecture:
 * - 5 threads: SPI control, CSI-2 RX, Ethernet TX, Command, Health Monitor
 * - Signal handling: SIGTERM, SIGINT (graceful shutdown), SIGHUP (reload config)
 * - Privilege drop: root → detector user
 * - Capability retention: CAP_NET_BIND_SERVICE, CAP_SYS_NICE
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <time.h>
#include <sys/types.h>
#include <sys/prctl.h>
#include <sys/capability.h>
#include <sys/resource.h>
#include <pthread.h>
#include <sys/syscall.h>

#include "health_monitor.h"
#include "config/config_loader.h"
#include "hal/spi_master.h"
#include "hal/csi2_rx.h"
#include "hal/eth_tx.h"
#include "hal/bq40z50_driver.h"
#include "sequence_engine.h"
#include "frame_manager.h"
#include "protocol/command_protocol.h"

/* ==========================================================================
 * Constants
 * ========================================================================== */

#define DAEMON_NAME "detector_daemon"
#define DAEMON_VERSION "1.0.0"
#define CONFIG_PATH "/etc/detector/detector_config.yaml"
#define LOG_FILE "/var/log/detector/daemon.log"
#define PID_FILE "/var/run/detector_daemon.pid"

/* User and group */
#define DETECTOR_USER "detector"
#define DETECTOR_GROUP "detector"

/* Thread priorities (SCHED_FIFO) */
#define THREAD_PRIORITY_SPI        80    /* Highest: 100us polling */
#define THREAD_PRIORITY_CSI2       70    /* High: V4L2 DQBUF */
#define THREAD_PRIORITY_TX         60    /* Medium: UDP transmission */
#define THREAD_PRIORITY_CMD        50    /* Normal: Command processing */
#define THREAD_PRIORITY_HEALTH     40    /* Low: Health monitoring */

/* ==========================================================================
 * Types
 * ========================================================================== */

/**
 * @brief Daemon thread context
 */
typedef enum {
    THREAD_SPI = 0,
    THREAD_CSI2,
    THREAD_TX,
    THREAD_CMD,
    THREAD_HEALTH,
    THREAD_COUNT
} thread_id_t;

/**
 * @brief Daemon state
 */
typedef enum {
    DAEMON_STATE_INIT = 0,
    DAEMON_STATE_IDLE,
    DAEMON_STATE_RUNNING,
    DAEMON_STATE_STOPPING,
    DAEMON_STATE_ERROR,
} daemon_state_t;

/**
 * @brief Main daemon context
 */
typedef struct {
    /* Daemon state */
    daemon_state_t state;
    bool running;
    bool shutdown_requested;

    /* Configuration */
    detector_config_t config;
    char config_path[256];

    /* Threads */
    pthread_t threads[THREAD_COUNT];
    pthread_mutex_t state_mutex;

    /* Module contexts */
    spi_master_context_t spi_ctx;
    csi2_rx_context_t csi2_ctx;
    eth_tx_context_t eth_ctx;
    bq40z50_context_t battery_ctx;
    sequence_engine_t seq_eng;
    frame_manager_t frame_mgr;
    command_context_t cmd_ctx;
    health_monitor_context_t *health_ctx;  /* Singleton */

    /* Statistics */
    uint64_t start_time_ms;
    uint32_t uptime_sec;
} daemon_context_t;

/* ==========================================================================
 * Global State
 * ========================================================================== */

static daemon_context_t g_daemon_ctx = {0};
static volatile sig_atomic_t g_signal_received = 0;

/* ==========================================================================
 * Signal Handling (TDD)
 * ========================================================================== */

/**
 * @brief Signal handler for graceful shutdown
 *
 * REQ-FW-121: SIGTERM/SIGINT graceful shutdown
 * - Complete pending TX
 * - Stop streaming
 * - Close sockets
 * - Exit cleanly
 */
static void signal_handler(int signo) {
    switch (signo) {
        case SIGTERM:
        case SIGINT:
            /* Request graceful shutdown */
            g_signal_received = signo;
            g_daemon_ctx.shutdown_requested = true;
            break;

        case SIGHUP:
            /* Reload configuration (optional) */
            g_signal_received = signo;
            break;

        case SIGUSR1:
            /* Dump debug info */
            g_signal_received = signo;
            break;

        default:
            break;
    }
}

/**
 * @brief Setup signal handlers
 */
static int setup_signal_handlers(void) {
    struct sigaction sa;

    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = signal_handler;
    sigemptyset(&sa.sa_mask);
    sa.sa_flags = SA_RESTART;  /* Restart interrupted system calls */

    /* Register signal handlers */
    if (sigaction(SIGTERM, &sa, NULL) < 0) {
        perror("sigaction SIGTERM");
        return -1;
    }

    if (sigaction(SIGINT, &sa, NULL) < 0) {
        perror("sigaction SIGINT");
        return -1;
    }

    if (sigaction(SIGHUP, &sa, NULL) < 0) {
        perror("sigaction SIGHUP");
        return -1;
    }

    if (sigaction(SIGUSR1, &sa, NULL) < 0) {
        perror("sigaction SIGUSR1");
        return -1;
    }

    /* Ignore SIGPIPE (write to broken socket) */
    signal(SIGPIPE, SIG_IGN);

    return 0;
}

/* ==========================================================================
 * Privilege Management
 * ========================================================================== */

/**
 * @brief Drop privileges to detector user
 *
 * REQ-FW-102: Non-root execution with capability constraints
 * Retains: CAP_NET_BIND_SERVICE, CAP_SYS_NICE
 */
static int drop_privileges(void) {
    /* Keep required capabilities */
    cap_t caps = cap_init();
    cap_value_t cap_list[2] = { CAP_NET_BIND_SERVICE, CAP_SYS_NICE };

    if (cap_set_flag(caps, CAP_PERMITTED, 2, cap_list, CAP_SET) < 0) {
        perror("cap_set_flag PERMITTED");
        cap_free(caps);
        return -1;
    }

    if (cap_set_flag(caps, CAP_EFFECTIVE, 2, cap_list, CAP_SET) < 0) {
        perror("cap_set_flag EFFECTIVE");
        cap_free(caps);
        return -1;
    }

    if (cap_set_proc(caps) < 0) {
        perror("cap_set_proc");
        cap_free(caps);
        return -1;
    }

    cap_free(caps);

    /* Switch to detector user */
    /* TODO: Implement user/group lookup and setuid/setgid */
    /* This requires getpwnam() and initialization */

    return 0;
}

/* ==========================================================================
 * Thread Entry Points (DDD)
 * ========================================================================== */

/**
 * @brief Set thread realtime priority
 */
static int set_thread_priority(pthread_t thread, int priority) {
    struct sched_param param;

    param.sched_priority = priority;

    if (pthread_setschedparam(thread, SCHED_FIFO, &param) != 0) {
        perror("pthread_setschedparam");
        return -1;
    }

    return 0;
}

/**
 * @brief SPI control thread
 *
 * Polls FPGA STATUS register at 100us interval (SCHED_FIFO priority 80)
 * REQ-FW-030: SPI communication with FPGA
 */
static void *spi_control_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;

    prctl(PR_SET_NAME, "spi_ctrl", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Poll FPGA STATUS register */
        /* TODO: Implement SPI status polling */

        usleep(100);  /* 100us */
    }

    health_monitor_log(LOG_INFO, "spi_thread", "SPI control thread exiting");
    return NULL;
}

/**
 * @brief CSI-2 RX thread
 *
 * Receives frames via V4L2 DQBUF
 * REQ-FW-020: MIPI CSI-2 reception
 */
static void *csi2_rx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;

    prctl(PR_SET_NAME, "csi2_rx", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Dequeue frame from V4L2 */
        /* TODO: Implement V4L2 DQBUF */

        usleep(1000);  /* 1ms */
    }

    health_monitor_log(LOG_INFO, "csi2_thread", "CSI-2 RX thread exiting");
    return NULL;
}

/**
 * @brief Ethernet TX thread
 *
 * Transmits frames via UDP
 * REQ-FW-040: Ethernet transmission
 */
static void *eth_tx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;

    prctl(PR_SET_NAME, "eth_tx", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Transmit frame queue */
        /* TODO: Implement UDP transmission */

        usleep(100);  /* 100us */
    }

    health_monitor_log(LOG_INFO, "tx_thread", "Ethernet TX thread exiting");
    return NULL;
}

/**
 * @brief Command protocol thread
 *
 * Listens on UDP port 8001 for commands
 * REQ-FW-050: Command protocol
 */
static void *command_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;

    prctl(PR_SET_NAME, "command", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Process incoming commands */
        /* TODO: Implement UDP command listener */

        usleep(10000);  /* 10ms */
    }

    health_monitor_log(LOG_INFO, "cmd_thread", "Command thread exiting");
    return NULL;
}

/**
 * @brief Health monitor thread
 *
 * Watchdog (1s pet, 5s timeout), statistics, logging
 * REQ-FW-060: Health monitoring
 */
static void *health_monitor_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;

    prctl(PR_SET_NAME, "health", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Pet watchdog */
        health_monitor_pet_watchdog();

        /* Update statistics */
        /* TODO: Aggregate statistics from all threads */

        /* Check battery level */
        if (bq40z50_emergency_shutdown(&ctx->battery_ctx)) {
            health_monitor_log(LOG_CRITICAL, "health", "Emergency battery shutdown");
            ctx->shutdown_requested = true;
            break;
        }

        sleep(1);  /* 1 second */
    }

    health_monitor_log(LOG_INFO, "health_thread", "Health monitor thread exiting");
    return NULL;
}

/* ==========================================================================
 * Daemon Lifecycle
 * ========================================================================== */

/**
 * @brief Initialize all modules
 */
static int init_modules(daemon_context_t *ctx) {
    int ret;

    /* Load configuration */
    ret = config_loader_load(ctx->config_path, &ctx->config);
    if (ret != 0) {
        fprintf(stderr, "Failed to load config: %s\n", ctx->config_path);
        return -1;
    }

    /* Initialize health monitor */
    ret = health_monitor_init();
    if (ret != 0) {
        fprintf(stderr, "Failed to initialize health monitor\n");
        return -1;
    }
    ctx->health_ctx = (health_monitor_context_t *)&g_health_ctx;

    /* Initialize SPI master */
    ret = spi_master_init(&ctx->spi_ctx, "/dev/spidev0.0");
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize SPI master");
        return -1;
    }

    /* Initialize CSI-2 RX */
    ret = csi2_rx_init(&ctx->csi2_ctx, "/dev/video0");
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize CSI-2 RX");
        return -1;
    }

    /* Initialize Ethernet TX */
    ret = eth_tx_init(&ctx->eth_ctx, "eth0");
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize Ethernet TX");
        return -1;
    }

    /* Initialize battery driver */
    ret = bq40z50_init(&ctx->battery_ctx, "/dev/i2c-1", BQ40Z50_I2C_ADDR);
    if (ret != 0) {
        health_monitor_log(LOG_WARNING, "main", "Failed to initialize battery driver (continuing without battery monitoring)");
    }

    /* Initialize sequence engine */
    ret = sequence_engine_init(&ctx->seq_eng);
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize sequence engine");
        return -1;
    }

    /* Initialize frame manager */
    ret = frame_manager_init(&ctx->frame_mgr);
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize frame manager");
        return -1;
    }

    /* Initialize command protocol */
    ret = command_protocol_init(&ctx->cmd_ctx, 8001);
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize command protocol");
        return -1;
    }

    return 0;
}

/**
 * @brief Start all threads
 */
static int start_threads(daemon_context_t *ctx) {
    int ret;
    pthread_attr_t attr;

    pthread_attr_init(&attr);
    pthread_attr_setstacksize(&attr, 64 * 1024);  /* 64KB stack */

    /* Create threads */
    ret = pthread_create(&ctx->threads[THREAD_SPI], &attr, spi_control_thread, ctx);
    if (ret != 0) {
        perror("pthread_create SPI");
        return -1;
    }
    set_thread_priority(ctx->threads[THREAD_SPI], THREAD_PRIORITY_SPI);

    ret = pthread_create(&ctx->threads[THREAD_CSI2], &attr, csi2_rx_thread, ctx);
    if (ret != 0) {
        perror("pthread_create CSI2");
        return -1;
    }
    set_thread_priority(ctx->threads[THREAD_CSI2], THREAD_PRIORITY_CSI2);

    ret = pthread_create(&ctx->threads[THREAD_TX], &attr, eth_tx_thread, ctx);
    if (ret != 0) {
        perror("pthread_create TX");
        return -1;
    }
    set_thread_priority(ctx->threads[THREAD_TX], THREAD_PRIORITY_TX);

    ret = pthread_create(&ctx->threads[THREAD_CMD], &attr, command_thread, ctx);
    if (ret != 0) {
        perror("pthread_create CMD");
        return -1;
    }
    set_thread_priority(ctx->threads[THREAD_CMD], THREAD_PRIORITY_CMD);

    ret = pthread_create(&ctx->threads[THREAD_HEALTH], &attr, health_monitor_thread, ctx);
    if (ret != 0) {
        perror("pthread_create HEALTH");
        return -1;
    }
    set_thread_priority(ctx->threads[THREAD_HEALTH], THREAD_PRIORITY_HEALTH);

    pthread_attr_destroy(&attr);

    ctx->running = true;
    ctx->state = DAEMON_STATE_RUNNING;

    return 0;
}

/**
 * @brief Stop all threads gracefully
 */
static void stop_threads(daemon_context_t *ctx) {
    ctx->state = DAEMON_STATE_STOPPING;
    ctx->running = false;

    health_monitor_log(LOG_INFO, "main", "Stopping all threads");

    /* Wait for threads to finish */
    for (int i = 0; i < THREAD_COUNT; i++) {
        if (pthread_join(ctx->threads[i], NULL) != 0) {
            perror("pthread_join");
        }
    }

    health_monitor_log(LOG_INFO, "main", "All threads stopped");
}

/**
 * @brief Cleanup all modules
 */
static void cleanup_modules(daemon_context_t *ctx) {
    health_monitor_log(LOG_INFO, "main", "Cleaning up modules");

    command_protocol_cleanup(&ctx->cmd_ctx);
    frame_manager_cleanup(&ctx->frame_mgr);
    sequence_engine_cleanup(&ctx->seq_eng);
    bq40z50_cleanup(&ctx->battery_ctx);
    eth_tx_cleanup(&ctx->eth_ctx);
    csi2_rx_cleanup(&ctx->csi2_ctx);
    spi_master_cleanup(&ctx->spi_ctx);
    health_monitor_deinit();

    config_loader_cleanup(&ctx->config);
}

/* ==========================================================================
 * Main Entry Point
 * ========================================================================== */

int main(int argc, char *argv[]) {
    int ret;
    FILE *pid_file = NULL;

    printf("%s v%s - X-ray Detector Panel SoC Controller\n", DAEMON_NAME, DAEMON_VERSION);
    printf("Copyright (c) 2026 ABYZ Lab\n");

    /* Parse command line arguments */
    const char *config_path = CONFIG_PATH;
    if (argc > 1) {
        config_path = argv[1];
    }

    /* Initialize daemon context */
    memset(&g_daemon_ctx, 0, sizeof(g_daemon_ctx));
    strncpy(g_daemon_ctx.config_path, config_path, sizeof(g_daemon_ctx.config_path) - 1);
    g_daemon_ctx.state = DAEMON_STATE_INIT;
    g_daemon_ctx.start_time_ms = time(NULL) * 1000;

    /* Setup signal handlers */
    ret = setup_signal_handlers();
    if (ret != 0) {
        fprintf(stderr, "Failed to setup signal handlers\n");
        return 1;
    }

    /* Initialize mutex */
    pthread_mutex_init(&g_daemon_ctx.state_mutex, NULL);

    /* Initialize modules */
    ret = init_modules(&g_daemon_ctx);
    if (ret != 0) {
        fprintf(stderr, "Failed to initialize modules\n");
        return 1;
    }

    /* Drop privileges */
    ret = drop_privileges();
    if (ret != 0) {
        health_monitor_log(LOG_WARNING, "main", "Failed to drop privileges, continuing as root");
    }

    /* Write PID file */
    pid_file = fopen(PID_FILE, "w");
    if (pid_file != NULL) {
        fprintf(pid_file, "%d\n", getpid());
        fclose(pid_file);
    }

    /* Start threads */
    ret = start_threads(&g_daemon_ctx);
    if (ret != 0) {
        health_monitor_log(LOG_ERROR, "main", "Failed to start threads");
        cleanup_modules(&g_daemon_ctx);
        return 1;
    }

    health_monitor_log(LOG_INFO, "main", "Daemon started successfully");

    /* Main loop - wait for shutdown signal */
    while (!g_daemon_ctx.shutdown_requested) {
        sleep(1);

        /* Handle signals */
        if (g_signal_received == SIGHUP) {
            health_monitor_log(LOG_INFO, "main", "Reloading configuration");
            /* TODO: Reload config */
            g_signal_received = 0;
        } else if (g_signal_received == SIGUSR1) {
            health_monitor_log(LOG_INFO, "main", "Debug info requested");
            /* TODO: Dump debug info */
            g_signal_received = 0;
        }
    }

    /* Graceful shutdown */
    health_monitor_log(LOG_INFO, "main", "Shutdown requested, initiating graceful shutdown");

    stop_threads(&g_daemon_ctx);
    cleanup_modules(&g_daemon_ctx);

    /* Remove PID file */
    unlink(PID_FILE);

    health_monitor_log(LOG_INFO, "main", "Daemon shutdown complete");

    pthread_mutex_destroy(&g_daemon_ctx.state_mutex);

    return 0;
}
