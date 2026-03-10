/**
 * @file command_protocol.c
 * @brief Command Protocol for Host-FPGA communication
 *
 * REQ-FW-025~028: Host command handling.
 * - Command magic 0xBEEFCAFE
 * - Response magic 0xCAFEBEEF
 * - Frame format definition
 * - Anti-replay (monotonic sequence number)
 * - HMAC-SHA256 authentication
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include "protocol/command_protocol.h"
#include "sequence_engine.h"
#include "health_monitor.h"
#include <errno.h>
#include <string.h>
#include <stdlib.h>
#include <openssl/hmac.h>
#include <openssl/evp.h>

/* Command Protocol context */
static cmd_protocol_ctx_t cmd_ctx = {
    .hmac_key = {0},
    .last_seq = {0},
    .last_ip = {""},
    .auth_failures = 0,
    .initialized = false
};

/* Minimum frame sizes */
#define MIN_COMMAND_FRAME_SIZE  (sizeof(command_frame_t))
#define MIN_RESPONSE_FRAME_SIZE (sizeof(response_frame_t))

/* Forward declarations */
static int find_client_slot(const char *source_ip);
static int validate_command_frame(const command_frame_t *cmd);
static int build_response(uint32_t sequence, uint16_t status,
                          const void *payload, size_t payload_len,
                          uint8_t *resp_buf, size_t *resp_len);

/* ==========================================================================
 * Helper Functions
 * ========================================================================== */

/**
 * @brief Find or allocate client slot
 */
static int find_client_slot(const char *source_ip) {
    /* First try to find existing client */
    for (int i = 0; i < MAX_CLIENTS; i++) {
        if (cmd_ctx.last_ip[i][0] != '\0' &&
            strcmp(cmd_ctx.last_ip[i], source_ip) == 0) {
            return i;
        }
    }

    /* Find empty slot */
    for (int i = 0; i < MAX_CLIENTS; i++) {
        if (cmd_ctx.last_ip[i][0] == '\0') {
            strncpy(cmd_ctx.last_ip[i], source_ip, 15);
            cmd_ctx.last_ip[i][15] = '\0';
            return i;
        }
    }

    /* No slots available */
    return -1;
}

/**
 * @brief Validate command frame structure
 */
static int validate_command_frame(const command_frame_t *cmd) {
    if (cmd == NULL) {
        return -EINVAL;
    }

    /* Check magic number */
    if (cmd->magic != MAGIC_COMMAND) {
        return -EINVAL;
    }

    /* Check command ID range */
    if (cmd->command_id > 0xFF) {
        return -EINVAL;
    }

    return 0;
}

/**
 * @brief Calculate HMAC-SHA256 for response frame
 *
 * Computes HMAC over bytes 0-11 (magic, sequence, status) + payload
 * using pre-shared key from cmd_ctx.hmac_key
 *
 * @param resp Response frame
 * @param payload_len Payload length
 * @return 0 on success, -1 on failure
 */
static int calculate_response_hmac(response_frame_t *resp, size_t payload_len) {
    if (resp == NULL) {
        return -1;
    }

    /* Prepare HMAC data: bytes 0-11 (excluding HMAC field) + payload */
    size_t hmac_data_len = 12 + payload_len;  /* 12 bytes before HMAC + payload */
    uint8_t *hmac_data = (uint8_t *)malloc(hmac_data_len);
    if (hmac_data == NULL) {
        return -1;
    }

    /* Copy bytes 0-11 (magic, sequence, status, payload_len) */
    memcpy(hmac_data, resp, 12);
    /* Copy payload if present */
    if (payload_len > 0) {
        memcpy(hmac_data + 12, resp->payload, payload_len);
    }

    /* Calculate HMAC-SHA256 using OpenSSL */
    unsigned int hmac_len = HMAC_SIZE;
    unsigned char *result = HMAC(
        EVP_sha256(),                              /* SHA-256 hash function */
        cmd_ctx.hmac_key,                          /* Key */
        strlen(cmd_ctx.hmac_key),                  /* Key length */
        hmac_data,                                 /* Data to authenticate */
        hmac_data_len,                             /* Data length */
        resp->hmac,                                /* Output HMAC */
        &hmac_len                                  /* Output length */
    );

    free(hmac_data);

    if (result == NULL) {
        memset(resp->hmac, 0, HMAC_SIZE);
        return -1;
    }

    return 0;
}

