/**
 * @file config_loader.c
 * @brief Configuration loader implementation
 *
 * REQ-FW-003, REQ-FW-130~131: YAML configuration loading and validation.
 *
 * TDD Methodology:
 * - RED: Tests define expected behavior (test_config_loader.c)
 * - GREEN: Implementation satisfies tests
 * - REFACTOR: Code improvements while maintaining tests
 */

#include "config/config_loader.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <stdarg.h>
#include <yaml.h>

/* External mock YAML support for testing */
#ifdef CONFIG_LOADER_MOCK_MODE
extern const char *mock_yaml_get_content(void);
#endif

/**
 * @brief Thread-local error message storage
 */
static __thread char s_error_msg[256];

/**
 * @brief Set error message
 */
static void config_set_error(const char *format, ...) {
    va_list args;
    va_start(args, format);
    vsnprintf(s_error_msg, sizeof(s_error_msg), format, args);
    va_end(args);
}

/**
 * @brief Parse scalar value from YAML node
 */
static config_status_t parse_scalar(yaml_node_t *node, const char **value) {
    if (node == NULL || node->type != YAML_SCALAR_NODE) {
        return CONFIG_ERROR_PARSE;
    }
    *value = (const char *)node->data.scalar.value;
    return CONFIG_OK;
}

/**
 * @brief Parse integer value from YAML node
 */
static config_status_t parse_int(yaml_node_t *node, int *value) {
    if (node == NULL || node->type != YAML_SCALAR_NODE) {
        return CONFIG_ERROR_PARSE;
    }

    const char *str = (const char *)node->data.scalar.value;
    char *endptr;
    long val = strtol(str, &endptr, 10);

    if (*endptr != '\0') {
        return CONFIG_ERROR_PARSE;
    }

    *value = (int)val;
    return CONFIG_OK;
}

/**
 * @brief Parse string value into config field
 */
static config_status_t parse_string(yaml_node_t *node, char *buffer, size_t buffer_size) {
    if (node == NULL || node->type != YAML_SCALAR_NODE) {
        return CONFIG_ERROR_PARSE;
    }

    const char *str = (const char *)node->data.scalar.value;
    strncpy(buffer, str, buffer_size - 1);
    buffer[buffer_size - 1] = '\0';

    return CONFIG_OK;
}

/**
 * @brief Parse scan mode string
 */
static config_status_t parse_scan_mode(const char *str, uint8_t *mode) {
    if (strcmp(str, "single") == 0 || strcmp(str, "Single") == 0) {
        *mode = 0;
    } else if (strcmp(str, "continuous") == 0 || strcmp(str, "Continuous") == 0) {
        *mode = 1;
    } else if (strcmp(str, "calibration") == 0 || strcmp(str, "Calibration") == 0) {
        *mode = 2;
    } else {
        return CONFIG_ERROR_PARSE;
    }
    return CONFIG_OK;
}

/**
 * @brief Parse log level string
 */
static config_status_t parse_log_level(const char *str, uint8_t *level) {
    if (strcmp(str, "DEBUG") == 0 || strcmp(str, "debug") == 0) {
        *level = 0;
    } else if (strcmp(str, "INFO") == 0 || strcmp(str, "info") == 0) {
        *level = 1;
    } else if (strcmp(str, "WARN") == 0 || strcmp(str, "warn") == 0) {
        *level = 2;
    } else if (strcmp(str, "ERROR") == 0 || strcmp(str, "error") == 0) {
        *level = 3;
    } else {
        return CONFIG_ERROR_PARSE;
    }
    return CONFIG_OK;
}

/* ==========================================================================
 * Public API Implementation
 * ========================================================================== */

