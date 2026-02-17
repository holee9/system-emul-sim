/**
 * @file config_loader.h
 * @brief Configuration loader for detector configuration
 *
 * REQ-FW-003, REQ-FW-130~131: YAML configuration loading and validation.
 * Loads detector_config.yaml and validates parameter ranges.
 *
 * Features:
 * - YAML configuration parsing using libyaml
 * - Range validation for all parameters (REQ-FW-130)
 * - Hot/cold parameter classification (REQ-FW-131)
 * - Runtime parameter updates
 */

#ifndef DETECTOR_CONFIG_CONFIG_LOADER_H
#define DETECTOR_CONFIG_CONFIG_LOADER_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Detector configuration structure
 *
 * Contains all configurable parameters for the detector system.
 * Matches YAML structure in detector_config.yaml.
 */
typedef struct {
    /* Panel configuration */
    uint16_t rows;              /**< Panel rows (pixels) */
    uint16_t cols;              /**< Panel columns (pixels) */
    uint8_t bit_depth;          /**< Bits per pixel (14 or 16) */

    /* Timing configuration */
    uint16_t frame_rate;        /**< Frames per second (1-60) */
    uint32_t line_time_us;      /**< Line time in microseconds */
    uint32_t frame_time_us;     /**< Frame time in microseconds */

    /* SPI configuration */
    uint32_t spi_speed_hz;      /**< SPI clock speed in Hz (1M-50M) */
    uint8_t spi_mode;           /**< SPI mode (0-3) */

    /* CSI-2 configuration */
    uint32_t csi2_lane_speed_mbps; /**< CSI-2 lane speed in Mbps (400 or 800) */
    uint8_t csi2_lanes;         /**< Number of CSI-2 lanes (1-4) */

    /* Network configuration */
    char host_ip[16];           /**< Destination IP address */
    uint16_t data_port;         /**< Data port (1024-65535) */
    uint16_t control_port;      /**< Control port (1024-65535) */
    uint32_t send_buffer_size;  /**< Socket send buffer size */

    /* Scan mode */
    uint8_t scan_mode;          /**< 0=Single, 1=Continuous, 2=Calibration */

    /* Logging */
    uint8_t log_level;          /**< 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR */
} detector_config_t;

/**
 * @brief Configuration result codes
 */
typedef enum {
    CONFIG_OK = 0,              /**< Success */
    CONFIG_ERROR_NULL = -1,     /**< NULL pointer argument */
    CONFIG_ERROR_FILE = -2,     /**< File not found or unreadable */
    CONFIG_ERROR_PARSE = -3,    /**< YAML parsing error */
    CONFIG_ERROR_VALIDATE = -4, /**< Validation failed */
    CONFIG_ERROR_MEMORY = -5,   /**< Memory allocation failed */
    CONFIG_ERROR_PARAM = -6     /**< Invalid parameter */
} config_status_t;

/**
 * @brief Parameter type for hot/cold classification
 */
typedef enum {
    PARAM_TYPE_HOT = 0,         /**< Can be changed during operation */
    PARAM_TYPE_COLD = 1,        /**< Requires scan stop */
    PARAM_TYPE_UNKNOWN = 2      /**< Unrecognized parameter */
} param_type_t;

/* Validation ranges (REQ-FW-130) */
#define CONFIG_MIN_ROWS          128
#define CONFIG_MAX_ROWS          4096
#define CONFIG_MIN_COLS          128
#define CONFIG_MAX_COLS          4096
#define CONFIG_VALID_BIT_DEPTH_14  14
#define CONFIG_VALID_BIT_DEPTH_16  16
#define CONFIG_MIN_FRAME_RATE    1
#define CONFIG_MAX_FRAME_RATE    60
#define CONFIG_MIN_SPI_SPEED_HZ  1000000    /**< 1 MHz */
#define CONFIG_MAX_SPI_SPEED_HZ  50000000   /**< 50 MHz */
#define CONFIG_MIN_PORT          1024
#define CONFIG_MAX_PORT          65535
#define CONFIG_MIN_CSI2_LANES    1
#define CONFIG_MAX_CSI2_LANES    4
#define CONFIG_VALID_CSI2_SPEED_400  400
#define CONFIG_VALID_CSI2_SPEED_800  800

/**
 * @brief Load configuration from YAML file
 *
 * @param filename Path to YAML configuration file
 * @param config Pointer to store loaded configuration
 * @return CONFIG_OK on success, error code on failure
 *
 * @note Per REQ-FW-003: Load from detector_config.yaml
 * @note Parses YAML using libyaml parser
 * @note Validates loaded configuration
 */
config_status_t config_load(const char *filename, detector_config_t *config);

/**
 * @brief Validate configuration parameters
 *
 * @param config Configuration to validate
 * @return CONFIG_OK if valid, CONFIG_ERROR_VALIDATE if invalid
 *
 * @note Per REQ-FW-130: Range validation for all parameters
 * @note Checks: resolution (128-4096), bit_depth (14 or 16),
 *       frame_rate (1-60), spi_speed_hz (1M-50M), ports (1024-65535)
 */
config_status_t config_validate(const detector_config_t *config);

/**
 * @brief Check if parameter is hot-swappable
 *
 * @param param_name Parameter name to check
 * @return true if hot-swappable, false if cold or unknown
 *
 * @note Per REQ-FW-131: Hot/cold parameter classification
 * @note Hot parameters: frame_rate, host_ip, data_port, control_port, log_level
 * @note Cold parameters: rows, cols, bit_depth, csi2_lane_speed_mbps, csi2_lanes
 */
bool config_is_hot_swappable(const char *param_name);

/**
 * @brief Set configuration parameter at runtime
 *
 * @param config Configuration to modify
 * @param key Parameter name
 * @param value New value (type depends on parameter)
 * @return CONFIG_OK on success, error code on failure
 *
 * @note Only hot-swappable parameters can be set at runtime
 * @note Cold parameters will return CONFIG_ERROR_PARAM
 */
config_status_t config_set(detector_config_t *config, const char *key, const void *value);

/**
 * @brief Free configuration resources
 *
 * @param config Configuration to cleanup
 *
 * @note Currently no dynamic allocation, but provided for future use
 */
void config_cleanup(detector_config_t *config);

/**
 * @brief Get default configuration
 *
 * @param config Pointer to store default configuration
 * @return CONFIG_OK on success
 *
 * @note Provides safe default values for all parameters
 */
config_status_t config_get_defaults(detector_config_t *config);

/**
 * @brief Get last error message
 *
 * @return Error message string, or NULL if no error
 *
 * @note Thread-local storage for error messages
 */
const char *config_get_error(void);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_CONFIG_CONFIG_LOADER_H */
