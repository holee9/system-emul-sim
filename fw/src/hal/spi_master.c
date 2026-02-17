/**
 * @file spi_master.c
 * @brief SPI Master HAL for FPGA register communication
 *
 * REQ-FW-020~023: SPI Master HAL for FPGA register read/write.
 * Uses Linux spidev interface for FPGA register access.
 *
 * Transaction Format (32-bit):
 * - Byte 0: Register address (7:0)
 * - Byte 1: R/W# bit (0x00=write, 0x80=read)
 * - Byte 2: Data high byte
 * - Byte 3: Data low byte
 *
 * Example: Write 0x1234 to register 0x20
 *   TX: [0x20, 0x00, 0x12, 0x34]  (addr=0x20, WRITE=0x00, data=0x1234)
 *   RX: [don't care for write]
 *
 * Example: Read from register 0x20
 *   TX: [0x20, 0x80, 0x00, 0x00]  (addr=0x20, READ=0x80, dummy data)
 *   RX: [0x20, 0x80, 0x12, 0x34]  (FPGA echoes address + returns data)
 */

#include "hal/spi_master.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/ioctl.h>
#include <linux/spi/spidev.h>

/**
 * @brief SPI master internal state
 */
struct spi_master {
    int fd;                     /**< File descriptor */
    spi_config_t config;        /**< Configuration */
    char error_msg[256];        /**< Last error message */

    /* Statistics */
    uint32_t total_writes;
    uint32_t total_reads;
    uint32_t write_errors;
    uint32_t read_errors;
};

/* Forward declarations for legacy test compatibility */
static spi_master_t *g_spi = NULL;

/**
 * @brief Convert error code to string
 */
static const char* spi_error_string(spi_status_t status) {
    switch (status) {
        case SPI_OK:             return "Success";
        case SPI_ERROR_NULL:     return "NULL pointer argument";
        case SPI_ERROR_OPEN:     return "Failed to open device";
        case SPI_ERROR_IOCTL:    return "IOCTL configuration failed";
        case SPI_ERROR_TRANSFER: return "SPI transfer failed";
        case SPI_ERROR_VERIFY:   return "Write verification failed";
        case SPI_ERROR_TIMEOUT:  return "Operation timeout";
        case SPI_ERROR_CLOSED:   return "Device not open";
        default:                 return "Unknown error";
    }
}

/**
 * @brief Set error message
 */
static void spi_set_error(spi_master_t *spi, spi_status_t status, const char *detail) {
    if (spi == NULL) return;

    snprintf(spi->error_msg, sizeof(spi->error_msg), "%s: %s",
             spi_error_string(status), detail ? detail : "");
}

/* ==========================================================================
 * Public API Implementation
 * ========================================================================== */

spi_master_t *spi_master_create(const spi_config_t *config) {
    if (config == NULL || config->device == NULL) {
        return NULL;
    }

    spi_master_t *spi = (spi_master_t *)calloc(1, sizeof(spi_master_t));
    if (spi == NULL) {
        return NULL;
    }

    /* Copy configuration */
    spi->config = *config;
    spi->fd = -1;

    /* Open SPI device */
    spi->fd = open(config->device, O_RDWR);
    if (spi->fd < 0) {
        spi_set_error(spi, SPI_ERROR_OPEN, strerror(errno));
        free(spi);
        return NULL;
    }

    /* Configure SPI mode */
    uint8_t mode = config->mode;
    if (ioctl(spi->fd, SPI_IOC_WR_MODE, &mode) < 0) {
        spi_set_error(spi, SPI_ERROR_IOCTL, "Failed to set mode");
        close(spi->fd);
        free(spi);
        return NULL;
    }

    /* Configure bits per word */
    uint8_t bits = config->bits_per_word;
    if (ioctl(spi->fd, SPI_IOC_WR_BITS_PER_WORD, &bits) < 0) {
        spi_set_error(spi, SPI_ERROR_IOCTL, "Failed to set bits per word");
        close(spi->fd);
        free(spi);
        return NULL;
    }

    /* Configure speed */
    uint32_t speed = config->speed;
    if (ioctl(spi->fd, SPI_IOC_WR_MAX_SPEED_HZ, &speed) < 0) {
        spi_set_error(spi, SPI_ERROR_IOCTL, "Failed to set speed");
        close(spi->fd);
        free(spi);
        return NULL;
    }

    /* Initialize statistics */
    spi->total_writes = 0;
    spi->total_reads = 0;
    spi->write_errors = 0;
    spi->read_errors = 0;

    return spi;
}

void spi_master_destroy(spi_master_t *spi) {
    if (spi == NULL) return;

    if (spi->fd >= 0) {
        close(spi->fd);
    }

    free(spi);
}