config_status_t config_load(const char *filename, detector_config_t *config) {
    if (filename == NULL || config == NULL) {
        config_set_error("NULL parameter");
        return CONFIG_ERROR_NULL;
    }

    /* Initialize config to defaults */
    memset(config, 0, sizeof(detector_config_t));

    /* Open YAML file */
    FILE *fh = NULL;

#ifdef CONFIG_LOADER_MOCK_MODE
    /* Use mock YAML content if available */
    const char *mock_content = mock_yaml_get_content();
    if (mock_content != NULL && strcmp(filename, "detector_config.yaml") == 0) {
        /* Create a FILE* from string (using fmemopen if available) */
        fh = fmemopen((void *)mock_content, strlen(mock_content), "r");
    } else {
        fh = fopen(filename, "r");
    }
#else
    fh = fopen(filename, "r");
#endif

    if (fh == NULL) {
        config_set_error("Failed to open file: %s", strerror(errno));
        return CONFIG_ERROR_FILE;
    }

    /* Initialize YAML parser */
    yaml_parser_t parser;
    if (!yaml_parser_initialize(&parser)) {
        config_set_error("Failed to initialize YAML parser");
        fclose(fh);
        return CONFIG_ERROR_PARSE;
    }

    yaml_parser_set_input_file(&parser, fh);

    /* Parse YAML document */
    yaml_document_t document;
    if (!yaml_parser_load(&parser, &document)) {
        config_set_error("YAML parse error at line %d: %s",
                        (int)parser.problem_mark.line + 1,
                        parser.problem ? parser.problem : "unknown error");
        yaml_parser_delete(&parser);
        fclose(fh);
        return CONFIG_ERROR_PARSE;
    }

    /* Get root node */
    yaml_node_t *root = yaml_document_get_root_node(&document);
    if (root == NULL || root->type != YAML_MAPPING_NODE) {
        config_set_error("YAML root is not a mapping");
        yaml_document_delete(&document);
        yaml_parser_delete(&parser);
        fclose(fh);
        return CONFIG_ERROR_PARSE;
    }

    /* Parse configuration sections */
    yaml_node_pair_t *pair = root->data.mapping.pairs.start;
    yaml_node_pair_t *pair_end = root->data.mapping.pairs.top;

    for (; pair < pair_end; pair++) {
        yaml_node_t *key_node = yaml_document_get_node(&document, pair->key);
        yaml_node_t *value_node = yaml_document_get_node(&document, pair->value);

        if (key_node == NULL || value_node == NULL ||
            key_node->type != YAML_SCALAR_NODE ||
            value_node->type != YAML_MAPPING_NODE) {
            continue;
        }

        const char *section = (const char *)key_node->data.scalar.value;

        /* Parse panel section */
        if (strcmp(section, "panel") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "rows") == 0) {
                    parse_int(field_value, (int *)&config->rows);
                } else if (strcmp(field, "cols") == 0) {
                    parse_int(field_value, (int *)&config->cols);
                } else if (strcmp(field, "bit_depth") == 0) {
                    parse_int(field_value, (int *)&config->bit_depth);
                }
            }
        }
        /* Parse timing section */
        else if (strcmp(section, "timing") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "frame_rate") == 0) {
                    parse_int(field_value, (int *)&config->frame_rate);
                } else if (strcmp(field, "line_time_us") == 0) {
                    parse_int(field_value, (int *)&config->line_time_us);
                }
            }
        }
        /* Parse SPI section */
        else if (strcmp(section, "spi") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "speed_hz") == 0) {
                    parse_int(field_value, (int *)&config->spi_speed_hz);
                } else if (strcmp(field, "mode") == 0) {
                    parse_int(field_value, (int *)&config->spi_mode);
                }
            }
        }
        /* Parse CSI-2 section */
        else if (strcmp(section, "csi2") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "lane_speed_mbps") == 0) {
                    parse_int(field_value, (int *)&config->csi2_lane_speed_mbps);
                } else if (strcmp(field, "lanes") == 0) {
                    parse_int(field_value, (int *)&config->csi2_lanes);
                }
            }
        }
        /* Parse network section */
        else if (strcmp(section, "network") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "host_ip") == 0) {
                    parse_string(field_value, config->host_ip, sizeof(config->host_ip));
                } else if (strcmp(field, "data_port") == 0) {
                    parse_int(field_value, (int *)&config->data_port);
                } else if (strcmp(field, "control_port") == 0) {
                    parse_int(field_value, (int *)&config->control_port);
                } else if (strcmp(field, "send_buffer_size") == 0) {
                    parse_int(field_value, (int *)&config->send_buffer_size);
                }
            }
        }
        /* Parse scan section */
        else if (strcmp(section, "scan") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "mode") == 0) {
                    const char *mode_str;
                    if (parse_scalar(field_value, &mode_str) == CONFIG_OK) {
                        parse_scan_mode(mode_str, &config->scan_mode);
                    }
                }
            }
        }
        /* Parse logging section */
        else if (strcmp(section, "logging") == 0) {
            yaml_node_pair_t *item = value_node->data.mapping.pairs.start;
            yaml_node_pair_t *item_end = value_node->data.mapping.pairs.top;

            for (; item < item_end; item++) {
                yaml_node_t *field_key = yaml_document_get_node(&document, item->key);
                yaml_node_t *field_value = yaml_document_get_node(&document, item->value);

                if (field_key == NULL || field_value == NULL ||
                    field_key->type != YAML_SCALAR_NODE ||
                    field_value->type != YAML_SCALAR_NODE) {
                    continue;
                }

                const char *field = (const char *)field_key->data.scalar.value;

                if (strcmp(field, "level") == 0) {
                    const char *level_str;
                    if (parse_scalar(field_value, &level_str) == CONFIG_OK) {
                        parse_log_level(level_str, &config->log_level);
                    }
                }
            }
        }
    }

    /* Cleanup */
    yaml_document_delete(&document);
    yaml_parser_delete(&parser);
    fclose(fh);

    /* Validate loaded configuration */
    return config_validate(config);
}

