// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "smileysecure/bytes.hpp"
#include "smileysecure/codes.hpp"
#include "smileysecure/event_table.hpp"

namespace smileysecure {

/// One event read out of a poll reply, with the data bytes that belonged to it.
struct PollEvent {
    uint8_t code = 0;
    std::vector<uint8_t> data;

    /// The code as a named value.  A code the library has no name for still converts, so a
    /// switch on this should have a default arm; `is_known()` says whether the name means
    /// anything.
    Event event() const noexcept { return static_cast<Event>(code); }
    bool is_known() const noexcept { return event_name(code) != nullptr; }

    /// "Note Credit (0xEE), 1 data byte(s)" and the like.
    std::string to_string() const;
};

inline bool operator==(const PollEvent& a, const PollEvent& b) { return a.code == b.code && a.data == b.data; }
inline bool operator!=(const PollEvent& a, const PollEvent& b) { return !(a == b); }

/// What a poll reply turned out to contain.
///
/// A decode can end part-way.  Payload lengths are not on the wire, so the first event the table
/// has no length for takes the rest of the reply with it.  The events read before that point
/// are still good and are still here — throwing them away would lose credits that really
/// happened, which is why a stopped decode is a result rather than an error.
struct PollResult {
    enum class StopReason : uint8_t {
        /// The whole reply was read.
        None = 0,
        /// The table has no payload length for the code at this version.
        NoRule,
        /// The payload runs past the end of the reply.
        Truncated,
    };

    std::vector<PollEvent> events;
    StopReason stop_reason = StopReason::None;

    /// The event code the decode stopped at, when it stopped.
    uint8_t stopped_at_code = 0;

    /// The bytes from the code it stopped at to the end of the reply.  Empty when complete.
    std::vector<uint8_t> undecoded;

    bool complete() const noexcept { return stop_reason == StopReason::None; }

    /// Why the decode stopped, as a sentence, or an empty string when it did not.
    std::string stop_message(uint8_t version) const;

    std::string to_string() const;
};

/// Reads a poll reply's event list.
///
/// @param event_data the reply's data with the leading response code removed — `Reply::data`.
/// @param version the protocol version the device is set to.  The same bytes decode
///        differently at different versions, and only one reading is what the device meant.
PollResult decode_poll(ByteView event_data, uint8_t version, const EventTable& table = EventTable::standard());

}  // namespace smileysecure
