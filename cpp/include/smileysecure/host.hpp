// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <functional>
#include <mutex>
#include <string>
#include <thread>

#include "smileysecure/device.hpp"
#include "smileysecure/error.hpp"
#include "smileysecure/poll.hpp"

namespace smileysecure {

/// What to do with a note sitting in escrow.
enum class EscrowAction : uint8_t {
    /// Let the next poll stack it.  What a validator does if told nothing.
    Accept = 0,
    /// Keep it in escrow for another poll interval.
    Hold,
    /// Hand it back.
    Reject,
};

/// One event, as an event handler sees it.
struct DeviceEvent {
    Device& device;
    const PollEvent& event;

    /// What to do with the note, when this is a note in escrow.  The host sends the hold or
    /// reject as soon as the handler returns, before the next poll — a validator takes the next
    /// poll as permission to stack, so after that it is too late.
    EscrowAction escrow = EscrowAction::Accept;

    /// Whether this is a note held in escrow: a Read event naming a channel.  A Read with a
    /// zero channel is a note still being scanned, and is no decision yet.
    bool note_in_escrow() const noexcept;
};

/// Something that went wrong in the poll loop, which carries on regardless.
struct PollFault {
    Device& device;

    /// Why a poll or an escrow command failed; success when the fault is a part-read reply.
    Status status;

    /// The reply that stopped part-way, when that is the fault; null otherwise.  The events
    /// read before it were still delivered.
    const PollResult* result = nullptr;

    std::string to_string() const;
};

/// Settings for a `DeviceHost`.
struct HostOptions {
    /// A validator rejects an escrowed note after this long without a poll.
    static constexpr uint32_t kEscrowTimeoutMs = 10000;

    /// How long to wait between polls.  Must be shorter than `kEscrowTimeoutMs`.
    uint32_t poll_interval_ms = 200;

    /// Use Poll With Ack, acknowledging each poll's events once every handler has seen them.
    bool acknowledge_events = false;
};

/// An event-driven poll loop over one device.
///
/// Polling an SSP device is not passive: a poll is what lets a validator stack the note in its
/// escrow.  So events are delivered to the handler synchronously, between one poll and the
/// next, and a handler decides a note's fate by setting `DeviceEvent::escrow` — the host sends
/// the hold or reject before it polls again.  A handler may also call the device directly.
///
/// The loop never dies.  A poll that fails, an escrow command that fails, and a reply that
/// stops part-way are all reported to `on_fault`, and polling carries on.
///
/// Three ways to drive it:
///   - `poll_once()` from a loop of your own, such as Arduino's `loop()`;
///   - `run()` to block the calling task until `stop()`;
///   - `start()` to run it on a std::thread.
class DeviceHost {
public:
    explicit DeviceHost(Device& device, HostOptions options = {});
    ~DeviceHost();

    DeviceHost(const DeviceHost&) = delete;
    DeviceHost& operator=(const DeviceHost&) = delete;

    /// Called once per event, in the order the device sent them.
    std::function<void(DeviceEvent&)> on_event;

    /// Called for every fault; the loop carries on afterwards.
    std::function<void(const PollFault&)> on_fault;

    Device& device() noexcept { return device_; }
    const HostOptions& options() const noexcept { return options_; }

    /// One cycle: poll, hand each event to `on_event` and act on its escrow decision, then
    /// acknowledge when so configured.  Does not wait for the poll interval.
    void poll_once();

    /// Polls until `stop()` is called — from a handler or another task — waiting the poll
    /// interval between cycles.
    /// @return InvalidArgument if the poll interval is not shorter than the escrow timeout;
    ///         InvalidState if the loop is already running.
    Status run();

    /// Runs the loop on a new thread.
    /// @return InvalidState if it is already running; InvalidArgument for a bad interval.
    Status start();

    /// Stops the loop and, if `start()` created a thread, waits for it to finish.  Must not be
    /// called from inside a handler when the loop is on its own thread.
    void stop();

    bool running() const noexcept { return running_.load(); }

private:
    void run_loop();
    void raise(const PollResult& poll);
    void fault(const PollFault& fault);
    bool wait_interval();

    Device& device_;
    HostOptions options_;
    std::atomic<bool> running_{false};
    std::atomic<bool> stop_requested_{false};
    std::mutex wait_gate_;
    std::condition_variable wake_;
    std::thread thread_;
};

}  // namespace smileysecure
