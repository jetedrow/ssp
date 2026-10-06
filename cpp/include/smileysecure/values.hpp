// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <array>
#include <cstdint>
#include <string>

#include "smileysecure/bytes.hpp"
#include "smileysecure/error.hpp"

namespace smileysecure {

/// The few value encodings SSP uses inside command parameters and event payloads.
///
/// Monetary amounts are little-endian, serial numbers are big-endian, and country codes are
/// three ASCII letters.  Mixing the two byte orders up is the easiest mistake to make against
/// this protocol, so both are named here rather than written out at each call site.
namespace values {

/// The length of a country code in a payload.
constexpr size_t kCountryCodeLength = 3;

/// Reads a four-byte little-endian amount, in the dataset's smallest unit — cents, not euros.
Result<uint32_t> read_amount(ByteView data);

/// Writes a four-byte little-endian amount.
std::array<uint8_t, 4> write_amount(uint32_t value) noexcept;

/// Reads a four-byte big-endian number.  Serial numbers are sent this way round.
Result<uint32_t> read_big_endian(ByteView data);

/// Reads an eight-byte little-endian number, as the key exchange sends its three numbers.
Result<uint64_t> read_uint64(ByteView data);

/// Writes an eight-byte little-endian number.
std::array<uint8_t, 8> write_uint64(uint64_t value) noexcept;

/// Reads a three-letter ASCII country code, as in "EUR".
Result<std::string> read_country_code(ByteView data);

/// Writes a three-letter ASCII country code.
/// @return InvalidArgument unless the code is exactly three characters.
Result<std::array<uint8_t, 3>> write_country_code(const std::string& country_code);

/// Reads bytes as ASCII text, as firmware and dataset versions are reported.
std::string ascii(ByteView data);

}  // namespace values
}  // namespace smileysecure
