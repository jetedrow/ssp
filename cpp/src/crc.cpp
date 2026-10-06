// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/crc.hpp"

#include <array>

namespace smileysecure {
namespace {

constexpr uint16_t kPolynomial = 0x8005;
constexpr uint16_t kSeed = 0xFFFF;

constexpr std::array<uint16_t, 256> make_table() noexcept {
    std::array<uint16_t, 256> table{};
    for (unsigned i = 0; i < 256; ++i) {
        auto crc = static_cast<uint16_t>(i << 8);
        for (int bit = 0; bit < 8; ++bit) {
            crc = (crc & 0x8000) != 0 ? static_cast<uint16_t>((crc << 1) ^ kPolynomial) : static_cast<uint16_t>(crc << 1);
        }
        table[i] = crc;
    }
    return table;
}

// Built at compile time, so it lives in flash on a microcontroller rather than in RAM.
constexpr std::array<uint16_t, 256> kTable = make_table();

}  // namespace

uint16_t crc16(ByteView data) noexcept {
    uint16_t crc = kSeed;
    for (uint8_t byte : data) {
        crc = static_cast<uint16_t>((crc << 8) ^ kTable[((crc >> 8) ^ byte) & 0xFF]);
    }
    return crc;
}

}  // namespace smileysecure
