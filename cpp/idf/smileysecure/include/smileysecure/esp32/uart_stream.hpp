// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>
#include <cstdint>

#include "driver/uart.h"
#include "esp_err.h"
#include "smileysecure/transport.hpp"

namespace smileysecure {
namespace esp32 {

/// How to wire up and drive the UART an SSP device is on.
struct UartConfig {
    uart_port_t port = UART_NUM_1;
    int tx_pin = UART_PIN_NO_CHANGE;
    int rx_pin = UART_PIN_NO_CHANGE;

    /// SSP runs at 9600 baud unless the device has been told otherwise.
    uint32_t baud_rate = 9600;

    /// Receive buffer size for the driver.  Must exceed the chip's hardware FIFO (128 bytes);
    /// the default holds two maximum-sized packets.
    int rx_buffer_size = 1024;
};

/// A UART carrying SSP: 8 data bits, no parity, two stop bits.
///
/// Installs the ESP-IDF UART driver on construction and removes it on destruction.  Check
/// `status()` before use: a pin that cannot be routed or a port already in use leaves the stream
/// unusable, and every write then fails rather than the constructor aborting.
class UartStream final : public Stream, public BaudRateControl {
public:
    explicit UartStream(const UartConfig& config);
    ~UartStream() override;

    UartStream(const UartStream&) = delete;
    UartStream& operator=(const UartStream&) = delete;

    /// ESP_OK if the driver is installed and configured.
    esp_err_t status() const noexcept { return status_; }

    bool write(const uint8_t* data, size_t size) override;
    int read(uint8_t* data, size_t size, uint32_t timeout_ms) override;
    void flush() override;

    bool set_baud_rate(uint32_t baud_rate) override;
    void discard_buffers() override;

private:
    uart_port_t port_;
    esp_err_t status_ = ESP_FAIL;
    bool installed_ = false;
};

}  // namespace esp32
}  // namespace smileysecure
