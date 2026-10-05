// SPDX-License-Identifier: LGPL-3.0-or-later
#include <algorithm>

#include "smileysecure/constants.hpp"
#include "smileysecure/event_table.hpp"
#include "smileysecure/message.hpp"
#include "smileysecure/poll.hpp"
#include "smileysecure/setup.hpp"
#include "smileysecure/values.hpp"
#include "test.hpp"

using namespace smileysecure;
using V = std::vector<uint8_t>;
using sstest::bytes;

namespace {

uint8_t code(Event event) { return static_cast<uint8_t>(event); }

std::vector<uint8_t> codes_of(const PollResult& result) {
    std::vector<uint8_t> codes;
    for (const auto& event : result.events) codes.push_back(event.code);
    return codes;
}

}  // namespace

// ---- replies and messages -------------------------------------------------------------------

TEST(a_reply_splits_into_its_response_code_and_the_rest) {
    // The manual's Get Serial Number example: OK, then the serial big-endian.
    auto reply = Reply::parse(bytes("F0 00 1C 96 2C"));
    REQUIRE(reply.ok());
    CHECK(reply->ok());
    CHECK(reply->response() == Response::Ok);
    CHECK_EQ(*values::read_big_endian(reply->data), 1873452u);
}

TEST(an_empty_reply_is_rejected) {
    CHECK_EQ(Reply::parse(ByteView()).status().error, Error::PacketFormat);
}

TEST(a_refusal_carries_the_code_the_device_gave) {
    for (uint8_t refusal : {0xF2, 0xF3, 0xFA}) {
        const Reply reply{refusal, {}};
        CHECK(!reply.ok());
        const Status status = reply.ensure_ok();
        CHECK_EQ(status.error, Error::Refused);
        CHECK_EQ(status.response, refusal);
    }
}

TEST(a_response_code_this_library_does_not_name_still_reports_its_value) {
    const Status status = Reply{0xFC, {}}.ensure_ok();
    CHECK(status.describe().find("0xFC") != std::string::npos);
}

TEST(a_command_with_no_parameters_is_just_its_code) {
    CHECK_EQ(*make_message(Command::Poll), std::vector<uint8_t>{0x07});
}

TEST(a_command_carries_its_parameters_after_its_code) {
    CHECK_EQ(*make_message(Command::HostProtocolVersion, V{0x06}), (std::vector<uint8_t>{0x06, 0x06}));
}

TEST(a_command_this_library_does_not_name_can_still_be_built) {
    CHECK_EQ(*make_message(0x7C, V{0x01, 0x02}), (std::vector<uint8_t>{0x7C, 0x01, 0x02}));
}

TEST(a_command_too_long_for_a_packet_is_rejected) {
    const std::vector<uint8_t> parameters(kMaxDataLength, 0);
    CHECK_EQ(make_message(Command::Poll, parameters).status().error, Error::InvalidArgument);
}

// ---- values ---------------------------------------------------------------------------------

TEST(amounts_are_little_endian_and_serial_numbers_big_endian) {
    CHECK_EQ(*values::read_amount(bytes("88 13 00 00")), 5000u);
    CHECK_EQ(*values::read_big_endian(bytes("00 1C 96 2C")), 1873452u);
}

TEST(values_survive_a_round_trip) {
    const auto amount = values::write_amount(1234567);
    CHECK_EQ(*values::read_amount(ByteView(amount.data(), amount.size())), 1234567u);

    const auto wide = values::write_uint64(0x0123456789ABCDEFULL);
    CHECK_EQ(*values::read_uint64(ByteView(wide.data(), wide.size())), 0x0123456789ABCDEFULL);
    CHECK_EQ(wide[0], 0xEF);

    const auto country = values::write_country_code("EUR");
    REQUIRE(country.ok());
    CHECK_EQ(*values::read_country_code(ByteView(country->data(), country->size())), std::string("EUR"));
}

