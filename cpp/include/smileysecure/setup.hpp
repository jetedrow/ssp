// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "smileysecure/bytes.hpp"
#include "smileysecure/codes.hpp"
#include "smileysecure/error.hpp"

namespace smileysecure {

/// One note channel from a banknote validator's setup reply: a denomination the dataset
/// recognises.  Poll events identify a note by its channel number.
struct Channel {
    /// Counting from one.  This is the number a Note Credit payload carries.
    int number = 0;

    /// The note's value in the smallest unit of its currency — cents, not euros.
    uint32_t value = 0;

    /// The three-letter code of the note's currency, such as "EUR".
    std::string country_code;
};

/// A device's reply to a setup request: what it is, what firmware it runs, what protocol
/// version it is set to, and — for a banknote validator — what its dataset holds.
///
/// Every device type lays this reply out differently, so only the first eight bytes can be read
/// without knowing which device answered.  `channels` is filled for a banknote validator and its
/// payout variants; for any other unit type it is empty and `data` carries the bytes.
struct Setup {
    UnitType unit_type = UnitType::BanknoteValidator;

    /// Four ASCII digits, so "0335" means 3.35.
    std::string firmware_version;

    /// The dataset's main currency.
    std::string country_code;

    /// The protocol version the device is set to, when the reply says.
    std::optional<uint8_t> protocol_version;

    std::vector<Channel> channels;

    /// The legacy value multiplier.  Per-channel values in `channels` are already multiplied.
    uint32_t value_multiplier = 0;

    /// The whole reply, for a caller reading a layout this library does not.
    std::vector<uint8_t> data;

    /// Reads a setup reply's data, with the response code removed.
    /// @return PacketFormat if it is too short to be a setup reply.
    static Result<Setup> parse(ByteView data);
};

}  // namespace smileysecure
