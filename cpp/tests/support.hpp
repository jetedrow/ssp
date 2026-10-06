// Fakes for driving the library without hardware: a clock that only moves when told to, an
// in-memory stream, and a device simulator that answers commands synchronously as they are
// written.
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <deque>
#include <functional>
#include <map>
#include <vector>

#include "smileysecure/packet.hpp"
#include "smileysecure/transport.hpp"

namespace sstest {

/// A clock whose time moves only when something waits on it.
class FakeClock final : public smileysecure::Clock {
public:
    uint64_t now_ms() override { return now; }
    void sleep_ms(uint32_t duration_ms) override { now += duration_ms; }

    uint64_t now = 1000;
};

/// A byte queue.  Reading an empty one waits out the timeout on the fake clock and returns
/// nothing, as a silent line would; or reports the stream closed once `close()` is called.
class MemoryStream : public smileysecure::Stream {
public:
    explicit MemoryStream(FakeClock* clock = nullptr) : clock_(clock) {}

    bool write(const uint8_t* data, size_t size) override {
        written.insert(written.end(), data, data + size);
        on_write(data, size);
        return true;
    }

    int read(uint8_t* data, size_t size, uint32_t timeout_ms) override {
        if (incoming.empty()) {
            if (closed) return -1;
            if (clock_ != nullptr) clock_->sleep_ms(timeout_ms);
            return 0;
        }
        size_t count = 0;
        while (count < size && !incoming.empty()) {
            data[count++] = incoming.front();
            incoming.pop_front();
        }
        return static_cast<int>(count);
    }

    void feed(const std::vector<uint8_t>& bytes) { incoming.insert(incoming.end(), bytes.begin(), bytes.end()); }
    void close() { closed = true; }

    std::deque<uint8_t> incoming;
    std::vector<uint8_t> written;
    bool closed = false;

protected:
    virtual void on_write(const uint8_t*, size_t) {}

private:
    FakeClock* clock_;
};

/// A device on the far end of a MemoryStream.  Every packet the host writes is answered at once
/// by queueing the reply for the host to read, so a whole exchange runs on one thread.
class DeviceSimulator final : public MemoryStream {
public:
    explicit DeviceSimulator(FakeClock& clock, uint8_t address = 0) : MemoryStream(&clock), address_(address) {}

    /// Answer `command` with `reply` (the reply's data field, response code first).
    void respond(uint8_t command, std::vector<uint8_t> reply) { replies_[command] = std::move(reply); }

    /// Called for each command instead of the fixed replies, when set.
    std::function<std::vector<uint8_t>(const std::vector<uint8_t>& command)> handler;

    int drop_next_replies = 0;
    int corrupt_next_replies = 0;
    std::vector<std::vector<uint8_t>> received_commands;
    std::vector<bool> received_sequence_flags;

protected:
    void on_write(const uint8_t* data, size_t size) override {
        MemoryStream wire;
        wire.feed(std::vector<uint8_t>(data, data + size));
        wire.close();

        FakeClock unused;
        smileysecure::Frame frame;
        if (!smileysecure::read_packet(wire, unused, UINT64_MAX, frame)) return;

        auto packet = smileysecure::Packet::parse(frame.view());
        if (!packet || packet->address != address_) return;

        const bool flag = (frame.bytes[1] & smileysecure::kSequenceFlagMask) != 0;
        received_commands.push_back(packet->data);
        received_sequence_flags.push_back(flag);

        if (drop_next_replies > 0) {
            --drop_next_replies;
            return;
        }

        std::vector<uint8_t> reply_data;
        if (handler) {
            reply_data = handler(packet->data);
        } else {
            auto found = replies_.find(packet->data.empty() ? 0 : packet->data[0]);
            reply_data = found != replies_.end() ? found->second : std::vector<uint8_t>{0xF2};
        }

        smileysecure::Packet reply{address_, reply_data};
        auto logical = reply.encode(flag);
        if (!logical) return;

        if (corrupt_next_replies > 0) {
            --corrupt_next_replies;
            logical->back() ^= 0xFF;
        }

        MemoryStream out;
        smileysecure::write_packet(out, *logical);
        feed(out.written);
    }

private:
    uint8_t address_;
    std::map<uint8_t, std::vector<uint8_t>> replies_;
};

}  // namespace sstest
