// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <vector>

#include "smileysecure/bytes.hpp"
#include "smileysecure/constants.hpp"
#include "smileysecure/error.hpp"
#include "smileysecure/transport.hpp"

namespace smileysecure {

/// A single SSP packet: an address and a payload.  The framing around them is added by
/// `encode()` and checked by `parse()`.
///
/// Like the .NET SspRawPacket this deals only in logical packets.  Byte stuffing is applied and
/// removed at the stream layer, by `write_packet()` and `read_packet()`, so nothing here ever
/// sees a doubled STX.
struct Packet {
    uint8_t address = 0;
    std::vector<uint8_t> data;

    /// Parses a logical packet's bytes: STX, SEQ/ADDR, LEN, the data and the two CRC bytes.
    /// @param sequence_flag when given, the flag the packet must carry.
    static Result<Packet> parse(ByteView logical, std::optional<bool> sequence_flag = std::nullopt);

    /// Builds the packet's logical bytes, unstuffed.
    /// @return PacketLength if the data is longer than one packet can carry.
    Result<std::vector<uint8_t>> encode(bool sequence_flag) const;
};

/// A logical packet as it comes off the wire, held without allocating.
struct Frame {
    std::array<uint8_t, kMaxPacketLength> bytes{};
    size_t size = 0;

    ByteView view() const noexcept { return ByteView(bytes.data(), size); }
};

/// Writes a logical packet, doubling every STX after the leading one, and flushes.
Status write_packet(Stream& stream, ByteView logical);

/// Reads the next packet, skipping anything before its STX and removing byte stuffing.
///
/// @param deadline_ms the `clock.now_ms()` value by which the whole packet must have arrived.
/// @return Timeout if it does not arrive by then; ConnectionClosed if the stream ends; and
///         PacketFormat if a lone STX turns up inside the packet, which means the reader and
///         the sender disagree about where it began.
Status read_packet(Stream& stream, Clock& clock, uint64_t deadline_ms, Frame& out);

}  // namespace smileysecure
