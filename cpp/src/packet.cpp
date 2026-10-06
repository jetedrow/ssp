// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/packet.hpp"

#include "smileysecure/crc.hpp"

namespace smileysecure {

Result<Packet> Packet::parse(ByteView logical, std::optional<bool> sequence_flag) {
    if (logical.size() < kMinPacketLength) {
        return Status::failure(Error::PacketLength,
                               "a packet is at least 5 bytes (STX, SEQ/ADDR, LEN and two CRC bytes)");
    }

    if (logical.size() > kMaxPacketLength) {
        return Status::failure(Error::PacketLength,
                               "a packet is at most 260 bytes (5 of framing plus up to 255 data bytes)");
    }

    if (logical[0] != kStx) {
        return Status::failure(Error::PacketFormat, "the packet does not begin with STX (0x7F)");
    }

    // The length byte counts the data only.
    const size_t data_length = logical[2];
    if (logical.size() != data_length + kMinPacketLength) {
        return Status::failure(Error::PacketLength, "the packet's length byte disagrees with its size");
    }

    // The CRC covers everything between the STX and the CRC itself.
    const uint16_t crc = crc16(logical.subview(1, logical.size() - 3));
    if (logical[logical.size() - 2] != crc_low(crc) || logical[logical.size() - 1] != crc_high(crc)) {
        return Status::failure(Error::PacketCrc, "the packet's CRC-16 does not match its contents");
    }

    if (sequence_flag.has_value()) {
        const bool flag = (logical[1] & kSequenceFlagMask) != 0;
        if (flag != *sequence_flag) {
            return Status::failure(Error::PacketFormat,
                                   "the packet carries the wrong sequence flag, so it answers a different command");
        }
    }

    Packet packet;
    packet.address = static_cast<uint8_t>(logical[1] & kAddressMask);
    packet.data.assign(logical.begin() + 3, logical.begin() + 3 + data_length);
    return packet;
}

Result<std::vector<uint8_t>> Packet::encode(bool sequence_flag) const {
    if (data.size() > kMaxDataLength) {
        return Status::failure(Error::PacketLength, "a packet carries at most 255 data bytes");
    }

    std::vector<uint8_t> bytes;
    bytes.reserve(data.size() + kMinPacketLength);

    bytes.push_back(kStx);
    bytes.push_back(static_cast<uint8_t>((address & kAddressMask) | (sequence_flag ? kSequenceFlagMask : 0)));
    bytes.push_back(static_cast<uint8_t>(data.size()));
    bytes.insert(bytes.end(), data.begin(), data.end());

    const uint16_t crc = crc16(ByteView(bytes.data() + 1, bytes.size() - 1));
    bytes.push_back(crc_low(crc));
    bytes.push_back(crc_high(crc));
    return bytes;
}

Status write_packet(Stream& stream, ByteView logical) {
    if (logical.empty()) return Status::failure(Error::InvalidArgument, "a packet cannot be empty");
    if (logical.size() > kMaxPacketLength) return Status::failure(Error::PacketLength, "the packet is too long to send");

    // Worst case every byte after the leading STX is itself an STX and is doubled.  Kept on the
    // stack so a write costs no allocation.
    std::array<uint8_t, 1 + (kMaxPacketLength - 1) * 2> wire{};
    size_t size = 0;

    // The leading STX is never doubled; every one after it is.
    wire[size++] = logical[0];
    for (size_t i = 1; i < logical.size(); ++i) {
        wire[size++] = logical[i];
        if (logical[i] == kStx) wire[size++] = kStx;
    }

    if (!stream.write(wire.data(), size)) {
        return Status::failure(Error::TransportFailed, "the transport refused the write");
    }

    stream.flush();
    return Status::success();
}

namespace {

Status read_byte(Stream& stream, Clock& clock, uint64_t deadline_ms, uint8_t& out) {
    const uint64_t now = clock.now_ms();
    if (now >= deadline_ms) return Status::failure(Error::Timeout, "no reply arrived in time");

    const uint64_t remaining = deadline_ms - now;
    const auto timeout = static_cast<uint32_t>(remaining > UINT32_MAX ? UINT32_MAX : remaining);

    const int read = stream.read(&out, 1, timeout);
    if (read < 0) return Status::failure(Error::ConnectionClosed, "the stream ended while reading an SSP packet");
    if (read == 0) return Status::failure(Error::Timeout, "no reply arrived in time");
    return Status::success();
}

// Reads one logical byte, collapsing a stuffed STX pair back into a single byte.
Status read_unstuffed_byte(Stream& stream, Clock& clock, uint64_t deadline_ms, uint8_t& out) {
    Status status = read_byte(stream, clock, deadline_ms, out);
    if (!status || out != kStx) return status;

    // Inside a packet an STX must always be doubled.  A lone one means the sender and the reader
    // disagree about where this packet starts.
    uint8_t second = 0;
    status = read_byte(stream, clock, deadline_ms, second);
    if (!status) return status;
    if (second != kStx) {
        return Status::failure(Error::PacketFormat, "a non-byte-stuffed STX turned up inside a packet");
    }

    return Status::success();
}

}  // namespace

Status read_packet(Stream& stream, Clock& clock, uint64_t deadline_ms, Frame& out) {
    out.size = 0;

    // Anything before the start marker is noise from a partial or foreign packet; skip it.  A lone
    // STX can only be a start marker, because every STX within a packet is doubled.
    uint8_t current = 0;
    do {
        const Status status = read_byte(stream, clock, deadline_ms, current);
        if (!status) return status;
    } while (current != kStx);

    out.bytes[out.size++] = kStx;

    // The sequence/address byte, then the length, which says how much is left to read.
    for (int i = 0; i < 2; ++i) {
        const Status status = read_unstuffed_byte(stream, clock, deadline_ms, out.bytes[out.size]);
        if (!status) return status;
        ++out.size;
    }

    // The data, then the two CRC bytes.
    const size_t remaining = static_cast<size_t>(out.bytes[2]) + 2;
    for (size_t i = 0; i < remaining; ++i) {
        const Status status = read_unstuffed_byte(stream, clock, deadline_ms, out.bytes[out.size]);
        if (!status) return status;
        ++out.size;
    }

    return Status::success();
}

}  // namespace smileysecure
