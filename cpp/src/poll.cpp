// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/poll.hpp"

#include <cstdio>

namespace smileysecure {

std::string PollEvent::to_string() const {
    const char* name = event_name(code);
    char text[96];
    if (data.empty()) {
        std::snprintf(text, sizeof text, "%s (0x%02X)", name != nullptr ? name : "Unnamed", code);
    } else {
        std::snprintf(text, sizeof text, "%s (0x%02X), %u data byte(s)", name != nullptr ? name : "Unnamed", code,
                      static_cast<unsigned>(data.size()));
    }
    return text;
}

std::string PollResult::stop_message(uint8_t version) const {
    char text[128];
    switch (stop_reason) {
        case StopReason::NoRule:
            std::snprintf(text, sizeof text, "the event table has no payload length for 0x%02X at protocol version %u",
                          stopped_at_code, static_cast<unsigned>(version));
            return text;
        case StopReason::Truncated:
            std::snprintf(text, sizeof text, "0x%02X needs more data than the reply has left (%u byte(s))",
                          stopped_at_code, static_cast<unsigned>(undecoded.empty() ? 0 : undecoded.size() - 1));
            return text;
        case StopReason::None:
            break;
    }
    return std::string();
}

std::string PollResult::to_string() const {
    char text[96];
    if (complete()) {
        std::snprintf(text, sizeof text, "%u event(s)", static_cast<unsigned>(events.size()));
    } else {
        std::snprintf(text, sizeof text, "%u event(s), then stopped at 0x%02X", static_cast<unsigned>(events.size()),
                      stopped_at_code);
    }
    return text;
}

PollResult decode_poll(ByteView event_data, uint8_t version, const EventTable& table) {
    PollResult result;
    size_t offset = 0;

    while (offset < event_data.size()) {
        const uint8_t code = event_data[offset];
        const ByteView rest = event_data.subview(offset + 1);

        EventPayload payload;
        size_t length = 0;

        PollResult::StopReason stop = PollResult::StopReason::None;
        if (!table.find(code, version, payload)) {
            stop = PollResult::StopReason::NoRule;
        } else if (!payload.measure(rest, length)) {
            stop = PollResult::StopReason::Truncated;
        }

        if (stop != PollResult::StopReason::None) {
            result.stop_reason = stop;
            result.stopped_at_code = code;
            result.undecoded = event_data.subview(offset).to_vector();
            return result;
        }

        result.events.push_back(PollEvent{code, rest.subview(0, length).to_vector()});
        offset += 1 + length;
    }

    return result;
}

}  // namespace smileysecure
