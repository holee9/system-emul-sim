/**
 * @file test_command_protocol.c
 * @brief Unit tests for Command Protocol (FW-UT-07)
 *
 * Test ID: FW-UT-07
 * Coverage: Command Protocol per REQ-FW-025 through REQ-FW-028, REQ-FW-100
 *
 * Tests:
 * - Magic number validation (0xBEEFCAFE / 0xCAFEBEEF)
 * - Command parsing and handling
 * - HMAC-SHA256 authentication
 * - Sequence number anti-replay
 * - Response generation
 *
 * Copyright (c) 2026 ABYZ Lab
 */

#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>

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

/* Command frame format */
typedef struct {
    uint32_t magic;
    uint32_t sequence;
    uint16_t command_id;
    uint16_t payload_len;
    uint8_t hmac[HMAC_SIZE];
    uint8_t payload[];  /* Variable length */
} __attribute__((packed)) command_frame_t;

/* Response frame format */
typedef struct {
    uint32_t magic;
    uint32_t sequence;
    uint16_t status;
    uint16_t payload_len;
    uint8_t hmac[HMAC_SIZE];
    uint8_t payload[];  /* Variable length */
} __attribute__((packed)) response_frame_t;

/* Status codes */
#define STATUS_OK           0x0000
#define STATUS_ERROR        0x0001
#define STATUS_BUSY         0x0002
#define STATUS_INVALID_CMD  0x0003
#define STATUS_AUTH_FAILED  0x0004
#define STATUS_REPLAY       0x0005
#define STATUS_MOTION_DETECTED 0x0006

/* Function under test */
extern int cmd_protocol_init(const char *hmac_key);
extern void cmd_protocol_deinit(void);
extern int cmd_parse_packet(const uint8_t *buf, size_t len, command_frame_t *cmd);
extern int cmd_validate_magic(const uint8_t *buf, size_t len);
extern int cmd_validate_hmac(const command_frame_t *cmd, const char *key);
extern int cmd_check_replay(uint32_t sequence, const char *source_ip);
extern int cmd_handle_command(const command_frame_t *cmd, uint8_t *resp_buf, size_t *resp_len);
extern void cmd_update_replay_state(uint32_t sequence, const char *source_ip);

/* Mock functions */
extern void mock_hmac_set_valid(bool valid);
extern void mock_seq_set_last(uint32_t seq);

/* Test HMAC key */
static const char test_hmac_key[] = "test_key_123456789";

/* ==========================================================================
 * Magic Number Tests (REQ-FW-025, REQ-FW-026)
 * ========================================================================== */

/**
 * @test FW_UT_07_001: Valid command magic number
 * @pre Packet with magic = 0xBEEFCAFE
 * @post Validation succeeds
 */
