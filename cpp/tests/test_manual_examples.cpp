// The protocol manual's own worked poll replies, copied byte for byte from the .NET suite
// (ManualPollExampleTests).  They are the only independent evidence that the payload lengths in
// the event table are right: get a length wrong by one and the events after it in a
// multi-event reply decode as nonsense.  Nine of the manual's 72 examples are misprinted and
// are not here; see docs/protocol-support.md.
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/message.hpp"
#include "smileysecure/poll.hpp"
#include "test.hpp"

using namespace smileysecure;

namespace {

struct Example {
    const char* packet;
    uint8_t version;  // the lowest version that decodes it
    std::vector<uint8_t> codes;
    std::vector<size_t> sizes;
};

const std::vector<Example>& examples() {
    static const std::vector<Example> all{
    {"7F 80 02 F0 C6 AB A2", 4, {0xC6}, {0}},  // Payout Out Of Service
    {"7F 80 02 F0 D1 D9 A2", 4, {0xD1}, {0}},  // Barcode Ticket Ack
    {"7F 80 02 F0 DB E5 A2", 4, {0xDB}, {0}},  // Note Stored In Payout
    {"7F 80 02 F0 E5 62 22", 4, {0xE5}, {0}},  // Barcode Ticket Validated
    {"7F 80 02 F0 E7 6D A2", 4, {0xE7}, {0}},  // Stacker Full
    {"7F 80 02 F0 E8 4F A2", 4, {0xE8}, {0}},  // Disabled
    {"7F 80 02 F0 E9 4A 22", 4, {0xE9}, {0}},  // Unsafe Jam
    {"7F 80 02 F0 EB 45 A2", 4, {0xEB}, {0}},  // Stacked
    {"7F 80 02 F0 EC 54 22", 4, {0xEC}, {0}},  // Rejected
    {"7F 80 02 F0 ED 51 A2", 4, {0xED}, {0}},  // Rejecting
    {"7F 80 03 F0 E1 00 CC 6E", 4, {0xE1}, {1}},  // Note Cleared From Front
    {"7F 80 03 F0 E6 02 C0 7C", 4, {0xE6}, {1}},  // Fraud Attempt
    {"7F 80 03 F0 EE 04 D7 CC", 4, {0xEE}, {1}},  // Note Credit
    {"7F 80 03 F0 EF 00 CF CA", 4, {0xEF}, {1}},  // Read
    {"7F 80 03 F0 EF 03 C5 CA", 4, {0xEF}, {1}},  // Read
    {"7F 80 04 F0 EE 01 EB B9 48", 4, {0xEE, 0xEB}, {1, 0}},  // Poll
    {"7F 80 02 F0 C2 B0 22", 5, {0xC2}, {0}},  // Emptying
    {"7F 80 02 F0 C3 B5 A2", 5, {0xC3}, {0}},  // Emptied
    {"7F 80 02 F0 C7 AE 22", 5, {0xC7}, {0}},  // Note Float Removed
    {"7F 80 02 F0 C8 8C 22", 5, {0xC8}, {0}},  // Note Float Attached
    {"7F 80 02 F0 E3 76 22", 5, {0xE3}, {0}},  // Cashbox Removed
    {"7F 80 02 F0 E4 67 A2", 5, {0xE4}, {0}},  // Cashbox Replaced
    {"7F 80 03 F0 E2 02 C3 E4", 5, {0xE2}, {1}},  // Note Cleared Into Cashbox
    {"7F 80 06 F0 D5 E6 00 00 00 49 DB", 5, {0xD5}, {4}},  // Hopper Jammed
    {"7F 90 02 F0 C4 A2 62", 5, {0xC4}, {0}},  // Coin Mech Jammed
    {"7F 90 06 F0 DE C8 00 00 00 68 00", 5, {0xDE}, {4}},  // Cashbox Paid
    {"7F 80 02 F0 92 50 23", 6, {0x92}, {0}},  // Cashbox Back In Service
    {"7F 80 02 F0 93 55 A3", 6, {0x93}, {0}},  // Cashbox Unlock Enabled
    {"7F 80 02 F0 A0 FF A3", 6, {0xA0}, {0}},  // Tickets Low
    {"7F 80 02 F0 A1 FA 23", 6, {0xA1}, {0}},  // Tickets Replaced
    {"7F 80 02 F0 A2 F0 23", 6, {0xA2}, {0}},  // Printer Head Removed
    {"7F 80 02 F0 A4 E4 23", 6, {0xA4}, {0}},  // Ticket Jam
    {"7F 80 02 F0 A5 E1 A3", 6, {0xA5}, {0}},  // Ticket Printing
    {"7F 80 02 F0 A6 EB A3", 6, {0xA6}, {0}},  // Ticket Printed
    {"7F 80 02 F0 A9 C9 A3", 6, {0xA9}, {0}},  // Printer Head Replaced
    {"7F 80 02 F0 AA C3 A3", 6, {0xAA}, {0}},  // Ticket Path Closed
    {"7F 80 02 F0 AB C6 23", 6, {0xAB}, {0}},  // No Paper
    {"7F 80 02 F0 AC D7 A3", 6, {0xAC}, {0}},  // Paper Replaced
    {"7F 80 02 F0 AD D2 23", 6, {0xAD}, {0}},  // Ticket In Bezel
    {"7F 80 02 F0 AE D8 23", 6, {0xAE}, {0}},  // Print Halted
    {"7F 80 02 F0 AF DD A3", 6, {0xAF}, {0}},  // Printed To Cashbox
    {"7F 80 02 F0 E0 7C 22", 6, {0xE0}, {0}},  // Note Path Open
    {"7F 80 03 F0 90 04 D2 48", 6, {0x90}, {1}},  // Cashbox Out Of Service
    {"7F 80 03 F0 A8 08 F9 58", 6, {0xA8}, {1}},  // Ticket Printing Error
    {"7F 80 09 F0 C9 F4 01 00 00 45 55 52 DA C9", 6, {0xC9}, {7}},  // Note Transfered To Stacker
    {"7F 80 09 F0 CD E8 03 00 00 45 55 52 02 64", 6, {0xCD}, {7}},  // Note Dispensed At Reset
    {"7F 80 0A F0 B3 01 D4 08 00 00 45 55 52 44 F6", 6, {0xB3}, {8}},  // Smart Emptying
    {"7F 80 0A F0 D6 01 FA 05 00 00 45 55 52 4D 49", 6, {0xD6}, {8}},  // Halted
    {"7F 80 0A F0 D8 01 02 08 00 00 45 55 52 81 C0", 6, {0xD8}, {8}},  // Floated
    {"7F 90 02 F0 BD B7 E3", 6, {0xBD}, {0}},  // Attached Coin Mech Disabled
    {"7F 90 02 F0 BE BD E3", 6, {0xBE}, {0}},  // Attached Coin Mech Enabled
    {"7F 90 09 F0 DF F4 01 00 00 47 42 50 89 0F", 6, {0xDF}, {7}},  // Coin Credit
    {"7F 90 11 F0 DE 02 12 02 00 00 47 42 50 14 00 00 00 45 55 52 3A 50", 6, {0xDE}, {15}},  // Cashbox Paid
    {"7F 80 02 F0 B0 9C 23", 7, {0xB0}, {0}},  // Jam Recovery
    {"7F 80 02 F0 B5 82 23", 7, {0xB5}, {0}},  // Channel Disable
    {"7F 80 02 F0 B6 88 23", 7, {0xB6}, {0}},  // Initialising
    {"7F 80 03 F0 83 03 C0 22", 7, {0x83}, {1}},  // Calibration Failed
    {"7F 80 03 F0 B7 14 B1 1A", 7, {0xB7}, {1}},  // Coin Mech Error
    {"7F 80 0A F0 BF 01 26 02 00 00 45 55 52 ED 91", 7, {0xBF}, {8}},  // Value Added
    {"7F 80 11 F0 BF 02 DC 00 00 00 45 55 52 68 01 00 00 47 42 50 D1 05", 7, {0xBF}, {15}},  // Value Added
    {"7F 80 09 F0 CA F4 01 00 00 45 55 52 D0 F9", 8, {0xCA}, {7}},  // Note Into Stacker At Reset
    {"7F 80 09 F0 CB D0 07 00 00 47 42 50 B7 2D", 8, {0xCB}, {7}},  // Note Into Store At Reset
    {"7F 80 09 F0 CE E8 03 00 00 45 55 52 08 54", 8, {0xCE}, {7}},  // Note Held In Bezel
    };
    return all;
}

// The data field of a whole packet, dropping STX, SEQ/ADDR, LEN and the CRC.
std::vector<uint8_t> payload_of(const std::vector<uint8_t>& packet) {
    return std::vector<uint8_t>(packet.begin() + 3, packet.begin() + 3 + packet[2]);
}

}  // namespace