TEST(short_inputs_are_rejected_rather_than_overread) {
    CHECK(!values::read_amount(bytes("01 02 03")).ok());
    CHECK(!values::read_big_endian(bytes("01")).ok());
    CHECK(!values::read_uint64(bytes("01 02 03 04 05 06 07")).ok());
    CHECK(!values::read_country_code(bytes("45 55")).ok());
    CHECK(!values::write_country_code("EU").ok());
}

// ---- the event table ------------------------------------------------------------------------

TEST(a_versioned_event_resolves_to_the_newest_shape_at_or_below_the_version) {
    const std::vector<std::pair<uint8_t, uint8_t>> cases{{4, 1}, {6, 1}, {8, 1}, {9, 7}, {20, 7}};
    for (const auto& [version, size] : cases) {
        EventPayload payload;
        REQUIRE(EventTable::standard().find(code(Event::NoteCredit), version, payload));
        CHECK_EQ(payload.size, size);
    }
}

TEST(an_event_is_not_resolved_below_the_version_it_arrived_at) {
    EventPayload payload;
    CHECK(!EventTable::standard().find(code(Event::CoinCredit), 4, payload));
}

TEST(an_event_with_no_documented_size_is_not_resolved) {
    EventPayload payload;
    CHECK(!EventTable::standard().find(code(Event::CoinsLow), protocol_version::kHighest, payload));
}

TEST(an_event_the_table_has_never_heard_of_is_not_resolved) {
    EventPayload payload;
    CHECK(!EventTable::standard().find(0x7C, protocol_version::kHighest, payload));
}

TEST(an_event_can_be_added_for_a_device_newer_than_this_library) {
    const EventTable table = EventTable::standard().with_event(0x7C, 10, EventPayload::fixed(4));
    EventPayload payload;
    REQUIRE(table.find(0x7C, 10, payload));
    CHECK_EQ(payload.size, 4);
    CHECK(!table.find(0x7C, 9, payload));
}

TEST(extending_the_table_leaves_the_original_alone) {
    const EventTable extended = EventTable::standard().with_event(0x7C, 4, EventPayload::none());
    EventPayload payload;
    CHECK(extended.find(0x7C, 4, payload));
    CHECK(!EventTable::standard().find(0x7C, 4, payload));
}

TEST(registering_at_a_version_that_already_has_a_shape_replaces_it) {
    const EventTable table = EventTable::standard().with_event(code(Event::NoteCredit), 4, EventPayload::fixed(2));
    EventPayload at_four;
    EventPayload at_nine;
    REQUIRE(table.find(code(Event::NoteCredit), 4, at_four));
    REQUIRE(table.find(code(Event::NoteCredit), 9, at_nine));
    CHECK_EQ(at_four.size, 2);
    CHECK_EQ(at_nine.size, 7);
}

TEST(an_event_can_be_removed) {
    const EventTable table = EventTable::standard().without_event(code(Event::Stacked));
    EventPayload payload;
    CHECK(!table.find(code(Event::Stacked), 4, payload));
}

TEST(events_dropped_from_the_current_manual_are_still_decodable) {
    for (Event event : {Event::SafeJam, Event::CashboxTamper}) {
        EventPayload payload;
        REQUIRE(EventTable::standard().find(code(event), protocol_version::kLowest, payload));
        CHECK_EQ(payload.size, 0);
    }
}

TEST(every_named_event_has_a_rule_in_the_standard_table) {
    const auto codes = EventTable::standard().codes();
    for (int c = 0; c < 256; ++c) {
        const bool named = event_name(static_cast<uint8_t>(c)) != nullptr;
        const bool ruled = std::find(codes.begin(), codes.end(), c) != codes.end();
        CHECK_EQ(named, ruled);
    }
}

// ---- the poll decoder -----------------------------------------------------------------------

TEST(an_empty_reply_decodes_to_no_events) {
    const PollResult result = decode_poll(ByteView(), 4);
    CHECK(result.complete());
    CHECK(result.events.empty());
}