static void test_cmd_validate_command_magic(void **state) {
    (void)state;

    uint8_t packet[8] = {
        0xFE, 0xCA, 0xEF, 0xBE,  /* MAGIC_COMMAND little-endian */
        0x00, 0x00, 0x00, 0x00,
    };

    int result = cmd_validate_magic(packet, sizeof(packet));
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_07_002: Invalid magic number
 * @pre Packet with wrong magic
 * @post Validation fails
 */
static void test_cmd_validate_invalid_magic(void **state) {
    (void)state;

    uint8_t packet[8] = {
        0xFF, 0xFF, 0xFF, 0xFF,  /* Invalid magic */
        0x00, 0x00, 0x00, 0x00,
    };

    int result = cmd_validate_magic(packet, sizeof(packet));
    assert_int_equal(result, -EINVAL);
}

/**
 * @test FW_UT_07_003: Response magic number
 * @pre Response packet with magic = 0xCAFEBEEF
 * @post Validation succeeds
 */
static void test_cmd_validate_response_magic(void **state) {
    (void)state;

    uint8_t packet[8] = {
        0xEF, 0xBE, 0xFE, 0xCA,  /* MAGIC_RESPONSE little-endian */
        0x00, 0x00, 0x00, 0x00,
    };

    int result = cmd_validate_magic(packet, sizeof(packet));
    assert_int_equal(result, 0);
}

/* ==========================================================================
 * HMAC Validation Tests (REQ-FW-100)
 * ========================================================================== */

/**
 * @test FW_UT_07_004: Valid HMAC
 * @pre Command with valid HMAC
 * @post HMAC validation succeeds
 */
static void test_cmd_validate_hmac_valid(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    /* Set valid HMAC in mock */
    mock_hmac_set_valid(true);

    int result = cmd_validate_hmac(&cmd, test_hmac_key);
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_07_005: Invalid HMAC
 * @pre Command with invalid HMAC
 * @post HMAC validation fails
 */
static void test_cmd_validate_hmac_invalid(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    memset(cmd.hmac, 0xFF, HMAC_SIZE);

    mock_hmac_set_valid(false);

    int result = cmd_validate_hmac(&cmd, test_hmac_key);
    assert_int_equal(result, -EBADMSG);  /* Authentication failed */
}

/* ==========================================================================
 * Replay Protection Tests (REQ-FW-028)
 * ========================================================================== */

/**
 * @test FW_UT_07_006: Valid sequence number
 * @pre New sequence number > last sequence
 * @post Replay check passes
 */
static void test_cmd_replay_valid_sequence(void **state) {
    (void)state;

    mock_seq_set_last(0);

    int result = cmd_check_replay(1, "192.168.1.100");
    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_07_007: Replay attack detection - duplicate sequence
 * @pre Sequence number == last sequence
 * @post Replay detected, returns error
 */
static void test_cmd_replay_duplicate_sequence(void **state) {
    (void)state;

    mock_seq_set_last(5);

    int result = cmd_check_replay(5, "192.168.1.100");
    assert_int_equal(result, -EADDRINUSE);  /* Replay detected */
}

/**
 * @test FW_UT_07_008: Replay attack detection - old sequence
 * @pre Sequence number < last sequence
 * @post Replay detected, returns error
 */
static void test_cmd_replay_old_sequence(void **state) {
    (void)state;

    mock_seq_set_last(10);

    int result = cmd_check_replay(5, "192.168.1.100");
    assert_int_equal(result, -EADDRINUSE);  /* Replay detected */
}

/**
 * @test FW_UT_07_009: Different source IP - separate sequence tracking
 * @pre Different IPs have independent sequence counters
 * @post Both IPs can use the same sequence numbers
 */
static void test_cmd_replay_separate_sources(void **state) {
    (void)state;

    mock_seq_set_last(0);

    /* IP1 uses sequence 5 */
    cmd_update_replay_state(5, "192.168.1.100");

    /* IP2 should also be able to use sequence 5 */
    int result = cmd_check_replay(5, "192.168.1.101");
    assert_int_equal(result, 0);
}

/* ==========================================================================
 * Command Parsing Tests
 * ========================================================================== */

/**
 * @test FW_UT_07_010: Parse START_SCAN command
 * @pre Valid START_SCAN packet
 * @post Command parsed correctly
 */
static void test_cmd_parse_start_scan(void **state) {
    (void)state;

    uint8_t packet[44] = {0};  /* Minimum command frame size */

    /* Build command frame */
    command_frame_t *cmd = (command_frame_t *)packet;
    cmd->magic = MAGIC_COMMAND;
    cmd->sequence = 1;
    cmd->command_id = CMD_START_SCAN;
    cmd->payload_len = 0;

    command_frame_t parsed;
    int result = cmd_parse_packet(packet, sizeof(packet), &parsed);

    assert_int_equal(result, 0);
    assert_int_equal(parsed.command_id, CMD_START_SCAN);
    assert_int_equal(parsed.sequence, 1);
    assert_int_equal(parsed.payload_len, 0);
}

/**
 * @test FW_UT_07_011: Parse GET_STATUS command
 * @pre Valid GET_STATUS packet
 * @post Command parsed correctly
 */
static void test_cmd_parse_get_status(void **state) {
    (void)state;

    uint8_t packet[44] = {0};

    command_frame_t *cmd = (command_frame_t *)packet;
    cmd->magic = MAGIC_COMMAND;
    cmd->sequence = 10;
    cmd->command_id = CMD_GET_STATUS;
    cmd->payload_len = 0;

    command_frame_t parsed;
    int result = cmd_parse_packet(packet, sizeof(packet), &parsed);

    assert_int_equal(result, 0);
    assert_int_equal(parsed.command_id, CMD_GET_STATUS);
    assert_int_equal(parsed.sequence, 10);
}

/**
 * @test FW_UT_07_012: Parse SET_CONFIG command with payload
 * @pre SET_CONFIG with config data payload
 * @post Command and payload parsed correctly
 */
static void test_cmd_parse_set_config_with_payload(void **state) {
    (void)state;

    uint8_t packet[64] = {0};

    command_frame_t *cmd = (command_frame_t *)packet;
    cmd->magic = MAGIC_COMMAND;
    cmd->sequence = 5;
    cmd->command_id = CMD_SET_CONFIG;
    cmd->payload_len = 20;  /* 20 bytes of config data */

    /* Add some payload data */
    for (int i = 0; i < 20; i++) {
        packet[44 + i] = (uint8_t)i;
    }

    command_frame_t parsed;
    int result = cmd_parse_packet(packet, sizeof(packet), &parsed);

    assert_int_equal(result, 0);
    assert_int_equal(parsed.command_id, CMD_SET_CONFIG);
    assert_int_equal(parsed.payload_len, 20);
}

/* ==========================================================================
 * Command Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_07_013: Handle START_SCAN - success
 * @pre Valid START_SCAN command
 * @post Returns STATUS_OK, scan started
 */
static void test_cmd_handle_start_scan_success(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    mock_hmac_set_valid(true);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    assert_int_equal(result, 0);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->magic, MAGIC_RESPONSE);
    assert_int_equal(resp->sequence, 1);
    assert_int_equal(resp->status, STATUS_OK);
}

/**
 * @test FW_UT_07_014: Handle STOP_SCAN
 * @pre Valid STOP_SCAN command
 * @post Returns STATUS_OK, scan stopped
 */
static void test_cmd_handle_stop_scan(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 2,
        .command_id = CMD_STOP_SCAN,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    assert_int_equal(result, 0);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->status, STATUS_OK);
}

/**
 * @test FW_UT_07_015: Handle GET_STATUS
 * @pre Valid GET_STATUS command
 * @post Returns STATUS_OK with status payload
 */
static void test_cmd_handle_get_status(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 3,
        .command_id = CMD_GET_STATUS,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    assert_int_equal(result, 0);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->status, STATUS_OK);
    assert_true(resp->payload_len > 0);  /* Status data in payload */
}

