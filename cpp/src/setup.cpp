// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/setup.hpp"

#include "smileysecure/values.hpp"

namespace smileysecure {
namespace {

bool is_banknote_validator(UnitType unit_type) noexcept {
    switch (unit_type) {
        case UnitType::BanknoteValidator:
        case UnitType::SmartPayout:
        case UnitType::NoteFloat:
        case UnitType::Tebs:
        case UnitType::TebsWithSmartPayout:
        case UnitType::TebsWithSmartTicket:
            return true;
        default:
            return false;
    }
}

// The two multipliers in a setup reply are the only big-endian values in it; the per-channel
// values beside them are little-endian.
uint32_t read_big_endian_24(ByteView data) noexcept {
    return (static_cast<uint32_t>(data[0]) << 16) | (static_cast<uint32_t>(data[1]) << 8) | data[2];
}

void read_validator(ByteView data, Setup& setup) {
    // Layout, with n channels:
    //   0  unit type              8   value multiplier (3, big endian)
    //   1  firmware (4 ascii)     11  channel count, n
    //   5  country code (3)       12  channel values, one byte each (legacy)
    //   12 + n       channel security, one byte each (obsolete)
    //   12 + 2n      real value multiplier (3, big endian)
    //   15 + 2n      protocol version
    // and from protocol version 6, the part that actually carries the money:
    //   16 + 2n      country code per channel (3 ascii each)
    //   16 + 5n      value per channel (4, LITTLE endian -- unlike the multipliers)
    //   16 + 9n      end
    constexpr size_t kCountOffset = 11;
    if (data.size() <= kCountOffset) return;

    setup.value_multiplier = read_big_endian_24(data.subview(8, 3));
    const size_t count = data[kCountOffset];

    const size_t version_offset = 15 + count * 2;
    if (data.size() > version_offset && data[version_offset] != 0) {
        setup.protocol_version = data[version_offset];
    }

    if (count == 0) return;

    const size_t country_offset = 16 + count * 2;
    const size_t value_offset = 16 + count * 5;
    const size_t expanded_end = 16 + count * 9;

    setup.channels.reserve(count);

    if (data.size() >= expanded_end) {
        // Protocol version 6 and later: each channel carries its own currency and a full value,
        // so a multi-currency dataset reads correctly.
        for (size_t i = 0; i < count; ++i) {
            setup.channels.push_back(Channel{static_cast<int>(i + 1),
                                             *values::read_amount(data.subview(value_offset + i * 4, 4)),
                                             values::ascii(data.subview(country_offset + i * 3, 3))});
        }
        return;
    }

    // Before version 6 there is one currency, and a channel's value is a single byte that has to
    // be multiplied up.
    constexpr size_t kLegacyValueOffset = 12;
    for (size_t i = 0; i < count; ++i) {
        const uint32_t raw = kLegacyValueOffset + i < data.size() ? data[kLegacyValueOffset + i] : 0;
        setup.channels.push_back(Channel{static_cast<int>(i + 1), raw * setup.value_multiplier, setup.country_code});
    }
}

}  // namespace

Result<Setup> Setup::parse(ByteView data) {
    // Unit type, four firmware bytes and a three-byte country code are common to every device
    // type; everything after that depends on which device answered.
    constexpr size_t kHeaderLength = 8;
    if (data.size() < kHeaderLength) return Status::failure(Error::PacketFormat, "a setup reply is at least 8 bytes");

    Setup setup;
    setup.unit_type = static_cast<UnitType>(data[0]);
    setup.firmware_version = values::ascii(data.subview(1, 4));
    setup.country_code = values::ascii(data.subview(5, 3));
    setup.data = data.to_vector();

    if (is_banknote_validator(setup.unit_type)) read_validator(data, setup);

    return setup;
}

}  // namespace smileysecure
