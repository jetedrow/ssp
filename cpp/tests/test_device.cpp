// SPDX-License-Identifier: LGPL-3.0-or-later
#include <algorithm>

#include "smileysecure/device.hpp"
#include "support.hpp"
#include "test.hpp"

using namespace smileysecure;
using sstest::bytes;
using V = std::vector<uint8_t>;

namespace {

constexpr uint8_t kOk = 0xF0;
constexpr uint8_t kFail = 0xF8;
constexpr uint8_t kKeyNotSet = 0xFA;

uint8_t code(Command command) { return static_cast<uint8_t>(command); }

struct Harness {
    explicit Harness(DeviceOptions options = {}) : simulator(clock), bus(simulator, std::move(options), clock) {
        device = bus.device(0x00);
    }

    std::vector<uint8_t> commands_sent() const {
        std::vector<uint8_t> sent;
        for (const auto& command : simulator.received_commands) {
            if (!command.empty()) sent.push_back(command[0]);
        }
        return sent;
    }

    // Answers Host Protocol Version with OK up to `highest` and FAIL above it.
    void accept_versions_up_to(uint8_t highest) {
        simulator.handler = [this, highest](const V& command) -> V {
            if (command[0] == code(Command::HostProtocolVersion)) return V{command[1] <= highest ? kOk : kFail};
            auto found = fixed.find(command[0]);
            return found != fixed.end() ? found->second : V{0xF2};
        };
    }

    sstest::FakeClock clock;
    sstest::DeviceSimulator simulator;
    Bus bus;
    Device* device = nullptr;
    std::map<uint8_t, V> fixed;
};

V guide_setup_reply() {
    return bytes(
        "F0 00 30 33 33 35 45 55 52 00 00 01 04 05 0A 14 32 02 02 02 02 00 00 64 06 "
        "45 55 52 45 55 52 45 55 52 45 55 52 05 00 00 00 0A 00 00 00 14 00 00 00 32 00 00 00");
}

}  // namespace

TEST(an_accepted_command_completes_quietly) {
    Harness h;
    h.simulator.respond(code(Command::Enable), {kOk});

    CHECK(h.device->enable().ok());
    CHECK_EQ(h.simulator.received_commands, std::vector<V>{V{code(Command::Enable)}});
}

TEST(a_refused_command_carries_the_code_the_device_gave) {
    Harness h;
    h.simulator.respond(code(Command::Enable), {kKeyNotSet});

    const Status status = h.device->enable();
    CHECK_EQ(status.error, Error::Refused);
    CHECK_EQ(status.response, kKeyNotSet);
}

TEST(negotiation_walks_down_until_the_device_accepts_a_version) {
    Harness h;
    h.accept_versions_up_to(7);

    auto agreed = h.device->negotiate_protocol_version();
    REQUIRE(agreed.ok());
    CHECK_EQ(*agreed, 7);
    CHECK_EQ(h.device->protocol_version(), 7);

    V tried;
    for (const auto& command : h.simulator.received_commands) tried.push_back(command[1]);
    CHECK_EQ(tried, (V{9, 8, 7}));
}

TEST(negotiation_never_goes_above_the_hosts_own_ceiling) {
    DeviceOptions options;
    options.highest_protocol_version = 6;
    Harness h(options);
    h.accept_versions_up_to(9);

    auto agreed = h.device->negotiate_protocol_version();
    REQUIRE(agreed.ok());
    CHECK_EQ(*agreed, 6);
    CHECK_EQ(h.simulator.received_commands, std::vector<V>{V{code(Command::HostProtocolVersion), 6}});
}

TEST(a_device_that_accepts_no_version_at_all_is_reported) {
    Harness h;
    h.accept_versions_up_to(0);

    auto agreed = h.device->negotiate_protocol_version();
    CHECK_EQ(agreed.status().error, Error::Refused);
    CHECK_EQ(h.simulator.received_commands.size(), 6u);  // 9 down to 4
}

TEST(a_refusal_other_than_fail_stops_the_negotiation_immediately) {
    Harness h;
    h.simulator.respond(code(Command::HostProtocolVersion), {kKeyNotSet});

    auto agreed = h.device->negotiate_protocol_version();
    CHECK_EQ(agreed.status().response, kKeyNotSet);
    CHECK_EQ(h.simulator.received_commands.size(), 1u);
}

TEST(connecting_synchronises_then_settles_the_version_then_reads_the_setup) {
    Harness h;
    h.fixed[code(Command::Sync)] = {kOk};
    h.fixed[code(Command::SetupRequest)] = guide_setup_reply();
    h.accept_versions_up_to(6);

    auto setup = h.device->connect();
    REQUIRE(setup.ok());

    const uint8_t version = code(Command::HostProtocolVersion);
    CHECK_EQ(h.commands_sent(), (V{code(Command::Sync), version, version, version, version, code(Command::SetupRequest)}));
    CHECK(setup->unit_type == UnitType::BanknoteValidator);
    REQUIRE(setup->channels.size() == 4);
    CHECK_EQ(setup->channels[3].value, 50u);
    CHECK_EQ(h.device->protocol_version(), 6);
    REQUIRE(h.device->setup().has_value());
    CHECK_EQ(h.device->setup()->firmware_version, std::string("0335"));
}

