// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/values.hpp"

namespace smileysecure {
namespace values {

Result<uint32_t> read_amount(ByteView data) {
    if (data.size() < 4) return Status::failure(Error::InvalidArgument, "an amount is four bytes");
    return static_cast<uint32_t>(data[0]) | (static_cast<uint32_t>(data[1]) << 8) |
           (static_cast<uint32_t>(data[2]) << 16) | (static_cast<uint32_t>(data[3]) << 24);
}

std::array<uint8_t, 4> write_amount(uint32_t value) noexcept {
    return {static_cast<uint8_t>(value), static_cast<uint8_t>(value >> 8), static_cast<uint8_t>(value >> 16),
            static_cast<uint8_t>(value >> 24)};
}

Result<uint32_t> read_big_endian(ByteView data) {
    if (data.size() < 4) return Status::failure(Error::InvalidArgument, "a big-endian number is four bytes");
    return (static_cast<uint32_t>(data[0]) << 24) | (static_cast<uint32_t>(data[1]) << 16) |
           (static_cast<uint32_t>(data[2]) << 8) | static_cast<uint32_t>(data[3]);
}

Result<uint64_t> read_uint64(ByteView data) {
    if (data.size() < 8) return Status::failure(Error::InvalidArgument, "an eight-byte number is eight bytes");
    uint64_t value = 0;
    for (size_t i = 0; i < 8; ++i) value |= static_cast<uint64_t>(data[i]) << (i * 8);
    return value;
}

std::array<uint8_t, 8> write_uint64(uint64_t value) noexcept {
    std::array<uint8_t, 8> bytes{};
    for (size_t i = 0; i < 8; ++i) bytes[i] = static_cast<uint8_t>(value >> (i * 8));
    return bytes;
}

Result<std::string> read_country_code(ByteView data) {
    if (data.size() < kCountryCodeLength) return Status::failure(Error::InvalidArgument, "a country code is three bytes");
    return ascii(data.subview(0, kCountryCodeLength));
}

Result<std::array<uint8_t, 3>> write_country_code(const std::string& country_code) {
    if (country_code.size() != kCountryCodeLength) {
        return Status::failure(Error::InvalidArgument, "a country code is exactly three characters");
    }
    return std::array<uint8_t, 3>{static_cast<uint8_t>(country_code[0]), static_cast<uint8_t>(country_code[1]),
                                  static_cast<uint8_t>(country_code[2])};
}

std::string ascii(ByteView data) {
    std::string text;
    text.reserve(data.size());
    // Anything outside 7-bit ASCII is replaced, as System.Text.Encoding.ASCII does.
    for (uint8_t byte : data) text.push_back(byte < 0x80 ? static_cast<char>(byte) : '?');
    return text;
}

}  // namespace values
}  // namespace smileysecure