/**
 * @brief Build response frame
 */
static int build_response(uint32_t sequence, uint16_t status,
                          const void *payload, size_t payload_len,
                          uint8_t *resp_buf, size_t *resp_len) {
    if (resp_buf == NULL || resp_len == NULL) {
        return -EINVAL;
    }

    size_t required_len = MIN_RESPONSE_FRAME_SIZE + payload_len;
    if (*resp_len < required_len) {
        return -EMSGSIZE;
    }

    response_frame_t *resp = (response_frame_t *)resp_buf;
    resp->magic = MAGIC_RESPONSE;
    resp->sequence = sequence;
    resp->status = status;
    resp->payload_len = payload_len;

    /* Copy payload if present */
    if (payload != NULL && payload_len > 0) {
        memcpy(resp->payload, payload, payload_len);
    }

    /* Calculate HMAC-SHA256 using OpenSSL */
    if (calculate_response_hmac(resp, payload_len) != 0) {
        return -1;
    }

    *resp_len = required_len;
    return 0;
}

/* ==========================================================================
 * Public API
 * ========================================================================== */

/**
 * @brief Initialize Command Protocol
 */
int cmd_protocol_init(const char *hmac_key) {
    if (hmac_key == NULL) {
        return -EINVAL;
    }

    strncpy(cmd_ctx.hmac_key, hmac_key, sizeof(cmd_ctx.hmac_key) - 1);
    cmd_ctx.hmac_key[sizeof(cmd_ctx.hmac_key) - 1] = '\0';

    /* Reset client tracking */
    memset(cmd_ctx.last_seq, 0, sizeof(cmd_ctx.last_seq));
    memset(cmd_ctx.last_ip, 0, sizeof(cmd_ctx.last_ip));

    cmd_ctx.auth_failures = 0;
    cmd_ctx.initialized = true;

    return 0;
}

/**
 * @brief Deinitialize Command Protocol
 */
void cmd_protocol_deinit(void) {
    memset(&cmd_ctx, 0, sizeof(cmd_ctx));
    cmd_ctx.initialized = false;
}

/**
 * @brief Parse command packet
 */
int cmd_parse_packet(const uint8_t *buf, size_t len, command_frame_t *cmd) {
    if (!cmd_ctx.initialized) {
        return -EINVAL;
    }

    if (buf == NULL || cmd == NULL) {
        return -EINVAL;
    }

    if (len < MIN_COMMAND_FRAME_SIZE) {
        return -EMSGSIZE;
    }

    /* Copy command structure from buffer */
    memcpy(cmd, buf, MIN_COMMAND_FRAME_SIZE);

    /* Validate command */
    int result = validate_command_frame(cmd);
    if (result != 0) {
        return result;
    }

    return 0;
}

/**
 * @brief Validate magic number
 */
int cmd_validate_magic(const uint8_t *buf, size_t len) {
    if (buf == NULL) {
        return -EINVAL;
    }

    if (len < 4) {
        return -EMSGSIZE;
    }

    /* Extract magic (little-endian) */
    uint32_t magic = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);

    /* Check for valid command or response magic */
    if (magic != MAGIC_COMMAND && magic != MAGIC_RESPONSE) {
        return -EINVAL;
    }

    return 0;
}

/**
 * @brief Validate HMAC-SHA256
 *
 * Validates HMAC over command frame bytes 0-11 + payload
 * using pre-shared key
 *
 * @param cmd Command to validate
 * @param key HMAC key
 * @return 0 on success, -EBADMSG on HMAC mismatch, -EINVAL on error
 */
