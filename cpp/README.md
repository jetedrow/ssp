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
| Procedural device API | `SspBus`, `SspDevice` | Done: `Bus`, `Device` |
| Event-driven poll host, with escrow hold and reject | `SspDeviceHost` | Done: `DeviceHost` |
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

MyUart uart;                      // 9600 baud, 8 data bits, 2 stop bits, no parity
Bus bus(uart);                    // one per wire; serializes every exchange
Device& validator = *bus.device(0x00);

Result<Setup> setup = validator.connect();   // sync, agree a protocol version, read the setup
if (!setup) {
    log(setup.status().describe());          // "no usable reply: ... (last attempt: timeout)"
    return;
}
validator.set_channel_inhibits(uint16_t{0xFFFF});
validator.enable();

DeviceHost host(validator);
host.on_event = [&](DeviceEvent& e) {
    if (e.note_in_escrow() && !want_this_note(e.event)) e.escrow = EscrowAction::Reject;
    if (e.event.event() == Event::NoteCredit) credit(setup->channels.at(e.event.data[0] - 1));
};
host.on_fault = [](const PollFault& fault) { log(fault.to_string()); };

host.start();   // or host.run() to block this task, or host.poll_once() from your own loop
```

A poll is what lets a validator stack the note in its escrow, so the host hands each event to
the handler between one poll and the next, and sends the hold or reject a handler asks for
before polling again. The loop never stops on an error: failed polls, failed escrow commands and
replies it could only partly read all go to `on_fault`.

`Device` also has the raw `send()`, so a command this library has no name for is still reachable,
and every other call the .NET `SspDevice` has: `sync`, `reset`, `disable`, `hold`,
`reject_banknote`, `serial_number`, `firmware_version`, `poll_with_ack` and so on.

## Design choices

**No exceptions.** ESP-IDF builds without C++ exceptions by default, so failures come back as a
`Status` or a `Result<T>` instead of being thrown. Each `Error` value matches one of the .NET
library's exception types. A `Status` carries a fixed message and costs no allocation; call
`describe()` for the full sentence. CI builds the library and its tests with `-fno-exceptions
-fno-rtti` to keep it that way.

**Blocking calls, one mutex.** The .NET library is async; this one blocks, which suits a FreeRTOS
task or an Android worker thread. A `Link` holds a mutex for the length of each exchange, so a
command from another task queues behind a poll rather than interleaving with it on the wire.
`DeviceHost` can run on its own `std::thread`, block a task you give it, or be stepped one poll at
a time from a loop you already have.

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
