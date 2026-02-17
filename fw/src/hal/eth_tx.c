/**
 * @file eth_tx.c
 * @brief Ethernet TX HAL for UDP frame transmission
 *
 * REQ-FW-040~043: UDP frame transmission with fragmentation.
 * Uses Linux socket API for 10 GbE UDP streaming.
 *
 * TDD Methodology:
 * - RED: Tests define expected behavior (to be created)
 * - GREEN: Implementation satisfies tests
 * - REFACTOR: Code improvements while maintaining tests
 */

#include "hal/eth_tx.h"
#include "util/crc16.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <unistd.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <time.h>

/**
 * @brief Ethernet TX internal state
 */
struct eth_tx {
    int data_fd;               /**< Data socket (port 8000) */
    int cmd_fd;                /**< Command socket (port 8001) */
    eth_tx_config_t config;    /**< Configuration */
    char error_msg[256];       /**< Last error message */

    /* Destination address */
    struct sockaddr_in dest_addr;

    /* Statistics */
    eth_tx_stats_t stats;
};

/**
 * @brief Convert error code to string
 */
static const char* eth_error_string(eth_tx_status_t status) {
    switch (status) {
        case ETH_TX_OK:             return "Success";
        case ETH_TX_ERROR_NULL:     return "NULL pointer argument";
        case ETH_TX_ERROR_SOCKET:   return "Socket creation failed";
        case ETH_TX_ERROR_BIND:     return "Bind failed";
        case ETH_TX_ERROR_SEND:     return "Send failed";
        case ETH_TX_ERROR_CLOSED:   return "Socket not open";
        case ETH_TX_ERROR_PARAM:    return "Invalid parameter";
        case ETH_TX_ERROR_MEMORY:   return "Memory allocation failed";
        case ETH_TX_ERROR_TIMEOUT:  return "Send timeout";
        default:                    return "Unknown error";
    }
}

/**
 * @brief Set error message
 */
static void eth_set_error(eth_tx_t *eth, eth_tx_status_t status, const char *detail) {
    if (eth == NULL) return;

    snprintf(eth->error_msg, sizeof(eth->error_msg), "%s: %s",
             eth_error_string(status), detail ? detail : "");
}

/**
 * @brief Create UDP socket
 */
static int eth_create_socket(uint16_t port) {
    int fd = socket(AF_INET, SOCK_DGRAM, 0);
    if (fd < 0) {
        return -1;
    }

    /* Set socket options */
    int opt = 1;
    setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    /* Bind to port */
    struct sockaddr_in addr = {0};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(port);

    if (bind(fd, (struct sockaddr *)&addr, sizeof(addr)) < 0) {
        close(fd);
        return -1;
    }

    return fd;
}

/* ==========================================================================
 * Public API Implementation
 * ========================================================================== */

eth_tx_t *eth_tx_create(const eth_tx_config_t *config) {
    if (config == NULL || config->dest_ip == NULL) {
        return NULL;
    }

    eth_tx_t *eth = (eth_tx_t *)calloc(1, sizeof(eth_tx_t));
    if (eth == NULL) {
        return NULL;
    }

    /* Copy configuration */
    eth->config = *config;
    eth->data_fd = -1;
    eth->cmd_fd = -1;

    /* Parse destination IP */
    if (inet_pton(AF_INET, config->dest_ip, &eth->dest_addr.sin_addr) <= 0) {
        eth_set_error(eth, ETH_TX_ERROR_PARAM, "Invalid destination IP");
        free(eth);
        return NULL;
    }

    eth->dest_addr.sin_family = AF_INET;
    eth->dest_addr.sin_port = htons(config->data_port);

    /* Create data socket */
    eth->data_fd = eth_create_socket(config->data_port);
    if (eth->data_fd < 0) {
        eth_set_error(eth, ETH_TX_ERROR_SOCKET, "Failed to create data socket");
        free(eth);
        return NULL;
    }

    /* Create command socket */
    eth->cmd_fd = eth_create_socket(config->cmd_port);
    if (eth->cmd_fd < 0) {
        eth_set_error(eth, ETH_TX_ERROR_SOCKET, "Failed to create command socket");
        close(eth->data_fd);
        free(eth);
        return NULL;
    }

    /* Initialize statistics */
    memset(&eth->stats, 0, sizeof(eth->stats));

    return eth;
}

