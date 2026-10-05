// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>
#include <cstdint>

namespace smileysecure {

/// The byte that starts every packet.  Anywhere else in a packet it is sent twice, so that a
/// receiver hunting for the start of a packet can never mistake data for a start marker.
constexpr uint8_t kStx = 0x7F;

/// The largest data payload a single packet can carry.
constexpr size_t kMaxDataLength = 255;

/// The shortest a packet can be: STX, SEQ/ADDR, LEN, and two CRC bytes, carrying no data.
constexpr size_t kMinPacketLength = 5;

/// The longest a packet can be: the framing plus a full payload.
constexpr size_t kMaxPacketLength = kMinPacketLength + kMaxDataLength;

/// The mask selecting the address out of the combined sequence/address byte.
constexpr uint8_t kAddressMask = 0x7F;

/// The bit carrying the sequence flag in the combined sequence/address byte.
constexpr uint8_t kSequenceFlagMask = 0x80;

}  // namespace smileysecure