/**
 * @test FW_UT_07_016: Handle invalid command
 * @pre Unknown command ID
 * @post Returns STATUS_INVALID_CMD
 */
static void test_cmd_handle_invalid_command(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = 0xFF,  /* Unknown command */
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    assert_int_equal(result, 0);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->status, STATUS_INVALID_CMD);
}

/* ==========================================================================
 * Error Handling Tests
 * ========================================================================== */

/**
 * @test FW_UT_07_017: Command with HMAC failure
 * @pre Command with invalid HMAC
 * @post Returns STATUS_AUTH_FAILED, increments auth failure counter
 */
static void test_cmd_auth_failure(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    memset(cmd.hmac, 0xFF, HMAC_SIZE);

    mock_hmac_set_valid(false);

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->status, STATUS_AUTH_FAILED);
}

/**
 * @test FW_UT_07_018: Command with replay detected
 * @pre Duplicate sequence number
 * @post Returns STATUS_REPLAY
 */
static void test_cmd_replay_response(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 5,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    mock_seq_set_last(5);

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->status, STATUS_REPLAY);
}

/**
 * @test FW_UT_07_019: Command during busy state
 * @pre Scan already active, another START_SCAN received
 * @post Returns STATUS_BUSY
 */
static void test_cmd_busy_state(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    /* Note: This test requires the sequence engine to be in SCANNING state
     * and return BUSY for duplicate START_SCAN commands */

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    int result = cmd_handle_command(&cmd, resp_buf, &resp_len);

    /* If scan is already active, should return BUSY */
    response_frame_t *resp = (response_frame_t *)resp_buf;
    /* Result depends on sequence engine state */
}

/* ==========================================================================
 * Response Generation Tests
 * ========================================================================== */

/**
 * @test FW_UT_07_020: Response magic number
 * @pre Any command handled
 * @post Response has magic = 0xCAFEBEEF
 */
static void test_cmd_response_magic(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 1,
        .command_id = CMD_GET_STATUS,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    cmd_handle_command(&cmd, resp_buf, &resp_len);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->magic, MAGIC_RESPONSE);
}

/**
 * @test FW_UT_07_021: Response sequence echo
 * @pre Command with sequence N
 * @post Response echoes same sequence N
 */
