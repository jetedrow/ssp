// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/esp32/uart_stream.hpp"

#include "freertos/FreeRTOS.h"

namespace smileysecure {
namespace esp32 {
namespace {

TickType_t to_ticks(uint32_t milliseconds) {
    if (milliseconds == 0) return 0;
    // Round up, so a short timeout is never zero ticks and never shorter than asked for.
    const TickType_t ticks = pdMS_TO_TICKS(milliseconds);
    return ticks == 0 ? 1 : ticks;
}

}  // namespace

UartStream::UartStream(const UartConfig& config) : port_(config.port) {
    uart_config_t settings = {};
    settings.baud_rate = static_cast<int>(config.baud_rate);
    settings.data_bits = UART_DATA_8_BITS;
    settings.parity = UART_PARITY_DISABLE;
    settings.stop_bits = UART_STOP_BITS_2;
    settings.flow_ctrl = UART_HW_FLOWCTRL_DISABLE;
    settings.source_clk = UART_SCLK_DEFAULT;

    status_ = uart_driver_install(port_, config.rx_buffer_size, 0, 0, nullptr, 0);
    if (status_ != ESP_OK) return;
    installed_ = true;

    status_ = uart_param_config(port_, &settings);
    if (status_ != ESP_OK) return;

    status_ = uart_set_pin(port_, config.tx_pin, config.rx_pin, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE);
}

UartStream::~UartStream() {
    if (installed_) uart_driver_delete(port_);
}

bool UartStream::write(const uint8_t* data, size_t size) {
    if (status_ != ESP_OK) return false;
    return uart_write_bytes(port_, data, size) == static_cast<int>(size);
}

int UartStream::read(uint8_t* data, size_t size, uint32_t timeout_ms) {
    if (status_ != ESP_OK) return -1;
    const int count = uart_read_bytes(port_, data, static_cast<uint32_t>(size), to_ticks(timeout_ms));
    // The driver reports its own failures as -1, which is what the Stream contract uses for a
    // stream that will never produce more.
    return count < 0 ? -1 : count;
}

void UartStream::flush() {
    if (status_ == ESP_OK) uart_wait_tx_done(port_, portMAX_DELAY);
}

bool UartStream::set_baud_rate(uint32_t baud_rate) {
    if (status_ != ESP_OK) return false;
    // Anything still going out was meant for the old speed.
    uart_wait_tx_done(port_, portMAX_DELAY);
    return uart_set_baudrate(port_, baud_rate) == ESP_OK;
}

void UartStream::discard_buffers() {
    if (status_ == ESP_OK) uart_flush_input(port_);
}

}  // namespace esp32
}  // namespace smileysecure
