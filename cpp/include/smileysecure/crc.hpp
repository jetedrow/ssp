// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>

#include "smileysecure/bytes.hpp"

namespace smileysecure {

/// SSP's CRC-16: polynomial 0x8005, seeded with 0xFFFF, not reflected.
///
/// A packet's CRC covers everything between the STX and the CRC itself — the sequence/address
/// byte, the length and the data — and is sent low byte first.  The eSSP envelope uses the same
/// CRC over its decrypted block.
uint16_t crc16(ByteView data) noexcept;

/// Low byte of a CRC, the one sent first.
constexpr uint8_t crc_low(uint16_t crc) noexcept { return static_cast<uint8_t>(crc & 0xFF); }

/// High byte of a CRC, the one sent second.
constexpr uint8_t crc_high(uint16_t crc) noexcept { return static_cast<uint8_t>(crc >> 8); }

}  // namespace smileysecure
