/**
 * @file eth_tx.h
 * @brief Ethernet TX HAL for UDP frame transmission
 *
 * REQ-FW-040~043: UDP frame transmission with fragmentation.
 * Uses Linux socket API for 10 GbE UDP streaming.
 */

#ifndef DETECTOR_HAL_ETH_TX_H
#define DETECTOR_HAL_ETH_TX_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Frame header format (32 bytes)
 *
 * Per REQ-FW-040: Frame header format for UDP packet fragmentation.
 */
typedef struct __attribute__((packed)) {
    uint32_t magic;           /**< Magic number: 0xD7E01234 */
    uint32_t frame_number;    /**< Frame sequence number */
    uint32_t width;           /**< Frame width in pixels */
    uint32_t height;          /**< Frame height in pixels */
    uint16_t bit_depth;       /**< Bits per pixel (14 or 16) */
    uint16_t flags;           /**< Frame flags */
    uint32_t packet_index;    /**< Packet index in frame (0-based) */
    uint32_t total_packets;   /**< Total packets in frame */
    uint32_t payload_len;     /**< Payload length in this packet */
    uint32_t timestamp;       /**< Timestamp in nanoseconds */
    uint16_t header_crc;      /**< CRC-16 of header (excluding this field) */
    uint16_t reserved;        /**< Reserved for future use */
} eth_frame_header_t;

/* Frame header magic number */
#define ETH_FRAME_MAGIC         0xD7E01234

/* Frame header size */
#define ETH_FRAME_HEADER_SIZE   32

/* Maximum UDP payload size (MTU 1500 - IP header 20 - UDP header 8) */
#define ETH_MAX_UDP_PAYLOAD     1472

/* Default ports */
#define ETH_DEFAULT_DATA_PORT   8000  /**< Data channel (frame streaming) */
#define ETH_DEFAULT_CMD_PORT    8001  /**< Command channel (control) */

/**
 * @brief Ethernet TX configuration
 */
typedef struct {
    const char *dest_ip;       /**< Destination IP address */
    uint16_t data_port;        /**< Data port (default: 8000) */
    uint16_t cmd_port;         /**< Command port (default: 8001) */
    uint32_t mtu;              /**< Maximum transmission unit (default: 1500) */
    uint32_t max_payload;      /**< Maximum payload per packet */
    bool enable_crc;           /**< Enable CRC-16 in header */
} eth_tx_config_t;

/**
 * @brief Ethernet TX result codes
 */
typedef enum {
    ETH_TX_OK = 0,             /**< Success */
    ETH_TX_ERROR_NULL = -1,    /**< NULL pointer argument */
    ETH_TX_ERROR_SOCKET = -2,  /**< Socket creation failed */
    ETH_TX_ERROR_BIND = -3,    /**< Bind failed */
    ETH_TX_ERROR_SEND = -4,    /**< Send failed */
    ETH_TX_ERROR_CLOSED = -5,  /**< Socket not open */
    ETH_TX_ERROR_PARAM = -6,   /**< Invalid parameter */
    ETH_TX_ERROR_MEMORY = -7,  /**< Memory allocation failed */
    ETH_TX_ERROR_TIMEOUT = -8, /**< Send timeout */
} eth_tx_status_t;

/**
 * @brief Ethernet TX handle (opaque)
 */
typedef struct eth_tx eth_tx_t;

/**
 * @brief Frame transmission statistics
 */
typedef struct {
    uint64_t frames_sent;      /**< Total frames sent */
    uint64_t packets_sent;     /**< Total packets sent */
    uint64_t bytes_sent;       /**< Total bytes sent */
    uint64_t send_errors;      /**< Send errors */
    uint64_t frames_dropped;   /**< Frames dropped (buffer full) */
    double avg_latency_ms;     /**< Average send latency in milliseconds */
} eth_tx_stats_t;

/* Default configuration */
#define ETH_DEFAULT_MTU         1500
#define ETH_DEFAULT_MAX_PAYLOAD 8192  /**< Larger than MTU for jumbo frames */
#define ETH_DEFAULT_DEST_IP     "127.0.0.1"

/**
 * @brief Create and initialize Ethernet TX
 *
 * @param config Ethernet TX configuration parameters
 * @return Pointer to Ethernet TX handle, or NULL on error
 *
 * Creates UDP sockets for data and command channels.
 * Per REQ-FW-043: Port 8000 for data, port 8001 for command.
 */
eth_tx_t *eth_tx_create(const eth_tx_config_t *config);

/**
 * @brief Destroy and cleanup Ethernet TX
 *
 * @param eth Ethernet TX handle (can be NULL)
 *
 * Closes sockets, frees resources.
 */
void eth_tx_destroy(eth_tx_t *eth);

/**
 * @brief Send a frame (with fragmentation)
 *
 * @param eth Ethernet TX handle
 * @param frame_data Pointer to frame data (RAW16 pixels)
 * @param frame_size Frame size in bytes
 * @param width Frame width in pixels
 * @param height Frame height in pixels
 * @param bit_depth Bits per pixel (14 or 16)
 * @param frame_number Frame sequence number
 * @return ETH_TX_OK on success, error code on failure
 *
 * Per REQ-FW-040: Fragments frame into UDP packets with frame header.
 * Per REQ-FW-041: Sends all packets within 1 frame period.
 * Per REQ-FW-042: Includes CRC-16 in frame header.
 */
eth_tx_status_t eth_tx_send_frame(eth_tx_t *eth,
                                 const void *frame_data,
                                 size_t frame_size,
                                 uint32_t width,
                                 uint32_t height,
                                 uint16_t bit_depth,
                                 uint32_t frame_number);

/**
 * @brief Send a command packet
 *
 * @param eth Ethernet TX handle
 * @param cmd_data Command data buffer
 * @param cmd_size Command size in bytes
 * @return ETH_TX_OK on success, error code on failure
 *
 * Sends command on command channel (port 8001).
 */
eth_tx_status_t eth_tx_send_command(eth_tx_t *eth,
                                   const void *cmd_data,
                                   size_t cmd_size);

/**
 * @brief Get last error message
 *
 * @param eth Ethernet TX handle
 * @return Error message string, or NULL if no error
 */
const char *eth_get_error(eth_tx_t *eth);

/**
 * @brief Get statistics about Ethernet TX operations
 *
 * @param eth Ethernet TX handle
 * @param stats Pointer to store statistics
 * @return ETH_TX_OK on success, error code on failure
 */
eth_tx_status_t eth_tx_get_stats(eth_tx_t *eth, eth_tx_stats_t *stats);

/**
 * @brief Reset statistics counters
 *
 * @param eth Ethernet TX handle
 * @return ETH_TX_OK on success, error code on failure
 */
eth_tx_status_t eth_tx_reset_stats(eth_tx_t *eth);

/**
 * @brief Update destination address
 *
 * @param eth Ethernet TX handle
 * @param dest_ip New destination IP address
 * @return ETH_TX_OK on success, error code on failure
 *
 * Allows dynamic reconfiguration of destination.
 */
eth_tx_status_t eth_tx_set_destination(eth_tx_t *eth, const char *dest_ip);

/**
 * @brief Calculate number of packets for a frame
 *
 * @param eth Ethernet TX handle
 * @param frame_size Frame size in bytes
 * @return Number of packets required
 *
 * Utility function to estimate packet count before transmission.
 */
size_t eth_tx_calc_packet_count(eth_tx_t *eth, size_t frame_size);

#ifdef __cplusplus
}
#endif

#endif /* DETECTOR_HAL_ETH_TX_H */
