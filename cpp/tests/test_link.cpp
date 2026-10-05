// SPDX-License-Identifier: LGPL-3.0-or-later
#include <thread>

#include "smileysecure/link.hpp"
#include "support.hpp"
#include "test.hpp"

using namespace smileysecure;
using V = std::vector<uint8_t>;

namespace {

constexpr uint8_t kSync = 0x11;
constexpr uint8_t kPoll = 0x07;
constexpr uint8_t kOk = 0xF0;

struct Harness {
    explicit Harness(LinkOptions options = {}) : device(clock), link(device, options, clock) {}

    sstest::FakeClock clock;
    sstest::DeviceSimulator device;
    Link link;
};

}  // namespace

TEST(a_command_gets_its_reply) {
    Harness h;
    h.device.respond(kSync, {kOk});

    auto reply = h.link.exchange(0x00, V{kSync});
    REQUIRE(reply.ok());
    CHECK_EQ(reply->data, std::vector<uint8_t>{kOk});
    CHECK_EQ(reply->address, 0);
}

TEST(the_sequence_flag_alternates_between_commands) {
    Harness h;
    h.device.respond(kPoll, {kOk});

    for (int i = 0; i < 3; ++i) REQUIRE(h.link.exchange(0x00, V{kPoll}).ok());

    CHECK_EQ(h.device.received_sequence_flags, (std::vector<bool>{true, false, true}));
}

TEST(a_retry_repeats_the_sequence_flag_rather_than_advancing_it) {
    Harness h;
    h.device.respond(kSync, {kOk});
    h.device.drop_next_replies = 1;

    auto reply = h.link.exchange(0x00, V{kSync});
    REQUIRE(reply.ok());
    CHECK_EQ(h.device.received_commands.size(), 2u);
    CHECK_EQ(h.device.received_sequence_flags, (std::vector<bool>{true, true}));
}

TEST(a_corrupted_reply_is_retried) {
    Harness h;
    h.device.respond(kSync, {kOk});
    h.device.corrupt_next_replies = 1;

    REQUIRE(h.link.exchange(0x00, V{kSync}).ok());
    CHECK_EQ(h.device.received_commands.size(), 2u);
}

TEST(a_silent_device_fails_once_the_retries_are_spent) {
    LinkOptions options;
    options.max_retries = 1;
    options.response_timeout_ms = 100;
    Harness h(options);
    h.device.respond(kSync, {kOk});
    h.device.drop_next_replies = 5;

    const uint64_t started = h.clock.now;
    auto reply = h.link.exchange(0x00, V{kSync});

    CHECK_EQ(reply.status().error, Error::NoUsableReply);
    CHECK_EQ(reply.status().cause, Error::Timeout);
    CHECK_EQ(h.device.received_commands.size(), 2u);
    CHECK_EQ(h.clock.now - started, 200u);
}

TEST(a_closed_stream_is_not_retried) {
    Harness h;
    h.device.respond(kSync, {kOk});
    h.device.drop_next_replies = 5;
    h.device.close();

    CHECK_EQ(h.link.exchange(0x00, V{kSync}).status().error, Error::ConnectionClosed);
    CHECK_EQ(h.device.received_commands.size(), 1u);
}

TEST(reset_sequence_starts_the_flag_over_as_sync_does) {
    Harness h;
    h.device.respond(kPoll, {kOk});

    REQUIRE(h.link.exchange(0x00, V{kPoll}).ok());
    h.link.reset_sequence(0x00);
    REQUIRE(h.link.exchange(0x00, V{kPoll}).ok());

    CHECK_EQ(h.device.received_sequence_flags, (std::vector<bool>{true, true}));
}

TEST(each_address_keeps_its_own_sequence_flag) {
    sstest::FakeClock clock;
    sstest::DeviceSimulator device(clock, 0x10);
    Link link(device, {}, clock);
    device.respond(kPoll, {kOk});

    REQUIRE(link.exchange(0x10, V{kPoll}).ok());
    REQUIRE(link.exchange(0x10, V{kPoll}).ok());

    CHECK_EQ(device.received_sequence_flags, (std::vector<bool>{true, false}));
}

TEST(a_reply_from_the_wrong_address_is_not_accepted) {
    LinkOptions options;
    options.max_retries = 0;
    sstest::FakeClock clock;
    sstest::DeviceSimulator device(clock, 0x00);
    Link link(device, options, clock);
    device.respond(kSync, {kOk});

    // Nothing answers at 0x10, and the device at 0x00 ignores it.
    CHECK_EQ(link.exchange(0x10, V{kSync}).status().error, Error::NoUsableReply);
}

TEST(a_payload_containing_stx_reaches_the_device_intact) {
    Harness h;
    const std::vector<uint8_t> payload{0x0B, kStx, kStx, 0x01};
    h.device.respond(0x0B, {kOk, kStx, 0x02});

    auto reply = h.link.exchange(0x00, payload);
    REQUIRE(reply.ok());
    CHECK_EQ(h.device.received_commands[0], payload);
    CHECK_EQ(reply->data, (std::vector<uint8_t>{kOk, kStx, 0x02}));
}

TEST(an_address_wider_than_seven_bits_is_refused) {
    Harness h;
    CHECK_EQ(h.link.exchange(0x80, V{kSync}).status().error, Error::InvalidArgument);
}

TEST(concurrent_callers_are_serialized) {
    Harness h;
    h.device.respond(kPoll, {kOk});

    std::vector<std::thread> callers;
    int failures = 0;
    std::mutex counted;
    for (int i = 0; i < 8; ++i) {
        callers.emplace_back([&] {
            if (!h.link.exchange(0x00, V{kPoll}).ok()) {
                std::lock_guard<std::mutex> lock(counted);
                ++failures;
            }
        });
    }
    for (auto& caller : callers) caller.join();

    CHECK_EQ(failures, 0);
    CHECK_EQ(h.device.received_commands.size(), 8u);
    CHECK_EQ(h.device.received_sequence_flags,
             (std::vector<bool>{true, false, true, false, true, false, true, false}));
}
