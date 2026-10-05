# SmileySecure for C++

A portable C++17 port of the SmileySecure.Net library, for talking SSP (Innovative Technology's
Smiley Secure Protocol) from an ESP32, from Android, or from any other C++ host.

The core has no operating system dependencies. It talks to a `smileysecure::Stream` — a
three-method byte transport — and measures time against a `smileysecure::Clock`, so the same code
drives a validator over an ESP32 UART, a USB serial adapter on Android, or an in-memory pipe in a
test.

## Status

This is being ported layer by layer from the .NET library, one pull request at a time.

| Layer | .NET | C++ |
| --- | --- | --- |
| Framing: CRC-16, byte stuffing, sequence flag, retries, timeouts | `SspLink` and friends | Done: `Link`, `Packet`, `read_packet`, `write_packet` |
| Command codec: messages, replies, values, event table, poll decoder, setup reply | `SspMessage`, `SspReply`, `SspPollDecoder`, `SspSetup` | Done |
| Procedural device API | `SspBus`, `SspDevice` | Next |
| Event-driven poll host, with escrow hold and reject | `SspDeviceHost` | Planned |
| eSSP encryption | `SspEncryptionSession` | Planned |
| Firmware and dataset download | `SspFirmwareDownloader` | Planned |
| ESP-IDF component (also usable from Arduino-ESP32) with a UART stream | — | Planned |
| Android NDK/JNI wrapper and Gradle AAR | — | Planned |

The protocol knowledge — payload lengths, byte orders, the manual's misprints — is the .NET
library's, carried over unchanged. See [docs/protocol-support.md](../docs/protocol-support.md).

## Using it

```cpp
#include <smileysecure/smileysecure.hpp>

using namespace smileysecure;

class MyUart : public Stream {
public:
    bool write(const uint8_t* data, size_t size) override;              // queue bytes for sending
    int read(uint8_t* data, size_t size, uint32_t timeout_ms) override; // 0 on timeout, <0 if closed
};

MyUart uart;
Link link(uart);  // one per bus; serializes every exchange

auto poll = make_message(Command::Poll);
auto packet = link.exchange(0x00, *poll);
if (!packet) {
    log(packet.status().describe());  // "no usable reply: ... (last attempt: timeout)"
    return;
}

auto reply = Reply::parse(packet->data);
if (reply && reply->ok()) {
    PollResult events = decode_poll(reply->data, /* protocol version */ 7);
    for (const PollEvent& event : events.events) {
        if (event.event() == Event::NoteCredit) { /* ... */ }
    }
}
```

The device API that comes next wraps the message-and-reply step into calls like
`device.poll()` and `device.connect()`.

## Design choices

**No exceptions.** ESP-IDF builds without C++ exceptions by default, so failures come back as a
`Status` or a `Result<T>` instead of being thrown. Each `Error` value matches one of the .NET
library's exception types. A `Status` carries a fixed message and costs no allocation; call
`describe()` for the full sentence. CI builds the library and its tests with `-fno-exceptions
-fno-rtti` to keep it that way.

**Blocking calls, one mutex.** The .NET library is async; this one blocks, which suits a FreeRTOS
task or an Android worker thread. A `Link` holds a mutex for the length of each exchange, so a
command from another task queues behind a poll rather than interleaving with it on the wire.

**No allocation on the wire path.** Reading and writing a packet use fixed buffers sized to the
protocol's 260-byte maximum. Payloads above that layer are `std::vector`, which is fine on an ESP32
and keeps the API simple.

**C++17.** Every ESP-IDF 5 and NDK toolchain in use supports it. `ByteView` stands in for
`std::span`, and `Result<T>` for `std::expected`.

## Building and testing

Requires CMake 3.16 and a C++17 compiler.

```bash
cmake -S cpp -B cpp/build
cmake --build cpp/build
ctest --test-dir cpp/build --output-on-failure
```

The tests are ported from the .NET suite, including all 63 of the manual's valid worked poll
replies, and run against a device simulator, so no hardware is needed. They use a small built-in
harness rather than a framework, so nothing is downloaded at build time.

To use the library from another CMake project:

```cmake
add_subdirectory(path/to/SmileySecure.Net/cpp)
target_link_libraries(my_app PRIVATE SmileySecure::smileysecure)
```

## Licence

LGPL-3.0, the same as the .NET library.