TEST(polling_reads_the_reply_at_the_negotiated_version) {
    Harness h;
    h.simulator.respond(code(Command::HostProtocolVersion), {kOk});
    h.simulator.respond(code(Command::Poll), bytes("F0 EE 04 EB"));

    auto at_four = h.device->poll();
    REQUIRE(at_four.ok());
    REQUIRE(at_four->events.size() == 2);
    CHECK(at_four->events[0].event() == Event::NoteCredit);
    CHECK(at_four->events[1].event() == Event::Stacked);

    REQUIRE(h.device->set_protocol_version(9).ok());

    auto at_nine = h.device->poll();
    REQUIRE(at_nine.ok());
    CHECK(!at_nine->complete());  // a version 9 credit wants seven data bytes and there are two
}

TEST(a_poll_carrying_an_unknown_event_keeps_the_events_before_it) {
    Harness h;
    h.simulator.respond(code(Command::Poll), bytes("F0 EE 02 7C 11"));

    auto result = h.device->poll();
    REQUIRE(result.ok());
    CHECK(!result->complete());
    CHECK_EQ(result->stopped_at_code, 0x7C);
    REQUIRE(result->events.size() == 1);
    CHECK(result->events[0].event() == Event::NoteCredit);
}

TEST(an_event_table_supplied_in_options_is_what_polls_are_read_with) {
    DeviceOptions options;
    options.event_table = EventTable::standard().with_event(0x7C, protocol_version::kLowest, EventPayload::fixed(1));
    Harness h(options);
    h.simulator.respond(code(Command::Poll), bytes("F0 7C 11"));

    auto result = h.device->poll();
    REQUIRE(result.ok());
    CHECK(result->complete());
    CHECK_EQ(result->events.size(), 1u);
}

TEST(a_refused_poll_is_an_error_not_an_empty_result) {
    Harness h;
    h.simulator.respond(code(Command::Poll), {kKeyNotSet});
    CHECK_EQ(h.device->poll().status().error, Error::Refused);
}

TEST(channel_inhibits_go_out_lowest_channel_first) {
    Harness h;
    h.simulator.respond(code(Command::SetChannelInhibits), {kOk});

    REQUIRE(h.device->set_channel_inhibits(uint16_t{0x0007}).ok());
    REQUIRE(h.device->set_channel_inhibits(uint16_t{0xFFFF}).ok());

    CHECK_EQ(h.simulator.received_commands[0], (V{code(Command::SetChannelInhibits), 0x07, 0x00}));
    CHECK_EQ(h.simulator.received_commands[1], (V{code(Command::SetChannelInhibits), 0xFF, 0xFF}));
    CHECK_EQ(h.device->set_channel_inhibits(ByteView()).error, Error::InvalidArgument);
}

TEST(the_serial_number_is_read_big_endian) {
    Harness h;
    h.simulator.respond(code(Command::GetSerialNumber), bytes("F0 00 1C 96 2C"));

    auto serial = h.device->serial_number();
    REQUIRE(serial.ok());
    CHECK_EQ(*serial, 1873452u);
}

TEST(versions_come_back_as_text) {
    Harness h;
    h.simulator.respond(code(Command::GetFirmwareVersion), bytes("F0 4E 56 30 32 30 30 34"));
    auto firmware = h.device->firmware_version();
    REQUIRE(firmware.ok());
    CHECK_EQ(*firmware, std::string("NV02004"));
}

TEST(sync_puts_the_sequence_flag_back_in_step_at_both_ends) {
    Harness h;
    h.simulator.respond(code(Command::Sync), {kOk});
    h.simulator.respond(code(Command::Poll), {kOk});

    REQUIRE(h.device->poll().ok());  // flag advances to true
    REQUIRE(h.device->sync().ok());  // sync itself, then reset
    REQUIRE(h.device->poll().ok());  // must start again from true

    CHECK_EQ(h.simulator.received_sequence_flags, (std::vector<bool>{true, true, true}));
}

TEST(a_command_this_library_does_not_name_can_still_be_sent) {
    Harness h;
    h.simulator.respond(0x7C, {kOk, 0xAB});

    auto reply = h.device->send(0x7C, V{0x01});
    REQUIRE(reply.ok());
    CHECK(reply->ok());
    CHECK_EQ(reply->data, V{0xAB});
    CHECK_EQ(h.simulator.received_commands[0], (V{0x7C, 0x01}));
}

TEST(asking_for_the_same_address_twice_gives_the_same_device) {
    Harness h;
    CHECK(h.bus.device(0x10) == h.bus.device(0x10));
    CHECK(h.bus.device(0x10) != h.bus.device(0x00));
    CHECK(h.bus.device(0x80) == nullptr);
}
