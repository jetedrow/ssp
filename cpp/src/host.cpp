// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/host.hpp"

#include <chrono>

namespace smileysecure {

bool DeviceEvent::note_in_escrow() const noexcept {
    if (event.event() != Event::Read) return false;
    for (uint8_t byte : event.data) {
        if (byte != 0) return true;
    }
    return false;
}

std::string PollFault::to_string() const {
    if (!status.ok()) return status.describe();
    if (result != nullptr) return result->stop_message(device.protocol_version());
    return "unknown poll fault";
}

DeviceHost::DeviceHost(Device& device, HostOptions options) : device_(device), options_(options) {}

DeviceHost::~DeviceHost() { stop(); }

void DeviceHost::fault(const PollFault& fault) {
    if (!on_fault) return;
#if defined(__cpp_exceptions)
    try {
        on_fault(fault);
    } catch (...) {
        // A fault handler that throws has nowhere left to report to.
    }
#else
    on_fault(fault);
#endif
}

void DeviceHost::poll_once() {
    Result<PollResult> poll = options_.acknowledge_events ? device_.poll_with_ack() : device_.poll();
    if (!poll) {
        fault(PollFault{device_, poll.status(), nullptr});
        return;
    }

    // A reply that stopped part-way still carries the events read before it, so those are
    // delivered as usual and the gap is reported separately.
    if (!poll->complete()) fault(PollFault{device_, Status::success(), &*poll});

    raise(*poll);

    if (options_.acknowledge_events && !poll->events.empty()) {
        const Status acknowledged = device_.event_acknowledge();
        if (!acknowledged) fault(PollFault{device_, acknowledged, nullptr});
    }
}

void DeviceHost::raise(const PollResult& poll) {
    for (const PollEvent& event : poll.events) {
        DeviceEvent args{device_, event};

        if (on_event) {
#if defined(__cpp_exceptions)
            try {
                on_event(args);
            } catch (...) {
                // A handler that throws must not take the loop down with it, nor silently decide
                // the fate of a note: the event gets its default treatment and the throw is
                // reported.
                args.escrow = EscrowAction::Accept;
                fault(PollFault{device_, Status::failure(Error::InvalidState, "an event handler threw"), nullptr});
            }
#else
            on_event(args);
#endif
        }

        if (!args.note_in_escrow() || args.escrow == EscrowAction::Accept) continue;

        const Status sent = args.escrow == EscrowAction::Hold ? device_.hold() : device_.reject_banknote();
        if (!sent) fault(PollFault{device_, sent, nullptr});
    }
}

Status DeviceHost::run() {
    if (options_.poll_interval_ms >= HostOptions::kEscrowTimeoutMs) {
        return Status::failure(Error::InvalidArgument,
                               "a validator rejects an escrowed note after 10 seconds without a poll, so the "
                               "interval has to be shorter than that");
    }
    if (running_.exchange(true)) return Status::failure(Error::InvalidState, "this host is already polling");

    stop_requested_.store(false);
    run_loop();
    return Status::success();
}

void DeviceHost::run_loop() {
    while (!stop_requested_.load()) {
        poll_once();
        if (!wait_interval()) break;
    }
    running_.store(false);
}

bool DeviceHost::wait_interval() {
    std::unique_lock<std::mutex> lock(wait_gate_);
    return !wake_.wait_for(lock, std::chrono::milliseconds(options_.poll_interval_ms),
                           [this] { return stop_requested_.load(); });
}

Status DeviceHost::start() {
    if (running_.load()) return Status::failure(Error::InvalidState, "this host is already polling");
    if (thread_.joinable()) thread_.join();  // a loop stopped from inside a handler
    if (options_.poll_interval_ms >= HostOptions::kEscrowTimeoutMs) {
        return Status::failure(Error::InvalidArgument, "the poll interval has to be shorter than the 10-second escrow timeout");
    }

    stop_requested_.store(false);
    running_.store(true);
    thread_ = std::thread([this] { run_loop(); });
    return Status::success();
}

void DeviceHost::stop() {
    {
        std::lock_guard<std::mutex> lock(wait_gate_);
        stop_requested_.store(true);
    }
    wake_.notify_all();

    if (thread_.joinable() && thread_.get_id() != std::this_thread::get_id()) thread_.join();
}

}  // namespace smileysecure
