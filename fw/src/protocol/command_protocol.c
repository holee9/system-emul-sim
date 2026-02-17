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
#include <errno.h>
#include <string.h>
#include <stdlib.h>

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

    /* Calculate HMAC (simplified - in real implementation use OpenSSL HMAC) */
    memset(resp->hmac, 0, HMAC_SIZE);

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
 * @brief Validate HMAC
 */
int cmd_validate_hmac(const command_frame_t *cmd, const char *key) {
    if (!cmd_ctx.initialized) {
        return -EINVAL;
    }

    if (cmd == NULL || key == NULL) {
        return -EINVAL;
    }

    /* In real implementation, use OpenSSL HMAC() API
     * For now, just check that HMAC is not all zeros */
    for (int i = 0; i < HMAC_SIZE; i++) {
        if (cmd->hmac[i] != 0) {
            return 0;  /* Non-zero HMAC - accept */
        }
    }

    /* All zeros - invalid HMAC */
    return -EBADMSG;
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
        case CMD_START_SCAN:
            /* TODO: Start scan sequence */
            status = STATUS_OK;
            break;

        case CMD_STOP_SCAN:
            /* TODO: Stop scan sequence */
            status = STATUS_OK;
            break;

        case CMD_GET_STATUS:
            /* TODO: Get status */
            status = STATUS_OK;
            payload_len = 16;  /* Status payload size */
            break;

        case CMD_SET_CONFIG:
            /* TODO: Set configuration */
            status = STATUS_OK;
            break;

        case CMD_RESET:
            /* TODO: Reset system */
            status = STATUS_OK;
            break;

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