void eth_tx_destroy(eth_tx_t *eth) {
    if (eth == NULL) return;

    if (eth->data_fd >= 0) {
        close(eth->data_fd);
    }

    if (eth->cmd_fd >= 0) {
        close(eth->cmd_fd);
    }

    free(eth);
}

eth_tx_status_t eth_tx_send_frame(eth_tx_t *eth,
                                 const void *frame_data,
                                 size_t frame_size,
                                 uint32_t width,
                                 uint32_t height,
                                 uint16_t bit_depth,
                                 uint32_t frame_number) {
    if (eth == NULL || frame_data == NULL) return ETH_TX_ERROR_NULL;
    if (eth->data_fd < 0) return ETH_TX_ERROR_CLOSED;
    if (frame_size == 0) return ETH_TX_ERROR_PARAM;

    /* Calculate packet parameters */
    size_t max_payload = eth->config.max_payload;
    if (max_payload == 0) {
        max_payload = ETH_DEFAULT_MAX_PAYLOAD;
    }

    size_t header_size = ETH_FRAME_HEADER_SIZE;
    size_t payload_per_packet = max_payload - header_size;

    /* Calculate total packets needed */
    size_t total_packets = (frame_size + payload_per_packet - 1) / payload_per_packet;

    /* Track timing */
    struct timespec start, end;
    clock_gettime(CLOCK_MONOTONIC, &start);

    /* Send each packet */
    for (size_t i = 0; i < total_packets; i++) {
        /* Calculate payload offset and length */
        size_t offset = i * payload_per_packet;
        size_t payload_len = (offset + payload_per_packet > frame_size) ?
                             (frame_size - offset) : payload_per_packet;

        /* Build frame header */
        /* Per REQ-FW-040: Frame header format */
        eth_frame_header_t header = {0};
        header.magic = ETH_FRAME_MAGIC;
        header.frame_number = frame_number;
        header.width = width;
        header.height = height;
        header.bit_depth = bit_depth;
        header.flags = 0;
        header.packet_index = (uint32_t)i;
        header.total_packets = (uint32_t)total_packets;
        header.payload_len = (uint32_t)payload_len;
        header.timestamp = (uint32_t)time(NULL);
        header.reserved = 0;

        /* Per REQ-FW-042: Compute CRC-16 of header */
        if (eth->config.enable_crc) {
            header.header_crc = crc16_compute((const uint8_t *)&header,
                                             sizeof(header) - sizeof(uint16_t) - sizeof(uint16_t));
        } else {
            header.header_crc = 0;
        }

        /* Build packet buffer */
        size_t packet_size = header_size + payload_len;
        uint8_t *packet_buf = (uint8_t *)malloc(packet_size);
        if (packet_buf == NULL) {
            eth_set_error(eth, ETH_TX_ERROR_MEMORY, "Failed to allocate packet buffer");
            return ETH_TX_ERROR_MEMORY;
        }

        /* Copy header and payload */
        memcpy(packet_buf, &header, header_size);
        memcpy(packet_buf + header_size, (const uint8_t *)frame_data + offset, payload_len);

        /* Send packet */
        struct sockaddr_in dest_addr = eth->dest_addr;
        ssize_t sent = sendto(eth->data_fd, packet_buf, packet_size, 0,
                             (struct sockaddr *)&dest_addr, sizeof(dest_addr));

        free(packet_buf);

        if (sent < 0) {
            eth_set_error(eth, ETH_TX_ERROR_SEND, strerror(errno));
            eth->stats.send_errors++;
            return ETH_TX_ERROR_SEND;
        }

        if ((size_t)sent != packet_size) {
            eth_set_error(eth, ETH_TX_ERROR_SEND, "Partial send");
            eth->stats.send_errors++;
            return ETH_TX_ERROR_SEND;
        }

        eth->stats.packets_sent++;
        eth->stats.bytes_sent += sent;
    }

    /* Update timing statistics */
    clock_gettime(CLOCK_MONOTONIC, &end);
    uint64_t elapsed_ns = (end.tv_sec - start.tv_sec) * 1000000000ULL +
                          (end.tv_nsec - start.tv_nsec);
    double elapsed_ms = elapsed_ns / 1000000.0;

    /* Update average latency (exponential moving average) */
    if (eth->stats.frames_sent == 0) {
        eth->stats.avg_latency_ms = elapsed_ms;
    } else {
        eth->stats.avg_latency_ms = 0.9 * eth->stats.avg_latency_ms + 0.1 * elapsed_ms;
    }

    eth->stats.frames_sent++;

    /* Per REQ-FW-041: TX within 1 frame period */
    /* At 15 fps, 1 frame period = 66.7 ms */
    double frame_period_ms = 1000.0 / eth->config.fps;
    if (elapsed_ms > frame_period_ms) {
        /* Log warning but don't fail */
        /* In production, this should be logged */
    }

    return ETH_TX_OK;
}

