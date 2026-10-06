// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/crc.hpp"
#include "smileysecure/packet.hpp"
#include "support.hpp"
#include "test.hpp"

using namespace smileysecure;
using sstest::bytes;

namespace {

// A SYNC command with the sequence flag set, address 0x00.
const std::vector<uint8_t> kSync = bytes("7F 80 01 11 65 82");

Result<Packet> round_trip(const Packet& packet, bool sequence_flag) {
    auto logical = packet.encode(sequence_flag);
    if (!logical) return logical.status();

    sstest::MemoryStream wire;
    const Status written = write_packet(wire, *logical);
    if (!written) return written;

    sstest::MemoryStream reader;
    reader.feed(wire.written);
    reader.close();

    sstest::FakeClock clock;
    Frame frame;
    const Status read = read_packet(reader, clock, clock.now + 1000, frame);
    if (!read) return read;
    return Packet::parse(frame.view(), sequence_flag);
}

Status read_from(const std::vector<uint8_t>& wire, Frame& frame) {
    sstest::MemoryStream stream;
    stream.feed(wire);
    stream.close();
    sstest::FakeClock clock;
    return read_packet(stream, clock, clock.now + 1000, frame);
}

int count_lone_stx(const std::vector<uint8_t>& wire) {
    int lone = 0;
    for (size_t i = 0; i < wire.size(); ++i) {
        if (wire[i] != kStx) continue;
        if (i + 1 < wire.size() && wire[i + 1] == kStx) {
            ++i;  // a stuffed pair
            continue;
        }
        ++lone;
    }
    return lone;
}

}  // namespace

TEST(crc_matches_the_packets_printed_in_the_manual) {
    // Every packet the manual prints carries its own CRC, so these check the implementation
    // against a source outside this repository.
    for (const char* hex : {"7F 80 01 07 12 02", "7F 80 04 F0 EE 01 EB B9 48",
                            "7F 80 11 F0 BF 02 DC 00 00 00 45 55 52 68 01 00 00 47 42 50 D1 05"}) {
        const auto packet = bytes(hex);
        const uint16_t crc = crc16(ByteView(packet.data() + 1, packet.size() - 3));
        CHECK_EQ(crc_low(crc), packet[packet.size() - 2]);
        CHECK_EQ(crc_high(crc), packet[packet.size() - 1]);
    }
}

TEST(a_simple_command_parses) {
    auto packet = Packet::parse(kSync);
    REQUIRE(packet.ok());
    CHECK_EQ(packet->address, 0);
    CHECK_EQ(packet->data, std::vector<uint8_t>{0x11});
}

TEST(a_sequence_flag_mismatch_is_a_format_error) {
    CHECK(Packet::parse(kSync, true).ok());
    CHECK_EQ(Packet::parse(kSync, false).status().error, Error::PacketFormat);
}

TEST(a_constructed_packet_encodes_to_the_manuals_bytes) {
    auto encoded = Packet{0x00, {0x11}}.encode(true);
    REQUIRE(encoded.ok());
    CHECK_EQ(*encoded, kSync);
}

TEST(a_payload_containing_stx_survives_a_round_trip) {
    const std::vector<uint8_t> payload{0x11, kStx, 0x22, kStx, kStx, 0x33};
    auto parsed = round_trip(Packet{0x00, payload}, true);
    REQUIRE(parsed.ok());
    CHECK_EQ(parsed->data, payload);
}

TEST(every_payload_byte_value_survives_a_round_trip) {
    std::vector<uint8_t> payload;
    for (int i = 0; i < 255; ++i) payload.push_back(static_cast<uint8_t>(i));

    auto parsed = round_trip(Packet{0x07, payload}, false);
    REQUIRE(parsed.ok());
    CHECK_EQ(parsed->data, payload);
    CHECK_EQ(parsed->address, 0x07);
}

TEST(a_maximum_sized_packet_is_accepted) {
    auto parsed = round_trip(Packet{0x00, std::vector<uint8_t>(kMaxDataLength, 0xA5)}, false);
    REQUIRE(parsed.ok());
    CHECK_EQ(parsed->data.size(), kMaxDataLength);
}

TEST(a_packet_with_too_much_data_does_not_encode) {
    const Packet oversized{0x00, std::vector<uint8_t>(256, 0)};
    CHECK_EQ(oversized.encode(false).status().error, Error::PacketLength);
}

TEST(the_writer_doubles_every_stx_after_the_start_marker) {
    auto logical = Packet{0x00, {kStx}}.encode(true);
    REQUIRE(logical.ok());

    sstest::MemoryStream wire;
    REQUIRE(write_packet(wire, *logical).ok());

    CHECK_EQ(wire.written[0], kStx);
    CHECK_EQ(count_lone_stx(wire.written), 1);
    CHECK(wire.written.size() > logical->size());
}

TEST(the_reader_skips_noise_before_the_start_marker) {
    Frame frame;
    REQUIRE(read_from(bytes("B6 C2 7F 80 01 11 65 82 ED AA"), frame).ok());
    CHECK_EQ(frame.view().to_vector(), kSync);
}

TEST(the_reader_removes_byte_stuffing) {
    Frame frame;
    REQUIRE(read_from(bytes("7F 80 02 11 7F 7F 65 82"), frame).ok());
    CHECK_EQ(frame.view().to_vector(), bytes("7F 80 02 11 7F 65 82"));
}

TEST(a_lone_stx_inside_a_packet_is_a_format_error) {
    Frame frame;
    CHECK_EQ(read_from(bytes("7F 80 02 11 7F 65 82"), frame).error, Error::PacketFormat);
}

TEST(the_reader_reads_consecutive_packets) {
    sstest::MemoryStream wire;
    REQUIRE(write_packet(wire, *Packet{0x00, {0x11}}.encode(true)).ok());
    REQUIRE(write_packet(wire, *Packet{0x00, {0x07, 0x2A}}.encode(false)).ok());

    sstest::MemoryStream stream;
    stream.feed(wire.written);
    stream.close();
    sstest::FakeClock clock;

    Frame first;
    Frame second;
    REQUIRE(read_packet(stream, clock, clock.now + 1000, first).ok());
    REQUIRE(read_packet(stream, clock, clock.now + 1000, second).ok());

    CHECK_EQ(Packet::parse(first.view(), true)->data, std::vector<uint8_t>{0x11});
    CHECK_EQ(Packet::parse(second.view(), false)->data, (std::vector<uint8_t>{0x07, 0x2A}));
}

TEST(a_silent_line_times_out_rather_than_blocking) {
    sstest::FakeClock clock;
    sstest::MemoryStream stream(&clock);
    Frame frame;
    CHECK_EQ(read_packet(stream, clock, clock.now + 100, frame).error, Error::Timeout);
}

TEST(a_stream_that_ends_mid_packet_is_reported) {
    Frame frame;
    CHECK_EQ(read_from(bytes("7F 80"), frame).error, Error::ConnectionClosed);
}

TEST(packets_outside_the_length_bounds_are_rejected) {
    std::vector<uint8_t> too_long(kMaxPacketLength + 1, 0);
    too_long[0] = kStx;
    CHECK_EQ(Packet::parse(too_long).status().error, Error::PacketLength);
    CHECK_EQ(Packet::parse(bytes("7F 80 00 00")).status().error, Error::PacketLength);
}

TEST(a_corrupt_crc_is_reported) {
    auto packet = kSync;
    packet.back() ^= 0xFF;
    CHECK_EQ(Packet::parse(packet).status().error, Error::PacketCrc);
}