TEST(the_same_bytes_decode_differently_at_different_versions) {
    const auto stream = bytes("EE 01 E8 EB EC ED E7 E9");

    const PollResult at_four = decode_poll(stream, 4);
    CHECK(at_four.complete());
    CHECK_EQ(codes_of(at_four), bytes("EE E8 EB EC ED E7 E9"));
    CHECK_EQ(at_four.events[0].data, std::vector<uint8_t>{0x01});

    const PollResult at_nine = decode_poll(stream, 9);
    CHECK(at_nine.complete());
    REQUIRE(at_nine.events.size() == 1);
    CHECK_EQ(at_nine.events[0].data, bytes("01 E8 EB EC ED E7 E9"));
}

TEST(an_event_older_than_its_first_version_is_not_decoded) {
    const PollResult result = decode_poll(bytes("BF 00"), 6);
    CHECK(!result.complete());
    CHECK_EQ(result.stopped_at_code, 0xBF);
}

TEST(an_unknown_code_stops_the_decode_and_keeps_what_came_before) {
    const PollResult result = decode_poll(bytes("EB 7C 11 22 EC"), 4);
    CHECK(!result.complete());
    CHECK(result.stop_reason == PollResult::StopReason::NoRule);
    CHECK_EQ(result.stopped_at_code, 0x7C);
    CHECK(result.stop_message(4).find("0x7C") != std::string::npos);
    REQUIRE(result.events.size() == 1);
    CHECK(result.events[0].event() == Event::Stacked);
    CHECK_EQ(result.undecoded, bytes("7C 11 22 EC"));
}

TEST(an_event_with_no_documented_size_stops_the_decode) {
    for (uint8_t c : {0xD3, 0xC0, 0xBA, 0xA7, 0x8B}) {
        const std::vector<uint8_t> reply{c};
        const PollResult result = decode_poll(reply, 9);
        CHECK(!result.complete());
        CHECK_EQ(result.stopped_at_code, c);
    }
}

TEST(registering_a_size_lets_a_previously_undecodable_event_through) {
    const EventTable table = EventTable::standard().with_event(code(Event::CoinsLow), 4, EventPayload::none());
    const PollResult result = decode_poll(bytes("D3 EB"), 4, table);
    CHECK(result.complete());
    CHECK_EQ(codes_of(result), bytes("D3 EB"));
}

TEST(a_payload_running_past_the_end_of_the_reply_stops_the_decode) {
    // Fraud Attempt carries one byte, and there is none.
    const PollResult result = decode_poll(bytes("EB E6"), 4);
    CHECK(!result.complete());
    CHECK(result.stop_reason == PollResult::StopReason::Truncated);
    CHECK_EQ(result.stopped_at_code, 0xE6);
    CHECK(result.stop_message(4).find("more data than the reply has left") != std::string::npos);
    CHECK_EQ(result.events.size(), 1u);
}

TEST(a_count_prefixed_payload_sizes_itself_from_its_count_byte) {
    for (uint8_t currencies : {0, 1, 3}) {
        std::vector<uint8_t> stream{code(Event::Dispensed), currencies};
        stream.insert(stream.end(), static_cast<size_t>(currencies) * 7, 0x00);
        stream.push_back(code(Event::Stacked));

        const PollResult result = decode_poll(stream, 6);
        CHECK(result.complete());
        CHECK_EQ(codes_of(result), (std::vector<uint8_t>{code(Event::Dispensed), code(Event::Stacked)}));
        CHECK_EQ(result.events[0].data.size(), 1u + currencies * 7u);
    }
}

TEST(error_during_payout_carries_a_trailing_cause_byte_after_its_currency_blocks) {
    const PollResult result = decode_poll(bytes("B1 01 88 13 00 00 47 42 50 03 EB"), 7);
    CHECK(result.complete());
    CHECK_EQ(codes_of(result), bytes("B1 EB"));

    const auto& payload = result.events[0].data;
    REQUIRE(payload.size() == 9);
    CHECK_EQ(*values::read_amount(ByteView(payload).subview(1)), 5000u);
    CHECK_EQ(*values::read_country_code(ByteView(payload).subview(5)), std::string("GBP"));
    CHECK_EQ(payload[8], 0x03);
}