spi_status_t spi_write_register(spi_master_t *spi, uint8_t addr, uint16_t data) {
    if (spi == NULL) return SPI_ERROR_NULL;
    if (spi->fd < 0) return SPI_ERROR_CLOSED;

    /* Validate address (7-bit address space) */
    if (addr > 0x7F) {
        spi_set_error(spi, SPI_ERROR_NULL, "Invalid address");
        return SPI_ERROR_NULL;
    }

    spi_status_t status;
    int retry_count = 0;

    /* Write with verification retry loop */
    do {
        /* Write the register */
        status = spi_write_register_no_verify(spi, addr, data);
        if (status != SPI_OK) {
            spi->write_errors++;
            return status;
        }

        /* Verify by reading back */
        uint16_t read_back;
        status = spi_read_register(spi, addr, &read_back);
        if (status != SPI_OK) {
            spi->read_errors++;
            retry_count++;
            continue;
        }

        /* Check if verification succeeded */
        if (read_back == data) {
            spi->total_writes++;
            return SPI_OK;
        }

        retry_count++;

    } while (retry_count < SPI_MAX_RETRY_COUNT);

    /* All retries failed */
    spi_set_error(spi, SPI_ERROR_VERIFY, "Max retries exceeded");
    return SPI_ERROR_VERIFY;
}

spi_status_t spi_read_register(spi_master_t *spi, uint8_t addr, uint16_t *data) {
    if (spi == NULL || data == NULL) return SPI_ERROR_NULL;
    if (spi->fd < 0) return SPI_ERROR_CLOSED;

    /* Validate address (7-bit address space) */
    if (addr > 0x7F) {
        spi_set_error(spi, SPI_ERROR_NULL, "Invalid address");
        return SPI_ERROR_NULL;
    }

    /* Build read transaction */
    /* Format: [addr, READ(0x80), dummy, dummy] */
    uint8_t tx_buf[4] = {
        addr,           /* Register address */
        0x80,           /* READ command */
        0x00,           /* Dummy byte */
        0x00            /* Dummy byte */
    };

    uint8_t rx_buf[4] = {0};

    /* Prepare SPI transfer */
    struct spi_ioc_transfer tr = {
        .tx_buf = (unsigned long)tx_buf,
        .rx_buf = (unsigned long)rx_buf,
        .len = 4,
        .speed_hz = spi->config.speed,
        .bits_per_word = spi->config.bits_per_word,
        .delay_usecs = 0,
    };

    /* Execute SPI transfer */
    int ret = ioctl(spi->fd, SPI_IOC_MESSAGE(1), &tr);
    if (ret < 0) {
        spi_set_error(spi, SPI_ERROR_TRANSFER, strerror(errno));
        spi->read_errors++;
        return SPI_ERROR_TRANSFER;
    }

    /* Extract data from RX buffer */
    /* Response format: [addr, 0x80, data_hi, data_lo] */
    *data = ((uint16_t)rx_buf[2] << 8) | rx_buf[3];

    spi->total_reads++;
    return SPI_OK;
}

spi_status_t spi_write_register_no_verify(spi_master_t *spi, uint8_t addr, uint16_t data) {
    if (spi == NULL) return SPI_ERROR_NULL;
    if (spi->fd < 0) return SPI_ERROR_CLOSED;

    /* Validate address (7-bit address space) */
    if (addr > 0x7F) {
        spi_set_error(spi, SPI_ERROR_NULL, "Invalid address");
        return SPI_ERROR_NULL;
    }

    /* Build write transaction */
    /* Format: [addr, WRITE(0x00), data_hi, data_lo] */
    uint8_t tx_buf[4] = {
        addr,                   /* Register address */
        0x00,                   /* WRITE command */
        (data >> 8) & 0xFF,     /* Data high byte */
        data & 0xFF             /* Data low byte */
    };

    uint8_t rx_buf[4] = {0};

    /* Prepare SPI transfer */
    struct spi_ioc_transfer tr = {
        .tx_buf = (unsigned long)tx_buf,
        .rx_buf = (unsigned long)rx_buf,
        .len = 4,
        .speed_hz = spi->config.speed,
        .bits_per_word = spi->config.bits_per_word,
        .delay_usecs = 0,
    };

    /* Execute SPI transfer */
    int ret = ioctl(spi->fd, SPI_IOC_MESSAGE(1), &tr);
    if (ret < 0) {
        spi_set_error(spi, SPI_ERROR_TRANSFER, strerror(errno));
        return SPI_ERROR_TRANSFER;
    }

    return SPI_OK;
}

