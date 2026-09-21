# Protocol support

What the library implements today, and against which version of the protocol.

## Reference documents

| Document | What it covers | Version in use |
| --- | --- | --- |
| ITL GA138, SSP Communications Protocol Manual | The protocol itself: commands, responses, poll events, the encryption layer | Issue 19, protocol version 6 |
| ITL GA973, SSP Implementation Guide | Worked integration guidance | v2.2 |
| ITL, Multi-Address SSP Downloading (2015) | Firmware and dataset download over a shared bus, command `0x74` | 2015 |

Innovative Technology's download centre is the authoritative source. If a newer GA138 is obtained,
add it here and record what changed.

## Protocol versions

The negotiated protocol version is set with `HostProtocolVersion` (`0x06`) at connect and is carried
on the connection. Commands declare the minimum version they require.

| Version | Added |
| --- | --- |
| 1 | Initial gaming protocol |
| 2 | Generic commands, coin readers and hoppers, device addressing, encrypted packets |
| 3 | `SHOW_RESET_EVENTS`, note-cleared-at-reset events |
| 4 | Barcode ticket commands, revised reject reason codes |
| 6 | Multi-currency, payout by denomination, 4-byte channel values, lid and calibration events |

Versions 7 and 8 exist on newer hardware. They are not yet modelled; adding one is a table entry per
command and per response parser, not a structural change.

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
| Command codec and version gate | Not started |
| Device facade, procedural | Not started |
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
