// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <bitset>
#include <cstdint>
#include <mutex>

#include "smileysecure/bytes.hpp"
#include "smileysecure/error.hpp"
#include "smileysecure/packet.hpp"
#include "smileysecure/transport.hpp"

namespace smileysecure {

/// Timing and retry behaviour for a `Link`.
struct LinkOptions {
    /// How long to wait for a device's reply before treating the exchange as lost and retrying.
    uint32_t response_timeout_ms = 1000;

    /// How many times to resend a command that drew no usable reply.  Zero means send once and
    /// give up.
    ///
    /// A retry repeats the original sequence flag rather than advancing it, which is what lets
    /// the device tell a retransmission from a new command and reply from its cache instead of
    /// acting twice.  That matters most on a payout.
    uint8_t max_retries = 2;
};

/// A request/response link to one or more SSP devices over a single stream.
///
/// The link owns two things the layers above must not have to think about.
///
/// The first is the sequence flag.  SSP's retransmission scheme depends on the host advancing
/// the flag for each new command and *repeating* it when resending one, so a device can tell a
/// retransmission from a fresh command.  That state is per device address and lives here.
///
/// The second is serialization.  An SSP bus carries one exchange at a time, so every call holds
/// a mutex for the length of its exchange.  That is what lets a command issued from another
/// task — or from inside a poll handler — queue between polls rather than race one.
class Link {
public:
    explicit Link(Stream& stream, LinkOptions options = {}, Clock& clock = Clock::system());

    Link(const Link&) = delete;
    Link& operator=(const Link&) = delete;

    /// Sends a command to a device and returns its reply.
    ///
    /// Timeouts and corrupt or out-of-step replies are retried up to `max_retries` times with
    /// the same sequence flag; when every attempt fails the result is `NoUsableReply` with the
    /// last attempt's error as its cause.  A closed stream or a refused write is not retried.
    Result<Packet> exchange(uint8_t address, ByteView data);

    /// Resets the sequence flag for a device, as a SYNC command does at the device end.  Call
    /// this whenever SYNC is sent, so both ends agree on the next flag.
    void reset_sequence(uint8_t address);

    const LinkOptions& options() const noexcept { return options_; }
    Stream& stream() noexcept { return stream_; }
    Clock& clock() noexcept { return clock_; }

private:
    Result<Packet> attempt_once(const std::vector<uint8_t>& logical, uint8_t address, bool sequence_flag);
    bool advance_sequence_flag(uint8_t address);

    Stream& stream_;
    Clock& clock_;
    LinkOptions options_;
    std::mutex gate_;
    std::bitset<128> sequence_flags_;
};

}  // namespace smileysecure
