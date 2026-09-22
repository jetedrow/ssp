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

The envelope is `SspEncryptedEnvelope`; the key and counter for one device's conversation are an
`SspEncryptionSession`, held by `SspLink` per address, so a bus can carry an encrypted device and a
plain one at once. Wrapping happens once per exchange rather than once per attempt: a
retransmission has to be the bytes the device already half-heard, counter included, or it reads as
a new packet out of sequence.

What the protocol documents leave open about this layer, and how each is handled, is in
`docs/protocol-support.md`.

## L2 — command codec

Turns typed requests into payload bytes and payload bytes back into typed responses. This is the
layer that makes the library survive protocol changes.

`SspMessage` builds a command's data field — the code, then its parameters. `SspReply` splits a
device's answer into a response code and the rest. Neither knows about framing, addressing or the
CRC, which all belong to L0.

Reading a poll reply is the part that needs help. A reply is a run of event codes, each followed by
however many data bytes that event carries, and **nothing on the wire says how many**. So:

- **The protocol version is a required argument, not a default.** It gates events, not commands —
  see `docs/protocol-support.md`. The same eight bytes are one credit at version 9 and seven events
  at version 4, and a host reading at the wrong version does not get an error, it gets a wrong
  answer. Making the version impossible to forget is the only defence.
- **The payload lengths live in `SspEventTable`, which is data and is immutable.** `WithEvent` returns
  a new table, so a device newer than this library is supported by adding a row rather than waiting
  for a release. Supporting a new protocol version is table entries, not a structural change.
- **An event the table has no length for stops the decode rather than guessing.** There is no way to
  tell where an unknown event's payload ends and the next code begins, so a guess would report
  events that never happened — including credits. `SspPollResult` carries the events read before
  that point, the code it stopped at, and the undecoded bytes; `IsComplete` is how a caller tells.
  Newer firmware degrades to a partial read, never to an exception or to fiction.
- **A raw escape hatch stays public.** `SspMessage.Create(byte command, ...)` sends a command code
  this library has no name for, and an event code it has no name for still arrives with its raw
  value, so an unmodelled device capability does not block anyone.

The lengths in `SspEventTable.Default` are checked against the 63 poll replies printed in the
protocol manual that are internally consistent, in `ManualPollExampleTests`.

## L3 — device facade

The API consumers actually use, in two shapes over one connection:

- **Procedural** — `await device.EnableAsync(ct)`, `await device.PollAsync(ct)`.
- **Event-driven** — an `SspDeviceHost` owning a poll loop that raises events as they arrive, plus
  an `IAsyncEnumerable` for `await foreach`.

`SspBus` owns the stream and the devices on it; `SspDevice` is one address on that bus. Both go
through a single serializer on the connection. That is what lets a procedural call be made from
inside an event handler without corrupting the sequence flag: the call is queued and interleaved
between polls rather than racing the poll loop.

**A poll is not a passive read.** When a validator reports `Read` with a non-zero payload, a note
has been validated and is held in escrow — and the *next poll* takes it. A host has one poll
interval to say otherwise, and doing nothing accepts. The device also rejects an escrowed note by
itself if no poll arrives for ten seconds, so a host cannot hold a note by stalling.

That single fact shapes the whole event-driven surface:

- `SspDeviceEventArgs.Escrow` is how a synchronous handler says `Hold` or `Reject`, and the host
  sends it before the next poll goes out.
- `ReadEventsAsync` polls *inside* the enumeration rather than on a background thread, so the body
  of an `await foreach` runs in the gap between polls. That is what lets a decision be awaited —
  a database lookup, a user prompt — and still beat the poll that would have taken the note.
- `SspDeviceHostOptions.PollInterval` refuses any value at or beyond the ten second timeout, since
  such a loop would guarantee that every held note is rejected.

The loop never dies on its own. A failed poll, a handler that throws, or a reply that stops at an
unknown event all raise `Fault` and polling continues; a cable that comes loose should be visible
and survivable, not terminal.

Two things about `SspDevice` are worth stating, because they are where a host most easily goes
wrong:

- **`ConnectAsync` settles the protocol version before reading the setup, and does not enable the
  device.** The version has to come first because it changes how the setup reply is laid out — a
  validator's channel data moved into an expanded segment at version 6. Stopping short of enabling
  is deliberate: a host gets to see what it has connected to before letting it take money.
- **Negotiation walks down, never up past the host's own ceiling.** The protocol offers no way to
  ask a device which versions it supports, only to ask it to change — OK if it can, FAIL if it
  cannot. So `NegotiateProtocolVersionAsync` starts at `SspDeviceOptions.HighestProtocolVersion`
  and steps down. A device left running ahead of its host sends events the host cannot measure,
  which is exactly what the version mechanism exists to prevent.

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
