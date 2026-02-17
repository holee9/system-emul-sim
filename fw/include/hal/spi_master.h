/**
 * @file spi_master.h
 * @brief SPI Master HAL for FPGA register communication
 *
 * REQ-FW-020~023: SPI Master HAL for FPGA register read/write.
 * Uses Linux spidev interface for FPGA register access.
 *
 * Transaction Format:
 * - 8-bit address
 * - 8-bit R/W# bit (1=write, 0=read)
 * - 16-bit data
 *
 * Example: Write 0x1234 to register 0x20
 *   TX: [0x20, 0x80, 0x12, 0x34]  (addr=0x20, RW=1, data=0x1234)
 *   RX: [0x20, 0x80, 0x12, 0x34]  (echo back)
 */

#ifndef DETECTOR_HAL_SPI_MASTER_H
#define DETECTOR_HAL_SPI_MASTER_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief SPI configuration parameters
 */
typedef struct {
    const char *device;     /**< Spidev device path (e.g., "/dev/spidev0.0") */
    uint32_t speed;         /**< SPI clock speed in Hz (max 50 MHz) */
    uint8_t bits_per_word;  /**< Bits per word (typically 8) */
    uint8_t mode;           /**< SPI mode (0-3, typically 0 for FPGA) */
} spi_config_t;

/**
 * @brief SPI master handle (opaque)
 */
typedef struct spi_master spi_master_t;

/**
 * @brief SPI transaction result codes
 */
typedef enum {
    SPI_OK = 0,              /**< Success */
    SPI_ERROR_NULL = -1,     /**< NULL pointer argument */
    SPI_ERROR_OPEN = -2,     /**< Failed to open device */
    SPI_ERROR_IOCTL = -3,    /**< IOCTL configuration failed */
    SPI_ERROR_TRANSFER = -4, /**< SPI transfer failed */
    SPI_ERROR_VERIFY = -5,   /**< Write verification failed */
    SPI_ERROR_TIMEOUT = -6,  /**< Operation timeout */
    SPI_ERROR_CLOSED = -7    /**< Device not open */
} spi_status_t;

/**
 * @brief FPGA transaction format
 *
 * Format: [addr, rw, data_hi, data_lo]
 * - addr: 8-bit register address
 * - rw: 1 for write, 0 for read
 * - data_hi: high byte of 16-bit data
 * - data_lo: low byte of 16-bit data
 */
typedef struct __attribute__((packed)) {
    uint8_t addr;        /**< Register address (0-255) */
    uint8_t rw;          /**< Read/Write bit (1=write, 0=read) */
    uint8_t data_hi;     /**< Data high byte */
    uint8_t data_lo;     /**< Data low byte */
} fpga_transaction_t;

/* Default configuration */
#define SPI_DEFAULT_DEVICE      "/dev/spidev0.0"
#define SPI_DEFAULT_SPEED       50000000   /**< 50 MHz */
#define SPI_DEFAULT_BITS        8
#define SPI_DEFAULT_MODE        0
#define SPI_MAX_RETRY_COUNT     3
#define SPI_DEFAULT_TIMEOUT_MS  1000

/**
 * @brief Create and initialize SPI master
 *
 * @param config SPI configuration parameters
 * @return Pointer to SPI master handle, or NULL on error
 */
spi_master_t *spi_master_create(const spi_config_t *config);

/**
 * @brief Destroy and cleanup SPI master
 *
 * @param spi SPI master handle (can be NULL)
 */
void spi_master_destroy(spi_master_t *spi);

/**
 * @brief Write to FPGA register with read-back verification
 *
 * @param spi SPI master handle
 * @param addr Register address (0-255)
 * @param data 16-bit data to write
 * @return SPI_OK on success, error code on failure
 *
 * @note Performs read-back verification per REQ-FW-021
 * @note Retries up to SPI_MAX_RETRY_COUNT times on verification failure
 */
spi_status_t spi_write_register(spi_master_t *spi, uint8_t addr, uint16_t data);

/**
 * @brief Read from FPGA register
 *
 * @param spi SPI master handle
 * @param addr Register address (0-255)
 * @param data Pointer to store 16-bit read data
 * @return SPI_OK on success, error code on failure
 */
spi_status_t spi_read_register(spi_master_t *spi, uint8_t addr, uint16_t *data);

/**
 * @brief Write to FPGA register without verification
 *
 * @param spi SPI master handle
 * @param addr Register address (0-255)
 * @param data 16-bit data to write
 * @return SPI_OK on success, error code on failure
 *
 * @note Does not perform read-back verification
 * @note Faster than spi_write_register() but less safe
 */
spi_status_t spi_write_register_no_verify(spi_master_t *spi, uint8_t addr, uint16_t data);

/**
 * @brief Perform bulk read from multiple consecutive registers
 *
 * @param spi SPI master handle
 * @param start_addr Starting register address
 * @param buffer Buffer to store read data
 * @param count Number of registers to read
 * @return SPI_OK on success, error code on failure
 */
spi_status_t spi_read_bulk(spi_master_t *spi, uint8_t start_addr,
                          uint16_t *buffer, size_t count);

/**
 * @brief Perform bulk write to multiple consecutive registers
 *
 * @param spi SPI master handle
 * @param start_addr Starting register address
 * @param buffer Buffer containing data to write
 * @param count Number of registers to write
 * @return SPI_OK on success, error code on failure
 *
 * @note Verifies all writes after completion
 */
spi_status_t spi_write_bulk(spi_master_t *spi, uint8_t start_addr,
                           const uint16_t *buffer, size_t count);

/**
 * @brief Get last error message
 *
 * @param spi SPI master handle
 * @return Error message string, or NULL if no error
 */
const char *spi_get_error(spi_master_t *spi);

/**
 * @brief Get statistics about SPI operations
 *
 * @param spi SPI master handle
 * @param total_writes Pointer to store total write count
 * @param total_reads Pointer to store total read count
 * @param write_errors Pointer to store write error count
 * @param read_errors Pointer to store read error count
 * @return SPI_OK on success, error code on failure
 */
spi_status_t spi_get_stats(spi_master_t *spi,
                          uint32_t *total_writes,
                          uint32_t *total_reads,
                          uint32_t *write_errors,
                          uint32_t *read_errors);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_HAL_SPI_MASTER_H */
