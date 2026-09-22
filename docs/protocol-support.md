# Protocol support

What the library implements today, and against which version of the protocol.

## Reference documents

| Document | What it covers | Version in use |
| --- | --- | --- |
| ITL GA138, SSP Communications Protocol Manual | The protocol itself: commands, responses, poll events, the encryption layer | `GA138_2_2_649A`, issue 2.2, protocol versions 4 to 9 |
| ITL GA973, SSP Implementation Guide | Worked integration guidance | v2.2 |
| ITL, Multi-Address SSP Downloading (2015) | Firmware and dataset download over a shared bus, command `0x74` | 2015 |

Innovative Technology's download centre is the authoritative source. These documents are ITL's
property and are not in this repository; if a newer GA138 is obtained, add it to this table and
record what changed.

## Protocol versions

The protocol version gates **events, not commands**. A poll reply is a run of event codes, each
followed by however many data bytes that event carries, and nothing on the wire says how many — the
host is expected to know in advance. A device sending an event the host had never heard of would
leave the host unable to find where the next event began, so the rest of the reply would be read as
nonsense. Raising the version is a host saying "I know the events you are about to send me", and
the manual is explicit that a host must never set a device higher than it can itself decode.

Which *commands* a device accepts is not a function of the version at all; it is a per-device
command table, which is why no command in the manual carries a minimum version.

The version is read with `SetupRequest` (`0x05`) at connect and set with `HostProtocolVersion`
(`0x06`).

`SspEventTable` holds the consequences. What each version adds, counted from the manual's own event
tables:

| Version | Events introduced | Events whose payload changed |
| --- | --- | --- |
| 4 | 28, the banknote validator set | — |
| 5 | 16: coin credit, cashbox, note float, emptying | — |
| 6 | 23: ticket printer, coin mechanism, cashbox service state | 14 payout and float events gain a per-currency block |
| 7 | 7: calibration, jam recovery, value added, error during payout | — |
| 8 | 3: note held in bezel, notes moved at reset | — |
| 9 | — | `Read` and `NoteCredit` grow from a channel to a country code and value |

Version 6 is the significant one: it is where payout amounts stop being a single figure and become a
count followed by one block per currency in the dataset, so the same event is a different length on
a one-currency device than on a three-currency one.

Versions 1 to 3 predate this issue of the manual. Their events are a subset of version 4's, so
decoding such a device at version 4 is safe.

### Going past version 9

`SspEventTable` is data rather than a switch, and it is immutable, so a device newer than this
library can be supported without waiting for a release:

```csharp
var table = SspEventTable.Default
    .WithEvent(0x7C, firstVersion: 10, SspEventPayload.Fixed(4));

var events = SspPollDecoder.Decode(reply.Data.Span, version: 10, table);
```

The same applies to commands: `SspMessage.Create(byte command, params byte[] parameters)` sends a
code this library has no name for.

### Events with no documented size

The manual names five events without giving a payload size anywhere, and prints no worked packet to
read one off: `CoinsLow` (`0xD3`), `MaintenanceRequired` (`0xC0`), `CoinRejected` (`0xBA`),
`TicketInBezelAtStartup` (`0xA7`) and `EscrowActive` (`0x8B`).

Guessing zero for these would be a coin flip that reads the rest of the reply out of step whenever
it lost, so they are declared unknown. A decode that reaches one stops, returns the events it read
before it, and reports the code it stopped at — `SspPollResult.IsComplete` is how a caller tells.
A host that knows the real size registers it with `WithEvent`.

Two events go the other way. `SafeJam` (`0xEA`) and `CashboxTamper` (`0x91`) are in no event table
in this issue of the manual, but earlier issues define them and devices in the field send them, so
both are in `SspEventTable.Default` with a zero-length payload.

### Errors in the manual's worked examples

The manual prints 72 poll replies as worked examples. All 72 carry a CRC that checks out, and 63 of
them decode. The other nine are wrong, in three ways, and each one's length byte and CRC were
computed over its own wrong bytes — so they cannot be told apart from correct packets by checking,
only by decoding them:

- **Six drop the leading event code.** The `Dispensing`, `Floating`, `Incomplete Payout` and
  `Coin Credit` examples print only the payload, so `7F 80 05 F0 94 11 00 00 E8 F3` under
  `Floating` is a four-byte amount with no `0xD7` in front of it. Each decodes once its own code is
  prepended, which is how they were identified.
- **Two drop a country code** out of the middle of a payload: the `SmartEmptying` and
  `ErrorDuringPayout` examples give a count byte and an amount, then no three-letter code.
- **One prints the wrong code.** The Poll command's own headline example,
  `7F 80 03 F0 F1 F8 DC 0C`, is captioned "device reset and disabled" and uses `0xF8` — the FAIL
  response code — where the `Disabled` event `0xE8` belongs. `0xF8` appears in no event table in the
  manual.

The 63 that are correct are the corpus in `ManualPollExampleTests`. The nine are excluded rather
than silently corrected, so the corpus stays a record of what the manual actually says.

## Implementation status

| Layer | Status |
| --- | --- |
| Framing: CRC-16 | Implemented |
| Framing: packet parse and serialize | Implemented |
| Framing: stream reader | Implemented |
| Framing: stream writer | Implemented |
| Framing: byte stuffing, both directions | Implemented |
| Framing: sequence flag ownership | Implemented |
| Framing: timeouts, cancellation and retry | Implemented |
| Test transport: in-memory stream and device simulator | Implemented |
| Encryption (eSSP) | Not started |
| Command codec: message building, reply parsing | Implemented |
| Command codec: event table and version gate | Implemented |
| Command codec: poll event decoding | Implemented |
| Device facade, procedural | Implemented |
| Device facade, setup request parsing | Implemented for banknote validators |
| Device facade, protocol version negotiation | Implemented |
| Device facade, event-driven | Not started |
| Firmware and dataset download (`0x74`) | Not started |

## Framing defects, and how each was closed

All nine predated this work and are now fixed. The fixes are covered by tests, and the coverage was
checked rather than assumed: swapping the old parser back in fails six of the new framing tests,
including the stuffing round trip, the CRC message and both length bounds. Defects 6 through 8 had
no code to test against at all — the link layer and the writer did not exist.

1. **Byte stuffing was applied twice and never on the way out.** It now lives in exactly one place,
   `SspByteStuffing`, applied by `SspStreamWriter` and removed by `SspStreamReader`. `SspRawPacket`
   deals only in logical packets and no longer unstuffs. A payload containing `0x7F` round-trips.
2. **The reader could only be used once.** All of its read state is local to the call, so one
   instance reads any number of consecutive packets.
3. **The CRC mismatch message reported a meaningless expected value.** It computed over the whole
   packet, CRC bytes included; `CalculatePacketCrcFor` is now the single definition of what a
   packet's CRC should be, and both validation and the error message use it.
4. **Packets of 259 and 260 bytes were rejected.** The bounds are now `Constants.MinPacketLength`
   and `Constants.MaxPacketLength`, and a maximum-sized packet parses.
5. **There was no timeout or cancellation.** Every read and write takes a `CancellationToken`, and
   `SspLink` applies a per-exchange timeout.
6. **The sequence flag was unowned.** `SspLink` holds it per device address, advances it for each
   new command, and repeats it on a retry — which is what lets a device recognise a retransmission
   and answer from its cache rather than acting twice.
7. **The named-pipe emulator would have deadlocked.** Replaced by `SspLoopbackStream`, a duplex
   in-memory stream, and `SspDeviceSimulator`, which speaks the real wire format and can drop or
   corrupt replies on demand.
8. **`SspStreamWriter` was a shell and `SspPacket.cs` was commented out.** The writer is
   implemented; the dead file is gone.
9. **`SspResponse` was missing `0xF9` HEADER FAIL.** Added, with the meaning given in ITL's
   multi-address download note. Any further gaps wait on the full GA138, since guessing at response
   codes is worse than leaving them out.