TEST(an_unnamed_code_is_still_reported_with_its_raw_value) {
    const EventTable table = EventTable::standard().with_event(0x7C, 4, EventPayload::fixed(1));
    const PollResult result = decode_poll(bytes("7C 42"), 4, table);
    REQUIRE(result.complete() && result.events.size() == 1);
    CHECK_EQ(result.events[0].code, 0x7C);
    CHECK(!result.events[0].is_known());
    CHECK(result.events[0].to_string().find("0x7C") != std::string::npos);
}

// ---- the setup reply ------------------------------------------------------------------------

namespace {

const char* const kGuideExample =
    "00 "                                    // unit type: banknote validator
    "30 33 33 35 "                           // firmware 0335
    "45 55 52 "                              // EUR
    "00 00 01 "                              // value multiplier 1, big endian
    "04 "                                    // four channels
    "05 0A 14 32 "                           // legacy channel values
    "02 02 02 02 "                           // channel security, obsolete
    "00 00 64 "                              // real value multiplier 100, big endian
    "06 "                                    // protocol version 6
    "45 55 52 45 55 52 45 55 52 45 55 52 "   // a country code per channel
    "05 00 00 00 0A 00 00 00 14 00 00 00 32 00 00 00";  // channel values, little endian

}  // namespace

TEST(the_guides_worked_setup_reply_reads_back_as_the_guide_annotates_it) {
    auto setup = Setup::parse(bytes(kGuideExample));
    REQUIRE(setup.ok());
    CHECK(setup->unit_type == UnitType::BanknoteValidator);
    CHECK_EQ(setup->firmware_version, std::string("0335"));
    CHECK_EQ(setup->country_code, std::string("EUR"));
    CHECK(setup->protocol_version == std::optional<uint8_t>(6));
    CHECK_EQ(setup->value_multiplier, 1u);

    REQUIRE(setup->channels.size() == 4);
    const uint32_t expected[] = {5, 10, 20, 50};
    for (size_t i = 0; i < 4; ++i) {
        CHECK_EQ(setup->channels[i].number, static_cast<int>(i + 1));
        CHECK_EQ(setup->channels[i].value, expected[i]);
        CHECK_EQ(setup->channels[i].country_code, std::string("EUR"));
    }
}

TEST(a_reply_without_the_expanded_segment_falls_back_to_the_legacy_layout) {
    auto setup = Setup::parse(bytes("00 30 31 31 30 47 42 50 00 00 64 03 05 0A 14 02 02 02 00 00 64 05"));
    REQUIRE(setup.ok());
    CHECK(setup->protocol_version == std::optional<uint8_t>(5));
    CHECK_EQ(setup->value_multiplier, 100u);
    REQUIRE(setup->channels.size() == 3);
    CHECK_EQ(setup->channels[0].value, 500u);
    CHECK_EQ(setup->channels[1].value, 1000u);
    CHECK_EQ(setup->channels[2].value, 2000u);
    CHECK_EQ(setup->channels[2].country_code, std::string("GBP"));
}

TEST(a_device_with_an_unparsed_layout_still_reports_what_it_is_and_keeps_its_bytes) {
    const auto printer = bytes("08 30 31 30 30 46 50 31 00 01 00 46 50 31 00 00 02 0E");
    auto setup = Setup::parse(printer);
    REQUIRE(setup.ok());
    CHECK(setup->unit_type == UnitType::AddonPrinter);
    CHECK_EQ(setup->firmware_version, std::string("0100"));
    CHECK(setup->channels.empty());
    CHECK(!setup->protocol_version.has_value());
    CHECK_EQ(setup->data, printer);
}

TEST(a_reply_too_short_to_be_a_setup_reply_is_rejected) {
    CHECK_EQ(Setup::parse(bytes("00 30")).status().error, Error::PacketFormat);
}
