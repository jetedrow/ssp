// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include <chrono>
#include <thread>

#include "smileysecure/transport.hpp"

namespace smileysecure {
namespace {

class SteadyClock final : public Clock {
public:
    uint64_t now_ms() override {
        using namespace std::chrono;
        return static_cast<uint64_t>(duration_cast<milliseconds>(steady_clock::now().time_since_epoch()).count());
    }

    void sleep_ms(uint32_t duration_ms) override {
        std::this_thread::sleep_for(std::chrono::milliseconds(duration_ms));
    }
};

}  // namespace

Clock& Clock::system() {
    static SteadyClock clock;
    return clock;
}

}  // namespace smileysecure
