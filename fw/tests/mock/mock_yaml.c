/**
 * @file mock_yaml.c
 * @brief Mock YAML functions for testing
 *
 * Provides mock implementations for YAML testing to allow
 * unit tests to run without actual YAML files.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <setjmp.h>
#include <cmocka.h>
#include <yaml.h>

/* Mock YAML content buffer */
static const char *mock_yaml_content = NULL;

/* Valid YAML configuration for testing */
static const char *default_valid_yaml =
    "# Detector Configuration\n"
    "panel:\n"
    "  rows: 2048\n"
    "  cols: 2048\n"
    "  bit_depth: 16\n"
    "\n"
    "timing:\n"
    "  frame_rate: 15\n"
    "  line_time_us: 50\n"
    "\n"
    "spi:\n"
    "  speed_hz: 50000000\n"
    "  mode: 0\n"
    "\n"
    "csi2:\n"
    "  lane_speed_mbps: 400\n"
    "  lanes: 4\n"
    "\n"
    "network:\n"
    "  host_ip: \"192.168.1.100\"\n"
    "  data_port: 8000\n"
    "  control_port: 8001\n"
    "  send_buffer_size: 16777216\n"
    "\n"
    "scan:\n"
    "  mode: continuous\n"
    "\n"
    "logging:\n"
    "  level: INFO\n";

/**
 * @brief Set mock YAML content for testing
 *
 * @param content YAML content to use (NULL for file not found simulation)
 *
 * @note This function overrides the file system for YAML loading.
 *       Tests can set custom YAML content or NULL to simulate errors.
 */
void mock_yaml_set_content(const char *content) {
    mock_yaml_content = content;
}

/**
 * @brief Get mock YAML content
 *
 * @return Current mock YAML content, or default if not set
 */
const char *mock_yaml_get_content(void) {
    if (mock_yaml_content != NULL) {
        return mock_yaml_content;
    }
    return default_valid_yaml;
}

/**
 * @brief Reset mock YAML content to default
 */
void mock_yaml_reset_content(void) {
    mock_yaml_content = NULL;
}
