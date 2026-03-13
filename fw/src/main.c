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
#include <pwd.h>
#include <grp.h>
#include <syslog.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>

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
    spi_master_t *spi_ctx;
    csi2_rx_t *csi2_ctx;
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

/* Global SPI master context for FPGA register access */
spi_master_t *g_spi_master = NULL;

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
 *
 * Process:
 * 1. Keep required capabilities in permitted and effective sets
 * 2. Drop supplementary groups
 * 3. Set GID to detector group
 * 4. Set UID to detector user
 * 5. Verify privilege drop succeeded
 */
static int drop_privileges(void) {
    struct passwd *pw;
    struct group *gr;

    /* Lookup detector user */
    pw = getpwnam(DETECTOR_USER);
    if (!pw) {
        syslog(LOG_ERR, "User '%s' not found", DETECTOR_USER);
        return -1;
    }

    /* Lookup detector group */
    gr = getgrnam(DETECTOR_GROUP);
    if (!gr) {
        syslog(LOG_ERR, "Group '%s' not found", DETECTOR_GROUP);
        return -1;
    }

    /* Keep required capabilities before dropping root */
    cap_t caps = cap_init();
    cap_value_t cap_list[2] = { CAP_NET_BIND_SERVICE, CAP_SYS_NICE };

    if (cap_set_flag(caps, CAP_PERMITTED, 2, cap_list, CAP_SET) < 0) {
        syslog(LOG_ERR, "cap_set_flag PERMITTED failed: %m");
        cap_free(caps);
        return -1;
    }

    if (cap_set_flag(caps, CAP_EFFECTIVE, 2, cap_list, CAP_SET) < 0) {
        syslog(LOG_ERR, "cap_set_flag EFFECTIVE failed: %m");
        cap_free(caps);
        return -1;
    }

    if (cap_set_proc(caps) < 0) {
        syslog(LOG_ERR, "cap_set_proc failed: %m");
        cap_free(caps);
        return -1;
    }

    cap_free(caps);

    /* Drop supplementary groups (clear all groups) */
    if (setgroups(0, NULL) < 0) {
        syslog(LOG_ERR, "setgroups failed: %m");
        return -1;
    }

    /* Set GID to detector group */
    if (setgid(gr->gr_gid) < 0) {
        syslog(LOG_ERR, "setgid failed: %m");
        return -1;
    }

    /* Set UID to detector user */
    if (setuid(pw->pw_uid) < 0) {
        syslog(LOG_ERR, "setuid failed: %m");
        return -1;
    }

    /* Verify privilege drop succeeded */
    if (setuid(0) == 0) {
        syslog(LOG_ERR, "Privilege drop failed - still have root access");
        return -1;
    }

    syslog(LOG_INFO, "Privileges dropped to user '%s' (UID %d, GID %d)",
           DETECTOR_USER, pw->pw_uid, gr->gr_gid);

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
        uint8_t fpga_status = 0x04; /* Default: READY (mock implementation) */
        uint32_t error_count = 0;

        /* Real implementation would read SPI:
        spi_status_t spi_result = spi_master_read_status(&ctx->spi_ctx);
        if (spi_result.status == SPI_OK) {
            fpga_status = spi_result.reg_value;
        } else {
            error_count++;
            health_monitor_update_stat("spi_errors", 1);
        }
        */

        /* Check FPGA status bits */
        if (fpga_status & FPGA_STATUS_ERROR) {
            health_monitor_log(LOG_WARNING, "spi", "FPGA error detected (status=0x%02X)", fpga_status);
            error_count++;
            health_monitor_update_stat("spi_errors", 1);
        }

        if (fpga_status & FPGA_STATUS_BUSY) {
            /* FPGA is busy processing */
            health_monitor_update_stat("frames_received", 1);
        } else {
            /* FPGA is idle and ready for next command */
            health_monitor_log(LOG_DEBUG, "spi", "FPGA ready (status=0x%02X)", fpga_status);
        }

        /* Update SPI error statistics */
        if (error_count > 0) {
            health_monitor_update_stat("spi_errors", error_count);
        }

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

    uint32_t frame_number = 0;

    while (ctx->running && !ctx->shutdown_requested) {
        /* Get ready buffer from frame manager */
        uint8_t *frame_data = NULL;
        size_t frame_size = 0;
        uint32_t ready_frame_number = 0;

        int ret = frame_mgr_get_ready_buffer(&frame_data, &frame_size, &ready_frame_number);
        if (ret == 0) {
            /* Frame available, transmit via UDP */
            /* REQ-FW-040: Frame fragmentation and transmission */
            eth_tx_status_t tx_result = eth_tx_send_frame(
                ctx->eth_ctx.handle,
                frame_data,
                frame_size,
                ctx->config.detector.rows,      /* Width */
                ctx->config.detector.cols,      /* Height */
                ctx->config.detector.bit_depth, /* Bit depth */
                ready_frame_number              /* Frame number */
            );

            if (tx_result == ETH_TX_OK) {
                /* Transmission successful, release buffer */
                frame_mgr_release_buffer(ready_frame_number);
                health_monitor_update_stat("frames_sent", 1);

                /* Notify sequence engine of transmission complete */
                seq_handle_event(EVT_COMPLETE, NULL);
            } else {
                /* Transmission failed */
                health_monitor_log(LOG_ERROR, "tx_thread",
                                 "Failed to send frame %u: %d",
                                 ready_frame_number, tx_result);
                health_monitor_update_stat("frames_dropped", 1);

                /* Still release buffer on error */
                frame_mgr_release_buffer(ready_frame_number);
            }
        } else if (ret == -ENOENT) {
            /* No ready buffers, wait */
            usleep(100);  /* 100us */
        } else {
            /* Error getting buffer */
            health_monitor_log(LOG_ERROR, "tx_thread",
                             "Failed to get ready buffer: %d", ret);
            usleep(1000);  /* 1ms */
        }
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

    /* Create UDP socket for command listening */
    int cmd_fd = socket(AF_INET, SOCK_DGRAM, 0);
    if (cmd_fd < 0) {
        health_monitor_log(LOG_ERROR, "cmd_thread", "Failed to create command socket");
        return NULL;
    }

    /* Set socket options */
    int opt = 1;
    setsockopt(cmd_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    /* Bind to command port */
    struct sockaddr_in cmd_addr = {0};
    cmd_addr.sin_family = AF_INET;
    cmd_addr.sin_addr.s_addr = INADDR_ANY;
    cmd_addr.sin_port = htons(ETH_DEFAULT_CMD_PORT);

    if (bind(cmd_fd, (struct sockaddr *)&cmd_addr, sizeof(cmd_addr)) < 0) {
        health_monitor_log(LOG_ERROR, "cmd_thread", "Failed to bind command socket to port %d",
                         ETH_DEFAULT_CMD_PORT);
        close(cmd_fd);
        return NULL;
    }

    health_monitor_log(LOG_INFO, "cmd_thread", "Command listener started on port %d",
                     ETH_DEFAULT_CMD_PORT);

    /* Command buffer */
    uint8_t cmd_buf[2048];
    uint8_t resp_buf[2048];
    struct sockaddr_in client_addr;
    socklen_t client_len;

    while (ctx->running && !ctx->shutdown_requested) {
        /* Set socket timeout for non-blocking operation */
        struct timeval tv = {0, 100000};  /* 100ms timeout */
        setsockopt(cmd_fd, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));

        /* Receive command packet */
        client_len = sizeof(client_addr);
        ssize_t recv_len = recvfrom(cmd_fd, cmd_buf, sizeof(cmd_buf), 0,
                                    (struct sockaddr *)&client_addr, &client_len);

        if (recv_len < 0) {
            if (errno == EAGAIN || errno == EWOULDBLOCK) {
                /* Timeout, continue */
                continue;
            }
            /* Error */
            health_monitor_log(LOG_ERROR, "cmd_thread", "recvfrom error: %d", errno);
            break;
        }

        /* Parse command packet */
        command_frame_t cmd;
        int parse_result = cmd_parse_packet(cmd_buf, recv_len, &cmd);
        if (parse_result != 0) {
            health_monitor_log(LOG_WARNING, "cmd_thread", "Failed to parse command packet");
            continue;
        }

        /* Get client IP for replay protection */
        char client_ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &client_addr.sin_addr, client_ip, sizeof(client_ip));

        /* Check for replay attack */
        int replay_result = cmd_check_replay(cmd.sequence, client_ip);
        if (replay_result != 0) {
            health_monitor_log(LOG_WARNING, "cmd_thread",
                             "Replay attack detected from %s (seq=%u)",
                             client_ip, cmd.sequence);
            health_monitor_update_stat("auth_failures", 1);
            continue;
        }

        /* Handle command */
        size_t resp_len = sizeof(resp_buf);
        int handle_result = cmd_handle_command(&cmd, resp_buf, &resp_len);
        if (handle_result != 0) {
            health_monitor_log(LOG_ERROR, "cmd_thread", "Failed to handle command");
            continue;
        }

        /* Update replay protection state */
        cmd_update_replay_state(cmd.sequence, client_ip);

        /* Send response */
        ssize_t sent_len = sendto(cmd_fd, resp_buf, resp_len, 0,
                                  (struct sockaddr *)&client_addr, client_len);
        if (sent_len < 0) {
            health_monitor_log(LOG_ERROR, "cmd_thread", "Failed to send response");
        } else if ((size_t)sent_len != resp_len) {
            health_monitor_log(LOG_WARNING, "cmd_thread", "Partial response sent");
        }

        health_monitor_log(LOG_DEBUG, "cmd_thread",
                         "Command processed: id=0x%02X, seq=%u, from=%s",
                         cmd.command_id, cmd.sequence, client_ip);
    }

    close(cmd_fd);
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

        /* Aggregate statistics from all threads */
        /* Collect statistics from sequence engine */
        seq_stats_t seq_stats;
        if (seq_get_stats(&seq_stats) == 0) {
            health_monitor_update_stat("frames_received", (int64_t)seq_stats.frames_received);
            health_monitor_update_stat("frames_sent", (int64_t)seq_stats.frames_sent);
            health_monitor_update_stat("retries", (int64_t)seq_stats.retries);
        }

        /* Calculate derived metrics */
        uint64_t frames_total = g_health_ctx.stats.frames_received;
        uint64_t frames_dropped = g_health_ctx.stats.frames_dropped;

        if (frames_total > 0) {
            uint32_t drop_rate = (uint32_t)((frames_dropped * 100) / frames_total);
            health_monitor_log(LOG_DEBUG, "health",
                             "Frame stats: received=%lu, sent=%lu, dropped=%lu, drop_rate=%u%%",
                             frames_total, g_health_ctx.stats.frames_sent,
                             frames_dropped, drop_rate);
        }

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
    spi_config_t spi_config = {
        .device = "/dev/spidev0.0",
        .speed = SPI_DEFAULT_SPEED,
        .bits_per_word = SPI_DEFAULT_BITS,
        .mode = SPI_DEFAULT_MODE
    };

    ctx->spi_ctx = spi_master_create(&spi_config);
    if (ctx->spi_ctx == NULL) {
        health_monitor_log(LOG_ERROR, "main", "Failed to initialize SPI master");
        return -1;
    }

    /* Set global SPI master context */
    g_spi_master = ctx->spi_ctx;

    /* Initialize CSI-2 RX */
    csi2_config_t csi2_config = {
        .device = "/dev/video0",
        .width = ctx->config.detector.cols,
        .height = ctx->config.detector.rows,
        .format = CSI2_PIX_FMT_RAW16,
        .buffer_count = 4,
        .fps = 15
    };

    ctx->csi2_ctx = csi2_rx_create(&csi2_config);
    if (ctx->csi2_ctx == NULL) {
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
    csi2_rx_destroy(ctx->csi2_ctx);
    spi_master_destroy(ctx->spi_ctx);
    g_spi_master = NULL;
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
            /* Reload configuration from file */
            detector_config_t new_config;
            config_status_t rc = config_load(g_daemon_ctx.config_path, &new_config);
            if (rc == CONFIG_OK) {
                /* Apply hot-swappable parameters */
                if (config_validate(&new_config) == CONFIG_OK) {
                    pthread_mutex_lock(&g_daemon_ctx.state_mutex);
                    g_daemon_ctx.config = new_config;
                    pthread_mutex_unlock(&g_daemon_ctx.state_mutex);
                    health_monitor_log(LOG_INFO, "main", "Configuration reloaded successfully");
                } else {
                    health_monitor_log(LOG_WARNING, "main", "Invalid configuration, not applied");
                }
            } else {
                health_monitor_log(LOG_ERROR, "main", "Failed to reload configuration: %s",
                                 config_get_error());
            }
            g_signal_received = 0;
        } else if (g_signal_received == SIGUSR1) {
            health_monitor_log(LOG_INFO, "main", "Debug info requested");
            /* Dump debug info */
            runtime_stats_t stats;
            health_monitor_get_stats(&stats);

            seq_stats_t seq_stats;
            seq_get_stats(&seq_stats);

            health_monitor_log(LOG_INFO, "main", "=== Debug Info ===");
            health_monitor_log(LOG_INFO, "main", "Uptime: %u sec", g_daemon_ctx.uptime_sec);
            health_monitor_log(LOG_INFO, "main", "Seq State: %s", seq_state_to_string(seq_get_state()));
            health_monitor_log(LOG_INFO, "main", "Frames: rcvd=%lu, sent=%lu, dropped=%lu",
                             stats.frames_received, stats.frames_sent, stats.frames_dropped);
            health_monitor_log(LOG_INFO, "main", "Errors: spi=%lu, csi2=%lu, auth=%lu",
                             stats.spi_errors, stats.csi2_errors, stats.auth_failures);
            health_monitor_log(LOG_INFO, "main", "================");
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