config_status_t config_validate(const detector_config_t *config) {
    if (config == NULL) {
        config_set_error("NULL config pointer");
        return CONFIG_ERROR_NULL;
    }

    /* Validate panel resolution (REQ-FW-130) */
    if (config->rows < CONFIG_MIN_ROWS || config->rows > CONFIG_MAX_ROWS) {
        config_set_error("rows out of range: %d (valid: %d-%d)",
                        config->rows, CONFIG_MIN_ROWS, CONFIG_MAX_ROWS);
        return CONFIG_ERROR_VALIDATE;
    }

    if (config->cols < CONFIG_MIN_COLS || config->cols > CONFIG_MAX_COLS) {
        config_set_error("cols out of range: %d (valid: %d-%d)",
                        config->cols, CONFIG_MIN_COLS, CONFIG_MAX_COLS);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate bit depth (REQ-FW-130) */
    if (config->bit_depth != CONFIG_VALID_BIT_DEPTH_14 &&
        config->bit_depth != CONFIG_VALID_BIT_DEPTH_16) {
        config_set_error("bit_depth invalid: %d (valid: 14 or 16)", config->bit_depth);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate frame rate (REQ-FW-130) */
    if (config->frame_rate < CONFIG_MIN_FRAME_RATE ||
        config->frame_rate > CONFIG_MAX_FRAME_RATE) {
        config_set_error("frame_rate out of range: %d (valid: %d-%d)",
                        config->frame_rate, CONFIG_MIN_FRAME_RATE, CONFIG_MAX_FRAME_RATE);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate SPI speed (REQ-FW-130) */
    if (config->spi_speed_hz < CONFIG_MIN_SPI_SPEED_HZ ||
        config->spi_speed_hz > CONFIG_MAX_SPI_SPEED_HZ) {
        config_set_error("spi_speed_hz out of range: %u (valid: %d-%d)",
                        config->spi_speed_hz, CONFIG_MIN_SPI_SPEED_HZ, CONFIG_MAX_SPI_SPEED_HZ);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate network ports (REQ-FW-130) */
    if (config->data_port < CONFIG_MIN_PORT || config->data_port > CONFIG_MAX_PORT) {
        config_set_error("data_port out of range: %d (valid: %d-%d)",
                        config->data_port, CONFIG_MIN_PORT, CONFIG_MAX_PORT);
        return CONFIG_ERROR_VALIDATE;
    }

    if (config->control_port < CONFIG_MIN_PORT || config->control_port > CONFIG_MAX_PORT) {
        config_set_error("control_port out of range: %d (valid: %d-%d)",
                        config->control_port, CONFIG_MIN_PORT, CONFIG_MAX_PORT);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate CSI-2 lanes */
    if (config->csi2_lanes < CONFIG_MIN_CSI2_LANES ||
        config->csi2_lanes > CONFIG_MAX_CSI2_LANES) {
        config_set_error("csi2_lanes out of range: %d (valid: %d-%d)",
                        config->csi2_lanes, CONFIG_MIN_CSI2_LANES, CONFIG_MAX_CSI2_LANES);
        return CONFIG_ERROR_VALIDATE;
    }

    /* Validate CSI-2 lane speed */
    if (config->csi2_lane_speed_mbps != CONFIG_VALID_CSI2_SPEED_400 &&
        config->csi2_lane_speed_mbps != CONFIG_VALID_CSI2_SPEED_800) {
        config_set_error("csi2_lane_speed_mbps invalid: %u (valid: 400 or 800)",
                        config->csi2_lane_speed_mbps);
        return CONFIG_ERROR_VALIDATE;
    }

    return CONFIG_OK;
}

bool config_is_hot_swappable(const char *param_name) {
    if (param_name == NULL) {
        return false;
    }

    /* Hot-swappable parameters (REQ-FW-131) */
    static const char *hot_params[] = {
        "frame_rate",
        "host_ip",
        "data_port",
        "control_port",
        "log_level",
        NULL
    };

    for (int i = 0; hot_params[i] != NULL; i++) {
        if (strcmp(param_name, hot_params[i]) == 0) {
            return true;
        }
    }

    return false;
}

config_status_t config_set(detector_config_t *config, const char *key, const void *value) {
    if (config == NULL || key == NULL || value == NULL) {
        config_set_error("NULL parameter");
        return CONFIG_ERROR_NULL;
    }

    /* Check if parameter is hot-swappable (REQ-FW-131) */
    if (!config_is_hot_swappable(key)) {
        config_set_error("Parameter '%s' is not hot-swappable", key);
        return CONFIG_ERROR_PARAM;
    }

    /* Set parameter value */
    if (strcmp(key, "frame_rate") == 0) {
        uint16_t new_rate = *(const uint16_t *)value;
        if (new_rate >= CONFIG_MIN_FRAME_RATE && new_rate <= CONFIG_MAX_FRAME_RATE) {
            config->frame_rate = new_rate;
            return CONFIG_OK;
        }
        return CONFIG_ERROR_VALIDATE;
    } else if (strcmp(key, "host_ip") == 0) {
        strncpy(config->host_ip, (const char *)value, sizeof(config->host_ip) - 1);
        config->host_ip[sizeof(config->host_ip) - 1] = '\0';
        return CONFIG_OK;
    } else if (strcmp(key, "data_port") == 0) {
        uint16_t new_port = *(const uint16_t *)value;
        if (new_port >= CONFIG_MIN_PORT && new_port <= CONFIG_MAX_PORT) {
            config->data_port = new_port;
            return CONFIG_OK;
        }
        return CONFIG_ERROR_VALIDATE;
    } else if (strcmp(key, "control_port") == 0) {
        uint16_t new_port = *(const uint16_t *)value;
        if (new_port >= CONFIG_MIN_PORT && new_port <= CONFIG_MAX_PORT) {
            config->control_port = new_port;
            return CONFIG_OK;
        }
        return CONFIG_ERROR_VALIDATE;
    } else if (strcmp(key, "log_level") == 0) {
        config->log_level = *(const uint8_t *)value;
        return CONFIG_OK;
    }

    config_set_error("Unknown parameter: %s", key);
    return CONFIG_ERROR_PARAM;
}

void config_cleanup(detector_config_t *config) {
    /* No dynamic allocation currently, but provided for future use */
    (void)config;
}

config_status_t config_get_defaults(detector_config_t *config) {
    if (config == NULL) {
        config_set_error("NULL config pointer");
        return CONFIG_ERROR_NULL;
    }

    memset(config, 0, sizeof(detector_config_t));

    /* Panel defaults */
    config->rows = 2048;
    config->cols = 2048;
    config->bit_depth = 16;

    /* Timing defaults */
    config->frame_rate = 15;
    config->line_time_us = 50;
    config->frame_time_us = 66667;

    /* SPI defaults */
    config->spi_speed_hz = 50000000;
    config->spi_mode = 0;

    /* CSI-2 defaults */
    config->csi2_lane_speed_mbps = 400;
    config->csi2_lanes = 4;

    /* Network defaults */
    strncpy(config->host_ip, "192.168.1.100", sizeof(config->host_ip) - 1);
    config->data_port = 8000;
    config->control_port = 8001;
    config->send_buffer_size = 16777216;

    /* Scan defaults */
    config->scan_mode = 1;  /* Continuous */

    /* Logging defaults */
    config->log_level = 1;  /* INFO */

    return CONFIG_OK;
}

const char *config_get_error(void) {
    return s_error_msg;
}
