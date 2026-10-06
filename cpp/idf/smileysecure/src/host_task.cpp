// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/esp32/host_task.hpp"

#include "esp_pthread.h"

namespace smileysecure {
namespace esp32 {

Status start_pinned(DeviceHost& host, const HostTaskOptions& options) {
    if (options.core >= portNUM_PROCESSORS) {
        return Status::failure(Error::InvalidArgument, "this chip does not have that core");
    }

    // esp_pthread settings apply to threads the calling task creates from here on, so set them
    // for the one std::thread the host is about to start and put the caller's back afterwards.
    esp_pthread_cfg_t previous = esp_pthread_get_default_config();
    const bool had_previous = esp_pthread_get_cfg(&previous) == ESP_OK;

    esp_pthread_cfg_t config = esp_pthread_get_default_config();
    config.stack_size = options.stack_size;
    config.prio = static_cast<size_t>(options.priority);
    config.pin_to_core = options.core < 0 ? tskNO_AFFINITY : options.core;
    config.thread_name = options.name;
    config.inherit_cfg = false;

    if (esp_pthread_set_cfg(&config) != ESP_OK) {
        return Status::failure(Error::InvalidArgument, "the poll task's stack size or priority was refused");
    }

    const Status started = host.start();

    if (had_previous) {
        esp_pthread_set_cfg(&previous);
    } else {
        const esp_pthread_cfg_t defaults = esp_pthread_get_default_config();
        esp_pthread_set_cfg(&defaults);
    }

    return started;
}

}  // namespace esp32
}  // namespace smileysecure
