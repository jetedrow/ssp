// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

#include "smileysecure/bytes.hpp"

namespace smileysecure {

/// SSP protocol versions.
///
/// SSP uses the protocol version to gate *events*, not commands.  A poll reply is a run of
/// event codes each followed by a payload whose length the host is expected to know in advance;
/// a device that sent an event the host had never heard of would leave it unable to tell where
/// that payload ended.  Raising the version is a host saying "I know the events you are about
/// to send me", so a host must never set a device higher than it can decode.
namespace protocol_version {

/// The oldest version the event table describes.  Older devices send a subset of its events.
constexpr uint8_t kLowest = 4;

/// The newest version the event table describes.
constexpr uint8_t kHighest = 9;

/// Whether the event table describes this version in full.  A version above `kHighest` is not
/// an error; it means the device may send events a decode will report rather than guess at.
constexpr bool is_known(uint8_t version) noexcept { return version >= kLowest && version <= kHighest; }

}  // namespace protocol_version

/// How many data bytes follow an event code in a poll reply.
///
/// Most events carry a fixed number of bytes.  The payout and float events on multi-currency
/// devices instead carry a count byte followed by that many equal-sized blocks, one per
/// currency, and one adds a trailing byte after the last block.  A handful of events are listed
/// in the manual with no size at all; those are `unknown()`, and a decode stops when it reaches
/// one rather than guessing a length and reading the rest of the reply out of step.
struct EventPayload {
    enum class Kind : uint8_t { Unknown = 0, Fixed, CountPrefixed };

    Kind kind = Kind::Unknown;
    uint8_t size = 0;          ///< For Fixed: the payload length.
    uint8_t block_size = 0;    ///< For CountPrefixed: the size of one repeated block.
    uint8_t trailer_size = 0;  ///< For CountPrefixed: the bytes after the last block.

    static constexpr EventPayload none() noexcept { return fixed(0); }
    static constexpr EventPayload unknown() noexcept { return EventPayload{}; }
    static constexpr EventPayload fixed(uint8_t size) noexcept {
        return EventPayload{Kind::Fixed, size, 0, 0};
    }
    /// A count byte, then that many `block_size` blocks, then `trailer_size` bytes.  A zero
    /// block size cannot be measured and behaves as unknown.
    static constexpr EventPayload count_prefixed(uint8_t block_size, uint8_t trailer_size = 0) noexcept {
        return EventPayload{Kind::CountPrefixed, 0, block_size, trailer_size};
    }

    /// Works out how many bytes of `data` — the reply from just after the event code — this
    /// payload occupies.
    /// @return false if the payload is unknown, or the reply ends before the payload does.
    bool measure(ByteView data, size_t& length) const noexcept;

    std::string to_string() const;
};

inline bool operator==(const EventPayload& a, const EventPayload& b) noexcept {
    return a.kind == b.kind && a.size == b.size && a.block_size == b.block_size && a.trailer_size == b.trailer_size;
}
inline bool operator!=(const EventPayload& a, const EventPayload& b) noexcept { return !(a == b); }

/// How long each event's payload is, at each protocol version.
///
/// This is the one piece of knowledge a poll reply cannot be read without, and the one most
/// likely to go out of date, so it is data rather than code and it is immutable: `with_event()`
/// returns a new table rather than altering a shared one.  A device newer than this library can
/// therefore be supported without waiting for a release:
///
///     auto table = EventTable::standard().with_event(0x7C, 10, EventPayload::fixed(4));
///
/// `standard()` is built from issue 2.2 of the SSP protocol manual, the same source as the .NET
/// library's SspEventTable.Default, and covers versions 4 to 9.
class EventTable {
public:
    /// The table built from the protocol manual.
    static const EventTable& standard();

    /// Finds the payload shape for an event at a protocol version.  A version above every
    /// variant gets the newest.
    /// @return false when there is no rule for the code at or below the version, or the rule
    ///         says the length is unknown.
    bool find(uint8_t code, uint8_t version, EventPayload& payload) const noexcept;

    /// A copy of this table with one event's shape added or replaced from `first_version` on.
    /// Registering at a version that already has a shape replaces it; registering at a new one
    /// leaves the others in place, so an event whose payload grew can be described one
    /// revision at a time.
    EventTable with_event(uint8_t code, uint8_t first_version, EventPayload payload) const;

    /// A copy of this table with an event removed entirely.
    EventTable without_event(uint8_t code) const;

    /// Every event code this table has a rule for, in ascending order.
    std::vector<uint8_t> codes() const;

private:
    struct Variant {
        uint8_t first_version;
        EventPayload payload;
    };

    struct Entry {
        uint8_t code;
        std::vector<Variant> variants;  // ascending by first_version
    };

    static EventTable build_standard();
    void put(uint8_t code, uint8_t first_version, EventPayload payload);

    std::vector<Entry> entries_;  // ascending by code
};

}  // namespace smileysecure
