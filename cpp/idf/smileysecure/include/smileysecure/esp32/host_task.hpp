// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>

#include "freertos/FreeRTOS.h"
#include "smileysecure/error.hpp"
#include "smileysecure/host.hpp"

namespace smileysecure {
namespace esp32 {

/// Where and how the poll loop's task runs.
struct HostTaskOptions {
    /// The core to pin the poll loop to.  Defaults to the second core on a dual-core chip
    /// (ESP32, S3, P4), leaving the first to Wi-Fi and the rest of the application; on a
    /// single-core chip there is only core 0.  Pass -1 to let the scheduler choose.
    int core = portNUM_PROCESSORS > 1 ? 1 : 0;

    /// Stack for the poll task, in bytes.  Event handlers run on it, so a handler that does
    /// heavy work or logs with large format strings needs more.
    size_t stack_size = 6144;

    /// FreeRTOS priority of the poll task.
    int priority = 5;

    /// Name of the task, as it shows up in task lists.
    const char* name = "ssp_poll";
};

/// Starts the host's poll loop on its own FreeRTOS task, pinned as `options` say.
///
/// The task is a std::thread created under an esp_pthread configuration, so `host.stop()` ends
/// it and waits for it exactly as it does anywhere else.  The calling task's own pthread
/// configuration is restored before this returns.
///
/// @return InvalidArgument if the core does not exist on this chip; otherwise whatever
///         `DeviceHost::start()` returns.
Status start_pinned(DeviceHost& host, const HostTaskOptions& options = {});

}  // namespace esp32
}  // namespace smileysecure
