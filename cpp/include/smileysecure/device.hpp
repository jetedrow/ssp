// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <atomic>
#include <cstdint>
#include <map>
#include <memory>
#include <mutex>
#include <optional>
#include <string>

#include "smileysecure/bytes.hpp"
#include "smileysecure/codes.hpp"
#include "smileysecure/error.hpp"
#include "smileysecure/event_table.hpp"
#include "smileysecure/link.hpp"
#include "smileysecure/message.hpp"
#include "smileysecure/poll.hpp"
#include "smileysecure/setup.hpp"
#include "smileysecure/transport.hpp"

namespace smileysecure {

/// Timing, retry and decoding settings for a bus and the devices on it.
struct DeviceOptions {
    /// How long to wait for a reply, and how often to resend a command that drew none.
    LinkOptions link{};

    /// The event payload lengths poll replies are decoded with.  Replace it to support a device
    /// newer than this library; see `EventTable::with_event()`.
    EventTable event_table = EventTable::standard();

    /// The highest protocol version this host is prepared to decode.  Negotiation never sets a
    /// device above it.  Raising it above what `event_table` covers means poll replies may stop
    /// part-way.
    uint8_t highest_protocol_version = protocol_version::kHighest;
};

class Device;

/// One SSP bus: a stream, and the devices addressed over it.
///
/// SSP is multi-drop — several devices can share one pair of wires, each answering to its own
/// address — and only one exchange can be in flight at a time.  The bus owns the `Link` that
/// serializes them, so calls to two devices from two tasks queue rather than interleave.
class Bus {
public:
    explicit Bus(Stream& stream, DeviceOptions options = {}, Clock& clock = Clock::system());
    ~Bus();

    Bus(const Bus&) = delete;
    Bus& operator=(const Bus&) = delete;

    /// The device at an address, created the first time it is asked for.  Validators default
    /// to address 0 and hoppers to 16.
    /// @return nullptr if the address does not fit in seven bits.
    Device* device(uint8_t address = 0x00);

    const DeviceOptions& options() const noexcept { return options_; }
    Link& link() noexcept { return link_; }

private:
    DeviceOptions options_;
    Link link_;
    std::mutex gate_;
    std::map<uint8_t, std::unique_ptr<Device>> devices_;
};

/// One device on a bus, as a set of blocking commands.
///
/// This is the procedural half of the API: ask the device to do something and get the answer.
/// The event-driven half, `DeviceHost`, is a poll loop built on these same calls, and both go
/// through the bus's one link, so a command issued from inside an event handler lands between
/// polls rather than racing one.
///
/// Every call blocks the calling task until the device answers or the retries run out.  Calls
/// may come from more than one task.
class Device {
public:
    Device(Bus& bus, uint8_t address);

    Device(const Device&) = delete;
    Device& operator=(const Device&) = delete;

    Bus& bus() noexcept { return bus_; }
    uint8_t address() const noexcept { return address_; }

    /// The protocol version this device is believed to be set to, which decides how its poll
    /// replies are read.  `protocol_version::kLowest` until connect, negotiation or a setup
    /// request says otherwise — so connect before polling.
    uint8_t protocol_version() const noexcept { return protocol_version_.load(); }

    /// What the device reported the last time `setup_request()` ran.
    std::optional<Setup> setup() const;

    // ---- the escape hatch ----------------------------------------------------------------

    /// Sends a command and returns the reply, without checking whether the device accepted it.
    /// Takes a raw byte so a capability this library has not modelled is still reachable.
    Result<Reply> send(uint8_t command, ByteView parameters = {});
    Result<Reply> send(Command command, ByteView parameters = {}) {
        return send(static_cast<uint8_t>(command), parameters);
    }

    /// Sends a command and fails with `Refused` unless the device accepted it.
    Result<Reply> send_checked(Command command, ByteView parameters = {});

    // ---- connecting ------------------------------------------------------------------------

    /// The startup sequence: synchronise, agree a protocol version, read the setup.
    ///
    /// The version is settled before the setup is read because a validator lays its channel
    /// data out differently from version 6.  This does not enable the device or lift its
    /// channel inhibits: nothing is accepted until `set_channel_inhibits()` and `enable()` have
    /// both run, which gives a host the chance to look at what it connected to first.
    Result<Setup> connect();

    /// Finds the highest version both ends support and sets the device to it.
    ///
    /// The protocol has no way to ask what a device supports, only to ask it to change: it
    /// answers OK if it can and FAIL if it cannot.  So this walks down from `highest` (or the
    /// options' ceiling) until one is accepted.  A refusal other than FAIL stops it at once.
    Result<uint8_t> negotiate_protocol_version(std::optional<uint8_t> highest = std::nullopt);

    /// Sets the device to a specific protocol version.
    Status set_protocol_version(uint8_t version);

    // ---- the everyday commands -------------------------------------------------------------

    /// Resets the sequence flag at both ends — also the usual way to check a device is there.
    Status sync();

    /// Restarts the device, as a power cycle would.
    Status reset();

    /// Puts the device into its enabled state, where it will accept money.
    Status enable();

    /// Puts the device into its disabled state, where it will not accept anything.
    Status disable();

    /// Lights the bezel.
    Status display_on();

    /// Turns the bezel light off.
    Status display_off();

    /// Returns the note held in escrow to the user.
    Status reject_banknote();

    /// Holds a note in escrow for another poll interval instead of accepting it.  A validator
    /// accepts an escrowed note on the next poll unless told otherwise, so a host that needs
    /// longer to decide has to keep sending this.
    Status hold();

    /// Says which channels may accept notes: a bit per channel, lowest bit channel 1, set to
    /// allow.  0xFFFF allows all sixteen.  A device accepts nothing until this has been sent.
    Status set_channel_inhibits(uint16_t channel_mask);

    /// The same, for a device with more channels: one byte per eight channels, lowest first.
    Status set_channel_inhibits(ByteView channel_mask);

    /// Asks the device what it is, and remembers the answer.  If the reply names a protocol
    /// version, `protocol_version()` follows it — the only way to learn a device's version.
    Result<Setup> setup_request();

    /// The factory-programmed serial number.
    Result<uint32_t> serial_number();

    /// The firmware version, as the ASCII string the device reports.
    Result<std::string> firmware_version();

    /// The loaded dataset's version, as the ASCII string the device reports.
    Result<std::string> dataset_version();

    // ---- polling ---------------------------------------------------------------------------

    /// What has happened since the last poll, read at `protocol_version()`.  Check
    /// `PollResult::complete()`: a reply containing an event this library has no length for is
    /// reported rather than guessed at.
    ///
    /// A validator takes a poll as permission to accept a note sitting in escrow, so polling is
    /// not a read-only operation.
    Result<PollResult> poll();

    /// Polls, and has the device repeat each event until it is acknowledged with
    /// `event_acknowledge()`, so a host that dies between reading a credit and recording it
    /// sees the credit again.
    Result<PollResult> poll_with_ack();

    /// Acknowledges the events from the last `poll_with_ack()`.
    Status event_acknowledge();

private:
    Status command(Command command, ByteView parameters = {});
    Result<PollResult> poll_using(Command command);

    Bus& bus_;
    const uint8_t address_;
    std::atomic<uint8_t> protocol_version_;
    mutable std::mutex setup_gate_;
    std::optional<Setup> setup_;
};

}  // namespace smileysecure
