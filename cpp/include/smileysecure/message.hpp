// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "smileysecure/bytes.hpp"
#include "smileysecure/codes.hpp"
#include "smileysecure/error.hpp"

namespace smileysecure {

/// Builds the data half of a command packet: the command code, then its parameters.
///
/// Framing, addressing, the sequence flag and the CRC all belong to the layer below, so what is
/// built here is only what goes in a packet's data field.
/// @return InvalidArgument if the command and its parameters would not fit in one packet.
Result<std::vector<uint8_t>> make_message(uint8_t command, ByteView parameters = {});

inline Result<std::vector<uint8_t>> make_message(Command command, ByteView parameters = {}) {
    return make_message(static_cast<uint8_t>(command), parameters);
}

/// A device's reply to a command, split into its response code and the data behind it.
///
/// Every SSP reply starts with a response code.  For most commands an OK is followed by
/// whatever that command returns; for a poll it is followed by the event list.
struct Reply {
    uint8_t code = 0;
    std::vector<uint8_t> data;

    Response response() const noexcept { return static_cast<Response>(code); }
    bool ok() const noexcept { return code == static_cast<uint8_t>(Response::Ok); }

    /// Splits a reply packet's data into a response code and the rest.
    /// @return PacketFormat if the reply carries no response code at all.
    static Result<Reply> parse(ByteView payload);

    /// Success if the device accepted the command, otherwise `Refused` carrying its code.
    Status ensure_ok() const noexcept;

    std::string to_string() const;
};

}  // namespace smileysecure