int cmd_validate_hmac(const command_frame_t *cmd, const char *key) {
    if (!cmd_ctx.initialized) {
        return -EINVAL;
    }

    if (cmd == NULL || key == NULL) {
        return -EINVAL;
    }

    /* Prepare HMAC data: bytes 0-11 (excluding HMAC field) + payload */
    size_t hmac_data_len = 12 + cmd->payload_len;
    uint8_t *hmac_data = (uint8_t *)malloc(hmac_data_len);
    if (hmac_data == NULL) {
        return -EINVAL;
    }

    /* Copy bytes 0-11 (magic, sequence, command_id, payload_len) */
    memcpy(hmac_data, cmd, 12);
    /* Copy payload if present */
    if (cmd->payload_len > 0) {
        memcpy(hmac_data + 12, cmd->payload, cmd->payload_len);
    }

    /* Calculate expected HMAC-SHA256 using OpenSSL */
    unsigned char calculated_hmac[HMAC_SIZE];
    unsigned int hmac_len = HMAC_SIZE;
    unsigned char *result = HMAC(
        EVP_sha256(),                              /* SHA-256 hash function */
        key,                                        /* Key */
        strlen(key),                                /* Key length */
        hmac_data,                                 /* Data to authenticate */
        hmac_data_len,                             /* Data length */
        calculated_hmac,                           /* Output HMAC */
        &hmac_len                                  /* Output length */
    );

    free(hmac_data);

    if (result == NULL) {
        return -EBADMSG;
    }

    /* Compare HMACs using constant-time comparison */
    if (CRYPTO_memcmp(calculated_hmac, cmd->hmac, HMAC_SIZE) != 0) {
        return -EBADMSG;  /* HMAC mismatch */
    }

    return 0;  /* HMAC valid */
}

/**
 * @brief Check for replay attack
 */
int cmd_check_replay(uint32_t sequence, const char *source_ip) {
    if (!cmd_ctx.initialized) {
        return -EINVAL;
    }

    if (source_ip == NULL) {
        return -EINVAL;
    }

    int slot = find_client_slot(source_ip);
    if (slot < 0) {
        /* Too many clients */
        return -ENOMEM;
    }

    /* Check sequence number */
    if (sequence <= cmd_ctx.last_seq[slot]) {
        /* Replay attack detected */
        return -EADDRINUSE;
    }

    return 0;
}

/**
 * @brief Update replay protection state
 */
void cmd_update_replay_state(uint32_t sequence, const char *source_ip) {
    if (!cmd_ctx.initialized) {
        return;
    }

    if (source_ip == NULL) {
        return;
    }

    int slot = find_client_slot(source_ip);
    if (slot >= 0) {
        cmd_ctx.last_seq[slot] = sequence;
    }
}

/**
 * @brief Handle command
 */
