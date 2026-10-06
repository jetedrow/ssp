// SPDX-License-Identifier: LGPL-3.0-or-later
#include <atomic>
#include <stdexcept>
#include <chrono>
#include <thread>

#include "smileysecure/host.hpp"
#include "support.hpp"
#include "test.hpp"

using namespace smileysecure;
using sstest::bytes;
using V = std::vector<uint8_t>;

namespace {

constexpr uint8_t kOk = 0xF0;

uint8_t code(Command command) { return static_cast<uint8_t>(command); }

const V kNoteInEscrow = bytes("F0 EF 03");
const V kNoteBeingScanned = bytes("F0 EF 00");

struct Harness {
    explicit Harness(HostOptions host_options = {}, DeviceOptions options = {})
        : simulator(clock), bus(simulator, std::move(options), clock), host(*bus.device(0x00), host_options) {}

    V commands_sent() const {
        V sent;
        for (const auto& command : simulator.received_commands) {
            if (!command.empty()) sent.push_back(command[0]);
        }
        return sent;
    }

    sstest::FakeClock clock;
    sstest::DeviceSimulator simulator;
    Bus bus;
    DeviceHost host;
};

}  // namespace

TEST(events_from_the_device_reach_the_handler_in_order) {
    Harness h;
    h.simulator.respond(code(Command::Poll), bytes("F0 EE 04 EB"));

    std::vector<Event> seen;
    h.host.on_event = [&](DeviceEvent& e) { seen.push_back(e.event.event()); };

    h.host.poll_once();

    CHECK(seen == (std::vector<Event>{Event::NoteCredit, Event::Stacked}));
}

TEST(a_handler_that_does_nothing_lets_the_next_poll_take_the_note) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);
    h.host.on_event = [](DeviceEvent&) {};

    h.host.poll_once();
    h.host.poll_once();

    CHECK_EQ(h.commands_sent(), (V{code(Command::Poll), code(Command::Poll)}));
}

TEST(a_rejecting_handler_gets_its_reject_out_before_the_next_poll) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);
    h.simulator.respond(code(Command::RejectBanknote), {kOk});
    h.host.on_event = [](DeviceEvent& e) {
        if (e.note_in_escrow()) e.escrow = EscrowAction::Reject;
    };

    h.host.poll_once();
    h.host.poll_once();

    CHECK_EQ(h.commands_sent(),
             (V{code(Command::Poll), code(Command::RejectBanknote), code(Command::Poll), code(Command::RejectBanknote)}));
}

TEST(a_holding_handler_gets_its_hold_out_before_the_next_poll) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);
    h.simulator.respond(code(Command::Hold), {kOk});
    h.host.on_event = [](DeviceEvent& e) {
        if (e.note_in_escrow()) e.escrow = EscrowAction::Hold;
    };

    h.host.poll_once();

    CHECK_EQ(h.commands_sent(), (V{code(Command::Poll), code(Command::Hold)}));
}

TEST(a_note_still_being_scanned_is_not_an_escrow_decision) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteBeingScanned);

    int escrows = 0;
    h.host.on_event = [&](DeviceEvent& e) {
        if (e.note_in_escrow()) ++escrows;
        e.escrow = EscrowAction::Reject;  // ignored: there is nothing in escrow to reject
    };

    h.host.poll_once();

    CHECK_EQ(escrows, 0);
    CHECK_EQ(h.commands_sent(), V{code(Command::Poll)});
}

TEST(a_handler_may_call_the_device_directly_between_polls) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);
    h.simulator.respond(code(Command::RejectBanknote), {kOk});
    h.host.on_event = [](DeviceEvent& e) {
        if (e.note_in_escrow()) e.device.reject_banknote();
    };

    h.host.poll_once();

    CHECK_EQ(h.commands_sent(), (V{code(Command::Poll), code(Command::RejectBanknote)}));
}

TEST(a_poll_that_fails_is_reported_and_the_next_one_recovers) {
    DeviceOptions options;
    options.link.max_retries = 0;
    Harness h({}, options);
    h.simulator.respond(code(Command::Poll), bytes("F0 EB"));
    h.simulator.drop_next_replies = 1;

    std::vector<Error> faults;
    int events = 0;
    h.host.on_fault = [&](const PollFault& f) { faults.push_back(f.status.error); };
    h.host.on_event = [&](DeviceEvent&) { ++events; };

    h.host.poll_once();
    h.host.poll_once();

    CHECK(faults == std::vector<Error>{Error::NoUsableReply});
    CHECK_EQ(events, 1);
}

