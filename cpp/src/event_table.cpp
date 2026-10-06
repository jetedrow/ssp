// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/event_table.hpp"

#include <algorithm>
#include <cstdio>
#include <initializer_list>

#include "smileysecure/codes.hpp"

namespace smileysecure {

bool EventPayload::measure(ByteView data, size_t& length) const noexcept {
    length = 0;

    switch (kind) {
        case Kind::Fixed:
            if (data.size() < size) return false;
            length = size;
            return true;

        case Kind::CountPrefixed: {
            if (block_size == 0 || data.empty()) return false;
            // The count byte itself is part of the payload.
            const size_t measured = 1 + static_cast<size_t>(data[0]) * block_size + trailer_size;
            if (data.size() < measured) return false;
            length = measured;
            return true;
        }

        case Kind::Unknown:
            break;
    }

    return false;
}

std::string EventPayload::to_string() const {
    char text[64];
    switch (kind) {
        case Kind::Fixed:
            std::snprintf(text, sizeof text, "%u byte(s)", static_cast<unsigned>(size));
            return text;
        case Kind::CountPrefixed:
            if (trailer_size == 0) {
                std::snprintf(text, sizeof text, "count + n x %u byte(s)", static_cast<unsigned>(block_size));
            } else {
                std::snprintf(text, sizeof text, "count + n x %u byte(s) + %u byte(s)",
                              static_cast<unsigned>(block_size), static_cast<unsigned>(trailer_size));
            }
            return text;
        case Kind::Unknown:
            break;
    }
    return "unknown length";
}

const EventTable& EventTable::standard() {
    static const EventTable table = build_standard();
    return table;
}

bool EventTable::find(uint8_t code, uint8_t version, EventPayload& payload) const noexcept {
    payload = EventPayload::unknown();

    const auto entry = std::lower_bound(entries_.begin(), entries_.end(), code,
                                        [](const Entry& e, uint8_t c) { return e.code < c; });
    if (entry == entries_.end() || entry->code != code) return false;

    // Walk back to the newest variant at or below the version, so a version above every variant
    // gets the newest — which is what a device reporting an unreleased version does in practice.
    for (auto variant = entry->variants.rbegin(); variant != entry->variants.rend(); ++variant) {
        if (variant->first_version <= version) {
            payload = variant->payload;
            return payload.kind != EventPayload::Kind::Unknown;
        }
    }

    // Every variant was added after this version, so the device should never send it.
    return false;
}

EventTable EventTable::with_event(uint8_t code, uint8_t first_version, EventPayload payload) const {
    EventTable copy = *this;
    copy.put(code, first_version, payload);
    return copy;
}

void EventTable::put(uint8_t code, uint8_t first_version, EventPayload payload) {
    auto entry = std::lower_bound(entries_.begin(), entries_.end(), code,
                                  [](const Entry& e, uint8_t c) { return e.code < c; });
    if (entry == entries_.end() || entry->code != code) {
        entry = entries_.insert(entry, Entry{code, {}});
    }

    auto& variants = entry->variants;
    variants.erase(std::remove_if(variants.begin(), variants.end(),
                                  [&](const Variant& v) { return v.first_version == first_version; }),
                   variants.end());
    const auto position = std::lower_bound(variants.begin(), variants.end(), first_version,
                                           [](const Variant& v, uint8_t version) { return v.first_version < version; });
    variants.insert(position, Variant{first_version, payload});
}

EventTable EventTable::without_event(uint8_t code) const {
    EventTable copy = *this;
    copy.entries_.erase(std::remove_if(copy.entries_.begin(), copy.entries_.end(),
                                       [&](const Entry& e) { return e.code == code; }),
                        copy.entries_.end());
    return copy;
}

std::vector<uint8_t> EventTable::codes() const {
    std::vector<uint8_t> codes;
    codes.reserve(entries_.size());
    for (const auto& entry : entries_) codes.push_back(entry.code);
    return codes;
}

EventTable EventTable::build_standard() {
    EventTable table;

    const auto add_versioned = [&table](Event code, std::initializer_list<Variant> variants) {
        for (const auto& variant : variants) {
            table.put(static_cast<uint8_t>(code), variant.first_version, variant.payload);
        }
    };
    const auto add = [&](Event code, uint8_t first_version, EventPayload payload) {
        add_versioned(code, {Variant{first_version, payload}});
    };

    // Blocks repeated once per currency in the device's dataset.  A payout block reports both
    // what moved and what was asked for; a value block reports one amount.
    constexpr uint8_t kValueBlock = 4 + 3;       // value, then a three-letter country code
    constexpr uint8_t kPayoutBlock = 4 + 4 + 3;  // value moved, value requested, country code

    const EventPayload none = EventPayload::none();
    const auto fixed = EventPayload::fixed;
    const auto counted = [](uint8_t block) { return EventPayload::count_prefixed(block); };

    add(Event::SlaveReset, 4, none);
    add_versioned(Event::Read, {{4, fixed(1)}, {9, fixed(7)}});
    add_versioned(Event::NoteCredit, {{4, fixed(1)}, {9, fixed(7)}});
    add(Event::Rejecting, 4, none);
    add(Event::Rejected, 4, none);
    add(Event::Stacked, 4, none);
    add(Event::SafeJam, 4, none);
    add(Event::UnsafeJam, 4, none);
    add(Event::Disabled, 4, none);
    add(Event::StackerFull, 4, none);
    add(Event::FraudAttempt, 4, fixed(1));
    add(Event::BarcodeTicketValidated, 4, none);
    add(Event::CashboxReplaced, 5, none);
    add(Event::CashboxRemoved, 5, none);
    add(Event::NoteClearedIntoCashbox, 5, fixed(1));
    add(Event::NoteClearedFromFront, 4, fixed(1));
    add(Event::NotePathOpen, 6, none);
    add_versioned(Event::CoinCredit, {{5, fixed(4)}, {6, fixed(7)}});
    add_versioned(Event::CashboxPaid, {{5, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::IncompleteFloat, {{5, fixed(8)}, {6, counted(kPayoutBlock)}});
    add_versioned(Event::IncompletePayout, {{4, fixed(8)}, {6, counted(kPayoutBlock)}});
    add_versioned(Event::NoteStoredInPayout, {{4, none}, {6, fixed(8)}});
    add_versioned(Event::Dispensing, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::Timeout, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::Floated, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::Floating, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::Halted, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::HopperJammed, {{5, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::Dispensed, {{4, fixed(4)}, {6, counted(kValueBlock)}});
    add(Event::BarcodeTicketAck, 4, none);
    add(Event::DeviceFull, 5, none);
    add(Event::NoteHeldInBezel, 8, fixed(7));
    add(Event::NoteDispensedAtReset, 6, fixed(7));
    add(Event::Stacking, 4, none);
    add(Event::NoteIntoStoreAtReset, 8, fixed(7));
    add(Event::NoteIntoStackerAtReset, 8, fixed(7));
    add(Event::NoteTransferedToStacker, 6, fixed(7));
    add(Event::NoteFloatAttached, 5, none);
    add(Event::NoteFloatRemoved, 5, none);
    add(Event::PayoutOutOfService, 4, none);
    add(Event::CoinMechReturnActive, 5, none);
    add(Event::CoinMechJammed, 5, none);
    add(Event::Emptied, 5, none);
    add(Event::Emptying, 5, none);
    add(Event::ValueAdded, 7, counted(kValueBlock));
    add(Event::AttachedCoinMechEnabled, 6, none);
    add(Event::AttachedCoinMechDisabled, 6, none);
    add(Event::CoinMechError, 7, fixed(1));
    add(Event::Initialising, 7, none);
    add(Event::ChannelDisable, 7, none);
    add_versioned(Event::SmartEmptied, {{5, fixed(4)}, {6, counted(kValueBlock)}});
    add_versioned(Event::SmartEmptying, {{5, fixed(4)}, {6, counted(kValueBlock)}});

    // Alone among the count-prefixed events, this one ends with a byte saying what failed.
    add(Event::ErrorDuringPayout, 7, EventPayload::count_prefixed(kValueBlock, 1));

    add(Event::JamRecovery, 7, none);
    add(Event::PrintedToCashbox, 6, none);
    add(Event::PrintHalted, 6, none);
    add(Event::TicketInBezel, 6, none);
    add(Event::PaperReplaced, 6, none);
    add(Event::NoPaper, 6, none);
    add(Event::TicketPathClosed, 6, none);
    add(Event::PrinterHeadReplaced, 6, none);
    add(Event::TicketPrintingError, 6, fixed(1));
    add(Event::TicketPrinted, 6, none);
    add(Event::TicketPrinting, 6, none);
    add(Event::TicketJam, 6, none);
    add(Event::TicketPathOpen, 6, none);
    add(Event::PrinterHeadRemoved, 6, none);
    add(Event::TicketsReplaced, 6, none);
    add(Event::TicketsLow, 6, none);
    add(Event::CashboxUnlockEnabled, 6, none);
    add(Event::CashboxBackInService, 6, none);
    add(Event::CashboxTamper, 4, none);
    add(Event::CashboxOutOfService, 6, fixed(1));
    add(Event::CalibrationFailed, 7, fixed(1));

    // The manual names these events but gives no payload size for them and prints no worked
    // packet to read one off.  Guessing zero would be a coin flip that desynchronises the rest of
    // the reply when it lost, so they are declared unknown: a decode stops at one and says so.
    // A host that knows better can supply the size with with_event().
    const EventPayload unknown = EventPayload::unknown();
    add(Event::CoinsLow, 4, unknown);
    add(Event::MaintenanceRequired, 4, unknown);
    add(Event::CoinRejected, 4, unknown);
    add(Event::TicketInBezelAtStartup, 4, unknown);
    add(Event::EscrowActive, 4, unknown);

    return table;
}

}  // namespace smileysecure