int cmd_handle_command(const command_frame_t *cmd, uint8_t *resp_buf, size_t *resp_len) {
    if (!cmd_ctx.initialized) {
        return -EINVAL;
    }

    if (cmd == NULL || resp_buf == NULL || resp_len == NULL) {
        return -EINVAL;
    }

    /* Validate command */
    int result = validate_command_frame(cmd);
    if (result != 0) {
        return build_response(cmd->sequence, STATUS_INVALID_CMD,
                            NULL, 0, resp_buf, resp_len);
    }

    /* Validate HMAC */
    result = cmd_validate_hmac(cmd, cmd_ctx.hmac_key);
    if (result != 0) {
        cmd_ctx.auth_failures++;
        return build_response(cmd->sequence, STATUS_AUTH_FAILED,
                            NULL, 0, resp_buf, resp_len);
    }

    /* Handle based on command ID */
    uint16_t status = STATUS_OK;
    uint8_t payload[256];
    size_t payload_len = 0;

    switch (cmd->command_id) {
        case CMD_START_SCAN: {
            /* Start scan sequence */
            scan_mode_t mode = SCAN_MODE_SINGLE;  /* Default: single scan */

            /* Parse mode from payload if provided */
            if (cmd->payload_len >= 1) {
                uint8_t mode_value = cmd->payload[0];
                if (mode_value < SCAN_MODE_MAX) {
                    mode = (scan_mode_t)mode_value;
                } else {
                    status = STATUS_ERROR;
                    break;
                }
            }

            int rc = seq_start_scan(mode);
            if (rc != 0) {
                status = (rc == -EBUSY) ? STATUS_BUSY : STATUS_ERROR;
            } else {
                status = STATUS_OK;
            }
            break;
        }

        case CMD_STOP_SCAN: {
            /* Stop scan sequence */
            int rc = seq_stop_scan();
            status = (rc == 0) ? STATUS_OK : STATUS_ERROR;
            break;
        }

        case CMD_GET_STATUS: {
            /* Get status */
            seq_stats_t seq_stats;
            memset(payload, 0, sizeof(payload));

            /* Get sequence engine state and statistics */
            payload[0] = (uint8_t)seq_get_state();

            if (seq_get_stats(&seq_stats) == 0) {
                /* Pack statistics into payload (little-endian) */
                memcpy(&payload[1], &seq_stats.frames_received, sizeof(uint32_t));
                memcpy(&payload[5], &seq_stats.frames_sent, sizeof(uint32_t));
                memcpy(&payload[9], &seq_stats.errors, sizeof(uint32_t));
                memcpy(&payload[13], &seq_stats.retries, sizeof(uint32_t));
                payload_len = 17;  /* 1 byte state + 4 * 4 bytes stats */
            } else {
                payload_len = 1;  /* State only */
            }

            status = STATUS_OK;
            break;
        }

        case CMD_SET_CONFIG: {
            /* Set configuration */
            /* Parse payload for configuration parameters */
            if (cmd->payload_len >= 8) {
                /* Parse configuration parameters from payload */
                uint32_t exposure_ms;
                uint16_t gain_db;
                uint8_t binning_x, binning_y;

                /* Little-endian unpacking */
                memcpy(&exposure_ms, &cmd->payload[0], sizeof(uint32_t));
                memcpy(&gain_db, &cmd->payload[4], sizeof(uint16_t));
                binning_x = cmd->payload[6];
                binning_y = cmd->payload[7];

                /* Validate parameters */
                if (exposure_ms > 0 && exposure_ms <= 10000 && /* 10ms max exposure */
                    gain_db >= 0 && gain_db <= 64 &&        /* 0-64dB gain range */
                    binning_x >= 1 && binning_x <= 4 &&      /* 1-4x binning */
                    binning_y >= 1 && binning_y <= 4) {

                    /* Apply configuration (mock implementation) */
                    health_monitor_log(LOG_INFO, "cmd",
                                     "Config set: exposure=%ums, gain=%udB, binning=%dx%d",
                                     exposure_ms, gain_db, binning_x, binning_y);

                    status = STATUS_OK;
                    payload_len = 1;  /* Acknowledge */
                    payload[0] = 0x01;  // Success indicator
                } else {
                    status = STATUS_INVALID_CMD;
                    payload_len = 4;  // Error code
                    uint32_t error_code = -EINVAL;
                    memcpy(payload, &error_code, 4);
                }
            } else {
                status = STATUS_INVALID_CMD;
                payload_len = 4;
                uint32_t error_code = -EMSGSIZE;
                memcpy(payload, &error_code, 4);
            }
            break;
        }

        case CMD_RESET: {
            /* Reset system */
            seq_deinit();
            seq_init();

            /* Reset health monitor statistics */
            extern void health_monitor_reset_stats(void);
            health_monitor_reset_stats();

            status = STATUS_OK;
            break;
        }

        default:
            status = STATUS_INVALID_CMD;
            break;
    }

    return build_response(cmd->sequence, status,
                         payload, payload_len, resp_buf, resp_len);
}

/**
 * @brief Get auth failure count
 */
uint32_t cmd_get_auth_failures(void) {
    return cmd_ctx.auth_failures;
}