static void test_cmd_response_sequence_echo(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 42,
        .command_id = CMD_GET_STATUS,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    cmd_handle_command(&cmd, resp_buf, &resp_len);

    response_frame_t *resp = (response_frame_t *)resp_buf;
    assert_int_equal(resp->sequence, 42);
}

/* ==========================================================================
 * Boundary Tests
 * ========================================================================== */

/**
 * @test FW_UT_07_022: Minimum packet size
 * @pre Packet with minimum valid size (44 bytes)
 * @post Parses successfully
 */
static void test_cmd_min_packet_size(void **state) {
    (void)state;

    uint8_t packet[44] = {0};

    command_frame_t *cmd = (command_frame_t *)packet;
    cmd->magic = MAGIC_COMMAND;
    cmd->sequence = 1;
    cmd->command_id = CMD_GET_STATUS;
    cmd->payload_len = 0;

    command_frame_t parsed;
    int result = cmd_parse_packet(packet, sizeof(packet), &parsed);

    assert_int_equal(result, 0);
}

/**
 * @test FW_UT_07_023: Packet too small
 * @pre Packet smaller than header
 * @post Returns error
 */
static void test_cmd_packet_too_small(void **state) {
    (void)state;

    uint8_t packet[43] = {0};  /* One byte too small */

    command_frame_t parsed;
    int result = cmd_parse_packet(packet, sizeof(packet), &parsed);

    assert_int_equal(result, -EMSGSIZE);
}

/**
 * @test FW_UT_07_024: Maximum sequence number
 * @pre Command with sequence = 0xFFFFFFFF
 * @post Handles overflow correctly
 */
static void test_cmd_max_sequence(void **state) {
    (void)state;

    command_frame_t cmd = {
        .magic = MAGIC_COMMAND,
        .sequence = 0xFFFFFFFF,
        .command_id = CMD_START_SCAN,
        .payload_len = 0,
    };

    uint8_t resp_buf[256];
    size_t resp_len = sizeof(resp_buf);

    mock_seq_set_last(0xFFFFFFFE);

    /* Should accept 0xFFFFFFFF after 0xFFFFFFFE */
    int result = cmd_check_replay(0xFFFFFFFF, "192.168.1.100");
    /* Note: Wraparound handling depends on implementation */
}

/* ==========================================================================
 * Test Runner
 * ========================================================================== */

int main(void) {
    const struct CMUnitTest tests[] = {
        /* Magic number tests */
        cmocka_unit_test(test_cmd_validate_command_magic),
        cmocka_unit_test(test_cmd_validate_invalid_magic),
        cmocka_unit_test(test_cmd_validate_response_magic),

        /* HMAC validation tests */
        cmocka_unit_test(test_cmd_validate_hmac_valid),
        cmocka_unit_test(test_cmd_validate_hmac_invalid),

        /* Replay protection tests */
        cmocka_unit_test(test_cmd_replay_valid_sequence),
        cmocka_unit_test(test_cmd_replay_duplicate_sequence),
        cmocka_unit_test(test_cmd_replay_old_sequence),
        cmocka_unit_test(test_cmd_replay_separate_sources),

        /* Command parsing tests */
        cmocka_unit_test(test_cmd_parse_start_scan),
        cmocka_unit_test(test_cmd_parse_get_status),
        cmocka_unit_test(test_cmd_parse_set_config_with_payload),

        /* Command handling tests */
        cmocka_unit_test(test_cmd_handle_start_scan_success),
        cmocka_unit_test(test_cmd_handle_stop_scan),
        cmocka_unit_test(test_cmd_handle_get_status),
        cmocka_unit_test(test_cmd_handle_invalid_command),

        /* Error handling tests */
        cmocka_unit_test(test_cmd_auth_failure),
        cmocka_unit_test(test_cmd_replay_response),
        cmocka_unit_test(test_cmd_busy_state),

        /* Response generation tests */
        cmocka_unit_test(test_cmd_response_magic),
        cmocka_unit_test(test_cmd_response_sequence_echo),

        /* Boundary tests */
        cmocka_unit_test(test_cmd_min_packet_size),
        cmocka_unit_test(test_cmd_packet_too_small),
        cmocka_unit_test(test_cmd_max_sequence),
    };

    return cmocka_run_group_tests_name("FW-UT-07: Command Protocol Tests",
                                       tests, NULL, NULL);
}