TEST(the_corpus_has_every_valid_manual_example) {
    CHECK_EQ(examples().size(), 63u);
}

TEST(each_manual_example_decodes_to_the_events_it_documents) {
    for (const auto& example : examples()) {
        auto reply = Reply::parse(payload_of(sstest::bytes(example.packet)));
        REQUIRE(reply.ok());
        CHECK(reply->ok());

        const PollResult result = decode_poll(reply->data, example.version);
        if (!result.complete()) {
            sstest::report(__FILE__, __LINE__,
                           std::string(example.packet) + " stopped: " + result.stop_message(example.version));
            continue;
        }

        std::vector<uint8_t> codes;
        std::vector<size_t> sizes;
        for (const auto& event : result.events) {
            codes.push_back(event.code);
            sizes.push_back(event.data.size());
        }
        CHECK_EQ(codes, example.codes);
        CHECK(sizes == example.sizes);
    }
}

// The version recorded against each packet is the lowest one it decodes at, so decoding it one
// version lower must fail.  Without this, a table that had every event arriving at version 4
// would pass the test above.
TEST(a_manual_example_does_not_decode_one_version_below_the_one_it_needs) {
    for (const auto& example : examples()) {
        if (example.version <= protocol_version::kLowest) continue;

        auto reply = Reply::parse(payload_of(sstest::bytes(example.packet)));
        REQUIRE(reply.ok());

        const auto lower = static_cast<uint8_t>(example.version - 1);
        if (decode_poll(reply->data, lower).complete()) {
            sstest::report(__FILE__, __LINE__, std::string(example.packet) + " decoded below its version");
        }
    }
}

TEST(two_events_in_one_reply_are_read_in_order_with_their_own_data) {
    auto reply = Reply::parse(payload_of(sstest::bytes("7F 80 04 F0 EE 01 EB B9 48")));
    REQUIRE(reply.ok());

    const PollResult result = decode_poll(reply->data, 4);
    REQUIRE(result.events.size() == 2);
    CHECK(result.events[0].event() == Event::NoteCredit);
    CHECK_EQ(result.events[0].data, std::vector<uint8_t>{0x01});
    CHECK(result.events[1].event() == Event::Stacked);
    CHECK(result.events[1].data.empty());
}