spi_status_t spi_read_bulk(spi_master_t *spi, uint8_t start_addr,
                          uint16_t *buffer, size_t count) {
    if (spi == NULL || buffer == NULL) return SPI_ERROR_NULL;
    if (spi->fd < 0) return SPI_ERROR_CLOSED;
    if (count == 0) return SPI_OK;

    /* Validate start address */
    if (start_addr > 0x7F) {
        spi_set_error(spi, SPI_ERROR_NULL, "Invalid start address");
        return SPI_ERROR_NULL;
    }

    /* Check for address overflow */
    if ((start_addr + count) > 0x80) {
        spi_set_error(spi, SPI_ERROR_NULL, "Address range overflow");
        return SPI_ERROR_NULL;
    }

    /* Read each register */
    for (size_t i = 0; i < count; i++) {
        spi_status_t status = spi_read_register(spi, start_addr + i, &buffer[i]);
        if (status != SPI_OK) {
            return status;
        }
    }

    return SPI_OK;
}

spi_status_t spi_write_bulk(spi_master_t *spi, uint8_t start_addr,
                           const uint16_t *buffer, size_t count) {
    if (spi == NULL || buffer == NULL) return SPI_ERROR_NULL;
    if (spi->fd < 0) return SPI_ERROR_CLOSED;
    if (count == 0) return SPI_OK;

    /* Validate start address */
    if (start_addr > 0x7F) {
        spi_set_error(spi, SPI_ERROR_NULL, "Invalid start address");
        return SPI_ERROR_NULL;
    }

    /* Check for address overflow */
    if ((start_addr + count) > 0x80) {
        spi_set_error(spi, SPI_ERROR_NULL, "Address range overflow");
        return SPI_ERROR_NULL;
    }

    /* Write each register with verification */
    for (size_t i = 0; i < count; i++) {
        spi_status_t status = spi_write_register(spi, start_addr + i, buffer[i]);
        if (status != SPI_OK) {
            return status;
        }
    }

    return SPI_OK;
}

const char *spi_get_error(spi_master_t *spi) {
    if (spi == NULL) return "NULL SPI handle";
    return spi->error_msg;
}

spi_status_t spi_get_stats(spi_master_t *spi,
                          uint32_t *total_writes,
                          uint32_t *total_reads,
                          uint32_t *write_errors,
                          uint32_t *read_errors) {
    if (spi == NULL) return SPI_ERROR_NULL;

    if (total_writes) *total_writes = spi->total_writes;
    if (total_reads) *total_reads = spi->total_reads;
    if (write_errors) *write_errors = spi->write_errors;
    if (read_errors) *read_errors = spi->read_errors;

    return SPI_OK;
}

/* ==========================================================================
 * Legacy API Compatibility (for existing tests)
 * ==========================================================================
 *
 * These functions provide backward compatibility with the original test
 * interface that used global state. They internally use the new API.
 */

/**
 * @brief Initialize SPI with default configuration
 *
 * Legacy API for test compatibility. Uses default spidev0.0 at 50 MHz.
 */
int fpga_spi_init(const char *device) {
    if (device == NULL) {
        device = SPI_DEFAULT_DEVICE;
    }

    /* Destroy existing instance if any */
    if (g_spi != NULL) {
        fpga_spi_deinit();
    }

    /* Create with default configuration */
    spi_config_t config = {
        .device = device,
        .speed = SPI_DEFAULT_SPEED,
        .bits_per_word = SPI_DEFAULT_BITS,
        .mode = SPI_DEFAULT_MODE
    };

    g_spi = spi_master_create(&config);
    return (g_spi != NULL) ? 0 : -1;
}

/**
 * @brief Deinitialize SPI
 */
void fpga_spi_deinit(void) {
    if (g_spi != NULL) {
        spi_master_destroy(g_spi);
        g_spi = NULL;
    }
}

/**
 * @brief Write to FPGA register with verification
 *
 * Legacy API: returns 0 on success, negative errno on failure
 */
int fpga_reg_write(uint8_t addr, uint16_t data) {
    if (g_spi == NULL) {
        return -EBADF;
    }

    spi_status_t status = spi_write_register(g_spi, addr, data);

    /* Convert spi_status_t to errno */
    switch (status) {
        case SPI_OK:             return 0;
        case SPI_ERROR_NULL:
        case SPI_ERROR_CLOSED:   return -EBADF;
        case SPI_ERROR_VERIFY:   return -ETIMEDOUT;
        default:                 return -EIO;
    }
}

/**
 * @brief Read from FPGA register
 *
 * Legacy API: returns 0 on success, negative errno on failure
 */
int fpga_reg_read(uint8_t addr, uint16_t *data) {
    if (g_spi == NULL) {
        return -EBADF;
    }

    if (data == NULL) {
        return -EINVAL;
    }

    spi_status_t status = spi_read_register(g_spi, addr, data);

    /* Convert spi_status_t to errno */
    switch (status) {
        case SPI_OK:             return 0;
        case SPI_ERROR_NULL:
        case SPI_ERROR_CLOSED:   return -EBADF;
        default:                 return -EIO;
    }
}
