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
| Framing: packet parse and serialize | Implemented, defects outstanding — see below |
| Framing: stream reader | Implemented, defects outstanding |
| Framing: stream writer | Not started |
| Framing: sequence flag ownership | Not started |
| Framing: timeouts and cancellation | Not started |
| Encryption (eSSP) | Not started |
| Command codec and version gate | Not started |
| Device facade, procedural | Not started |
| Device facade, event-driven | Not started |
| Firmware and dataset download (`0x74`) | Not started |

## Known defects in the existing framing layer

These predate the current work and are cleared in the transport correctness change.

1. Byte stuffing is removed twice — once in `SspStreamReader.ReadRawPacketAsync`, again in
   `SspRawPacket.Parse` — so a packet whose data contains `0x7F` throws when piped from one to the
   other. `SspRawPacket.GetPacketBytes` applies no stuffing at all on the way out.
2. `SspStreamReader.currentLength` is never reset on entry, so a second read on the same reader
   concatenates onto the previous packet.
3. The CRC mismatch message recomputes over the whole packet, including `STX` and the CRC bytes, and
   so reports a meaningless expected value.
4. `SspRawPacket.Parse` rejects packets of 259 bytes or more; a maximal packet is 260.
5. There is no timeout or cancellation anywhere; a silent device blocks the caller indefinitely.
6. The sequence flag is a caller-supplied parameter, so nothing implements toggle-on-success and
   repeat-on-retry.
7. `SspDeviceEmulator` connects a named pipe client with nothing calling `WaitForConnection`.
8. `SspStreamWriter` is a shell, and `SspPacket.cs` is entirely commented out.
9. `SspResponse` is missing `0xF9` `HEADER FAIL`, among others.
