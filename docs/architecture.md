# Architecture

SSP.net is built in four layers. Each one depends only on the layer below it, and each is testable
on its own without a device attached.

```
L3  Device facade     typed commands, events          SspDevice, SspBanknoteValidator, SspHopper
L2  Command codec     encode / decode, version gate
L1  Encryption        eSSP, transparent to L2
L0  Framing           Stream in/out, byte stuffing, CRC, sequence flag, retry, timeout
```

## L0 — framing

Owns the wire format: the `STX` marker, byte stuffing, the length byte, CRC-16, and the sequence
flag. It reads and writes a `System.IO.Stream` and knows nothing about what the bytes mean.

Two rules keep this layer honest:

- **Byte stuffing lives here and nowhere else**, on both the read and the write path. A layer above
  must never see, or have to produce, a doubled `0x7F`.
- **The sequence flag is owned here.** SSP's retransmission scheme depends on the host toggling the
  flag after each successful exchange and repeating it on a retry. That state belongs to whatever
  owns the link, not to the caller.

## L1 — encryption

The eSSP layer: Diffie-Hellman key negotiation, AES-128, the packet counter, and the `STEX`
envelope. It sits between framing and the codec so that encryption is invisible from above —
a command looks identical whether or not the link is encrypted.

## L2 — command codec

Turns typed requests into payload bytes and payload bytes back into typed responses. This is the
layer that makes the library survive protocol changes:

- The protocol version is **negotiated at connect** and carried on the connection, not compiled in.
- Each command declares the **minimum protocol version** it needs, so an unsupported command fails
  with a message naming the version it wanted rather than a bare `COMMAND_NOT_KNOWN` from the device.
- Response parsers are selected by **(device type, protocol version)**. `SetupRequest` is the
  motivating case: its payload differs per device, and channel values widened from two bytes to four
  at protocol version 6. Supporting a new version is a new entry in a table.
- An unrecognised poll event **degrades to `UnknownPollEvent`** rather than throwing. Newer firmware
  must never break an older copy of this library.
- A raw escape hatch stays public, so a device capability the library has not modelled yet does not
  block anyone.

## L3 — device facade

The API consumers actually use, in two shapes over one connection:

- **Procedural** — `await device.EnableAsync(ct)`, `await device.PollAsync(ct)`.
- **Event-driven** — an `SspDeviceHost` owning a poll loop that raises events as they arrive, plus
  an `IAsyncEnumerable` stream for `await foreach`.

Both go through a single serializer on the connection. That is what lets a procedural call be made
from inside an event handler without corrupting the sequence flag: the call is queued and
interleaved between polls rather than racing the poll loop.

## Transport

The library talks to a `Stream`. That is the whole transport contract.

| Transport | How |
| --- | --- |
| Serial | `SspSerialPort.Open("COM3")` in the `SSP.net.Serial` package |
| Network | `NetworkStream`, directly |
| I2C / SPI / anything else | Implement a `Stream` |
| Tests | An in-memory duplex stream — no hardware, no pipes, no timing races |

A raw `Stream` carries no notion of baud rate, DTR or the inter-byte gaps a multi-drop SSP bus cares
about. Where a transport needs those, an optional capability interface covers it while plain
`Stream` keeps the simple case simple.
