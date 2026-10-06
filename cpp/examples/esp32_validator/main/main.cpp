// A banknote validator on UART1, polled from the second core, with another peripheral's task
// sharing that core.
// SPDX-License-Identifier: LGPL-3.0-or-later
#include <cstdio>

#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "smileysecure/esp32/host_task.hpp"
#include "smileysecure/esp32/uart_stream.hpp"
#include "smileysecure/smileysecure.hpp"

using namespace smileysecure;

namespace {

constexpr const char* kTag = "validator";

// Change these to match your wiring.
constexpr int kTxPin = 17;
constexpr int kRxPin = 16;

// Stands in for any other peripheral -- an RFID reader, say -- that wants the same core.  It
// spends nearly all its time blocked, as the poll loop does, so the two share the core freely.
void other_peripheral_task(void*) {
    for (;;) {
        // read the RFID reader here
        vTaskDelay(pdMS_TO_TICKS(50));
    }
}

}  // namespace

extern "C" void app_main() {
    esp32::UartConfig uart_config;
    uart_config.port = UART_NUM_1;
    uart_config.tx_pin = kTxPin;
    uart_config.rx_pin = kRxPin;

    // Static so they outlive app_main, which returns once everything is running.
    static esp32::UartStream uart(uart_config);
    if (uart.status() != ESP_OK) {
        ESP_LOGE(kTag, "UART setup failed: %s", esp_err_to_name(uart.status()));
        return;
    }

    static Bus bus(uart);
    Device& validator = *bus.device(0x00);

    Result<Setup> setup = validator.connect();
    if (!setup) {
        ESP_LOGE(kTag, "connect failed: %s", setup.status().describe().c_str());
        return;
    }
    ESP_LOGI(kTag, "connected: firmware %s, %s, protocol version %u, %u channel(s)",
             setup->firmware_version.c_str(), setup->country_code.c_str(),
             static_cast<unsigned>(validator.protocol_version()), static_cast<unsigned>(setup->channels.size()));

    static std::vector<Channel> channels = setup->channels;

    validator.set_channel_inhibits(uint16_t{0xFFFF});
    validator.enable();

    static DeviceHost host(validator);
    host.on_event = [](DeviceEvent& e) {
        ESP_LOGI(kTag, "%s", e.event.to_string().c_str());

        // Turn back anything above 20.00, as an example of deciding a note's fate.
        if (e.note_in_escrow()) {
            const size_t channel = e.event.data[0];
            if (channel >= 1 && channel <= channels.size() && channels[channel - 1].value > 2000) {
                e.escrow = EscrowAction::Reject;
            }
        }
    };
    host.on_fault = [](const PollFault& fault) { ESP_LOGW(kTag, "%s", fault.to_string().c_str()); };

    // The poll loop on the second core, where it is out of the way of Wi-Fi and app_main...
    esp32::HostTaskOptions poll_task;
    const Status started = esp32::start_pinned(host, poll_task);
    if (!started) {
        ESP_LOGE(kTag, "could not start polling: %s", started.describe().c_str());
        return;
    }

    // ...and another peripheral's task pinned to the same core, at the same priority.
    xTaskCreatePinnedToCore(other_peripheral_task, "rfid", 4096, nullptr, poll_task.priority, nullptr,
                            poll_task.core);
}