TEST(a_failed_escrow_command_is_reported) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);
    h.simulator.respond(code(Command::Hold), {0xF5});
    h.host.on_event = [](DeviceEvent& e) { e.escrow = EscrowAction::Hold; };

    std::vector<uint8_t> refusals;
    h.host.on_fault = [&](const PollFault& f) { refusals.push_back(f.status.response); };

    h.host.poll_once();

    CHECK_EQ(refusals, V{0xF5});
}

TEST(a_reply_that_stops_part_way_raises_what_it_read_and_reports_the_gap) {
    Harness h;
    h.simulator.respond(code(Command::Poll), bytes("F0 EB 7C 11"));

    std::vector<Event> events;
    std::vector<uint8_t> stops;
    h.host.on_event = [&](DeviceEvent& e) { events.push_back(e.event.event()); };
    h.host.on_fault = [&](const PollFault& f) {
        if (f.result != nullptr) stops.push_back(f.result->stopped_at_code);
        CHECK(f.to_string().find("0x7C") != std::string::npos);
    };

    h.host.poll_once();

    CHECK(events == std::vector<Event>{Event::Stacked});
    CHECK_EQ(stops, V{0x7C});
}

TEST(acknowledging_events_polls_with_ack_and_acknowledges_after_the_handlers) {
    HostOptions options;
    options.acknowledge_events = true;
    Harness h(options);
    h.simulator.respond(code(Command::PollWithAck), bytes("F0 EE 01"));
    h.simulator.respond(code(Command::EventAck), {kOk});

    size_t sent_when_handled = 0;
    h.host.on_event = [&](DeviceEvent&) { sent_when_handled = h.simulator.received_commands.size(); };

    h.host.poll_once();

    CHECK_EQ(h.commands_sent(), (V{code(Command::PollWithAck), code(Command::EventAck)}));
    CHECK_EQ(sent_when_handled, 1u);
}

TEST(an_empty_poll_with_ack_is_not_acknowledged) {
    HostOptions options;
    options.acknowledge_events = true;
    Harness h(options);
    h.simulator.respond(code(Command::PollWithAck), {kOk});

    h.host.poll_once();

    CHECK_EQ(h.commands_sent(), V{code(Command::PollWithAck)});
}

#if defined(__cpp_exceptions)
TEST(a_handler_that_throws_is_reported_and_the_note_is_left_alone) {
    Harness h;
    h.simulator.respond(code(Command::Poll), kNoteInEscrow);

    int faults = 0;
    h.host.on_fault = [&](const PollFault&) { ++faults; };
    h.host.on_event = [](DeviceEvent& e) {
        e.escrow = EscrowAction::Reject;
        throw std::runtime_error("handler blew up");
    };

    h.host.poll_once();
    h.host.poll_once();

    CHECK_EQ(faults, 2);
    CHECK_EQ(h.commands_sent(), (V{code(Command::Poll), code(Command::Poll)}));
}
#endif

TEST(a_poll_interval_at_or_beyond_the_escrow_timeout_is_rejected) {
    HostOptions options;
    options.poll_interval_ms = HostOptions::kEscrowTimeoutMs;
    Harness h(options);
    CHECK_EQ(h.host.start().error, Error::InvalidArgument);
    CHECK_EQ(h.host.run().error, Error::InvalidArgument);
    CHECK(!h.host.running());
}

TEST(the_threaded_loop_polls_until_stopped_and_rejects_a_second_start) {
    HostOptions options;
    options.poll_interval_ms = 1;
    Harness h(options);
    h.simulator.respond(code(Command::Poll), bytes("F0 EB"));

    std::atomic<int> events{0};
    h.host.on_event = [&](DeviceEvent&) { ++events; };

    REQUIRE(h.host.start().ok());
    CHECK_EQ(h.host.start().error, Error::InvalidState);

    const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
    while (events.load() < 3 && std::chrono::steady_clock::now() < deadline) {
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
    h.host.stop();

    CHECK(events.load() >= 3);
    CHECK(!h.host.running());

    const size_t after_stop = h.simulator.received_commands.size();
    std::this_thread::sleep_for(std::chrono::milliseconds(20));
    CHECK_EQ(h.simulator.received_commands.size(), after_stop);

    // And it can be started again.
    REQUIRE(h.host.start().ok());
    h.host.stop();
}

TEST(run_blocks_until_a_handler_stops_it) {
    HostOptions options;
    options.poll_interval_ms = 1;
    Harness h(options);
    h.simulator.respond(code(Command::Poll), bytes("F0 EB"));

    int events = 0;
    h.host.on_event = [&](DeviceEvent&) {
        if (++events == 3) h.host.stop();
    };

    CHECK(h.host.run().ok());
    CHECK_EQ(events, 3);
    CHECK(!h.host.running());
}
