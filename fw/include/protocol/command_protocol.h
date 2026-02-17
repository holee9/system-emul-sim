/**
 * @file command_protocol.h
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

#ifndef DETECTOR_COMMAND_PROTOCOL_H
#define DETECTOR_COMMAND_PROTOCOL_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Magic numbers per REQ-FW-025, REQ-FW-026 */
#define MAGIC_COMMAND   0xBEEFCAFEu  /* Host -> FPGA */
#define MAGIC_RESPONSE  0xCAFEBEEFu  /* FPGA -> Host */

/* Command IDs */
#define CMD_START_SCAN  0x01
#define CMD_STOP_SCAN   0x02
#define CMD_GET_STATUS  0x10
#define CMD_SET_CONFIG  0x20
#define CMD_RESET       0x30

/* Max HMAC size */
#define HMAC_SIZE       32

/* Status codes */
#define STATUS_OK           0x0000
#define STATUS_ERROR        0x0001
#define STATUS_BUSY         0x0002
#define STATUS_INVALID_CMD  0x0003
#define STATUS_AUTH_FAILED  0x0004
#define STATUS_REPLAY       0x0005

/* Maximum number of tracked clients */
#define MAX_CLIENTS     16

/**
 * @brief Command frame format
 */
typedef struct {
    uint32_t magic;
    uint32_t sequence;
    uint16_t command_id;
    uint16_t payload_len;
    uint8_t hmac[HMAC_SIZE];
    uint8_t payload[];  /* Variable length */
} __attribute__((packed)) command_frame_t;

/**
 * @brief Response frame format
 */
typedef struct {
    uint32_t magic;
    uint32_t sequence;
    uint16_t status;
    uint16_t payload_len;
    uint8_t hmac[HMAC_SIZE];
    uint8_t payload[];  /* Variable length */
} __attribute__((packed)) response_frame_t;

/**
 * @brief Command Protocol context
 */
typedef struct {
    char hmac_key[64];
    uint32_t last_seq[MAX_CLIENTS];
    char last_ip[MAX_CLIENTS][16];
    uint32_t auth_failures;
    bool initialized;
} cmd_protocol_ctx_t;

/**
 * @brief Initialize Command Protocol
 *
 * @param hmac_key HMAC key for authentication
 * @return 0 on success, -errno on failure
 */
int cmd_protocol_init(const char *hmac_key);

/**
 * @brief Deinitialize Command Protocol
 */
void cmd_protocol_deinit(void);

/**
 * @brief Parse command packet
 *
 * @param buf Buffer containing packet
 * @param len Buffer length
 * @param cmd Pointer to store parsed command
 * @return 0 on success, -errno on failure
 */
int cmd_parse_packet(const uint8_t *buf, size_t len, command_frame_t *cmd);

/**
 * @brief Validate magic number
 *
 * @param buf Buffer containing packet
 * @param len Buffer length
 * @return 0 on success, -errno on failure
 */
int cmd_validate_magic(const uint8_t *buf, size_t len);

/**
 * @brief Validate HMAC
 *
 * @param cmd Command to validate
 * @param key HMAC key
 * @return 0 on success, -errno on failure
 */
int cmd_validate_hmac(const command_frame_t *cmd, const char *key);

/**
 * @brief Check for replay attack
 *
 * @param sequence Sequence number
 * @param source_ip Source IP address
 * @return 0 on success, -errno on failure
 */
int cmd_check_replay(uint32_t sequence, const char *source_ip);

/**
 * @brief Handle command
 *
 * @param cmd Command to handle
 * @param resp_buf Response buffer
 * @param resp_len Response buffer length (in/out)
 * @return 0 on success, -errno on failure
 */
int cmd_handle_command(const command_frame_t *cmd, uint8_t *resp_buf, size_t *resp_len);

/**
 * @brief Update replay protection state
 *
 * @param sequence Sequence number
 * @param source_ip Source IP address
 */
void cmd_update_replay_state(uint32_t sequence, const char *source_ip);

/**
 * @brief Get auth failure count
 *
 * @return Auth failure count
 */
uint32_t cmd_get_auth_failures(void);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_COMMAND_PROTOCOL_H */