eth_tx_status_t eth_tx_send_command(eth_tx_t *eth,
                                   const void *cmd_data,
                                   size_t cmd_size) {
    if (eth == NULL || cmd_data == NULL) return ETH_TX_ERROR_NULL;
    if (eth->cmd_fd < 0) return ETH_TX_ERROR_CLOSED;
    if (cmd_size == 0) return ETH_TX_ERROR_PARAM;

    /* Build destination address for command port */
    struct sockaddr_in dest_addr = eth->dest_addr;
    dest_addr.sin_port = htons(eth->config.cmd_port);

    /* Send command */
    ssize_t sent = sendto(eth->cmd_fd, cmd_data, cmd_size, 0,
                         (struct sockaddr *)&dest_addr, sizeof(dest_addr));

    if (sent < 0) {
        eth_set_error(eth, ETH_TX_ERROR_SEND, strerror(errno));
        return ETH_TX_ERROR_SEND;
    }

    if ((size_t)sent != cmd_size) {
        eth_set_error(eth, ETH_TX_ERROR_SEND, "Partial send");
        return ETH_TX_ERROR_SEND;
    }

    eth->stats.packets_sent++;
    eth->stats.bytes_sent += sent;

    return ETH_TX_OK;
}

const char *eth_get_error(eth_tx_t *eth) {
    if (eth == NULL) return "NULL Ethernet TX handle";
    return eth->error_msg;
}

eth_tx_status_t eth_tx_get_stats(eth_tx_t *eth, eth_tx_stats_t *stats) {
    if (eth == NULL || stats == NULL) return ETH_TX_ERROR_NULL;

    memcpy(stats, &eth->stats, sizeof(eth_tx_stats_t));
    return ETH_TX_OK;
}

eth_tx_status_t eth_tx_reset_stats(eth_tx_t *eth) {
    if (eth == NULL) return ETH_TX_ERROR_NULL;

    memset(&eth->stats, 0, sizeof(eth_tx_stats_t));
    return ETH_TX_OK;
}

eth_tx_status_t eth_tx_set_destination(eth_tx_t *eth, const char *dest_ip) {
    if (eth == NULL || dest_ip == NULL) return ETH_TX_ERROR_NULL;

    /* Parse new destination IP */
    struct in_addr new_addr;
    if (inet_pton(AF_INET, dest_ip, &new_addr) <= 0) {
        eth_set_error(eth, ETH_TX_ERROR_PARAM, "Invalid destination IP");
        return ETH_TX_ERROR_PARAM;
    }

    eth->dest_addr.sin_addr = new_addr;
    return ETH_TX_OK;
}

size_t eth_tx_calc_packet_count(eth_tx_t *eth, size_t frame_size) {
    if (eth == NULL) return 0;

    size_t max_payload = eth->config.max_payload;
    if (max_payload == 0) {
        max_payload = ETH_DEFAULT_MAX_PAYLOAD;
    }

    size_t header_size = ETH_FRAME_HEADER_SIZE;
    size_t payload_per_packet = max_payload - header_size;

    return (frame_size + payload_per_packet - 1) / payload_per_packet;
}
