# Getting started

## Packages

| Package | What it is |
| --- | --- |
| `SSP.net` | The library. Talks to a `Stream`; no transport dependencies. |
| `SSP.net.Serial` | Serial transport. Only needed if the device is on a COM port. |

Both target `netstandard2.0` and `net10.0`, so they work on .NET Framework 4.6.2 and later as well
as on current .NET.

## Building the repository

Requires the .NET 10 SDK, as pinned in `global.json`.

```bash
dotnet build src/CCS.SspNet.sln
dotnet test src/CCS.SspNet.sln
```

To run the fast tier only, as pull request builds do:

```bash
dotnet test src/CCS.SspNet.sln --filter "Category!=Slow&Category!=Hardware"
```

Test tiers are declared in `TestCategories`. An untagged test counts as fast and runs on every
build, so only the slower tiers need tagging. Tests tagged `Hardware` need a real device and never
run in CI.

## Connecting to a device

The library takes a `Stream`, whatever the device is plugged into.

Over a serial port:

```csharp
using CCS.SspNet.Serial;

using var port = SspSerialPort.Open("COM3");
// port.Stream is an ordinary Stream carrying SSP's wire settings:
// 9600 baud, 8 data bits, no parity, 2 stop bits.
```

On a multi-drop bus, every device shares the port, so use the lowest baud rate common to all of
them:

```csharp
using var port = SspSerialPort.Open("/dev/ttyUSB0", new SspSerialPortOptions { BaudRate = 9600 });
```

Over the network, or anything else, a `Stream` is all that is required — `NetworkStream` works
directly, and an I2C or SPI transport needs only a `Stream` implementation. Tests use an in-memory
stream and need no hardware at all.

## Talking to a validator

```csharp
using CCS.SspNet;
using CCS.SspNet.Protocol;
using CCS.SspNet.Serial;

using var port = SspSerialPort.Open("COM3");
var validator = SspDevice.Attach(port.Stream);
using var bus = validator.Bus;

// Synchronise, agree the highest protocol version both ends can handle, and read the dataset.
var setup = await validator.ConnectAsync();

Console.WriteLine($"{setup.UnitType}, firmware {setup.FirmwareVersion}, protocol version {validator.ProtocolVersion}");
foreach (var channel in setup.Channels)
{
    Console.WriteLine($"  channel {channel.Number}: {channel.Value} {channel.CountryCode}");
}

// A validator accepts nothing until both of these have run.
await validator.SetChannelInhibitsAsync(ushort.MaxValue);   // all sixteen channels
await validator.EnableAsync();

while (true)
{
    var poll = await validator.PollAsync();

    foreach (var e in poll.Events)
    {
        if (e.Event == SspEvent.NoteCredit)
        {
            // A credit identifies the note by channel up to protocol version 8, and by country
            // code and value from version 9 — so read the payload, not just its first byte.
            if (e.Data.Length == 1)
            {
                var channel = setup.Channels[e.Data.Span[0] - 1];
                Console.WriteLine($"credit {channel.Value} {channel.CountryCode}");
            }
            else
            {
                Console.WriteLine($"credit {SspValues.ReadAmount(e.Data.Span.Slice(3))} " +
                                  $"{SspValues.ReadCountryCode(e.Data.Span)}");
            }
        }
    }

    if (!poll.IsComplete)
    {
        // The device sent an event this library has no payload length for, so the rest of the
        // reply could not be read. The events above it are still good.
        Console.WriteLine($"stopped at 0x{poll.StoppedAtCode:X2}: {poll.StopReason}");
    }

    await Task.Delay(200);
}
```

`ConnectAsync` deliberately stops short of enabling the device, so a host can look at what it has
connected to — the unit type, the firmware, the dataset — before letting it take money.

Polling is not read-only: a validator treats a poll as permission to accept a note sitting in
escrow. To decide first, call `HoldAsync` to keep the note in escrow for another interval, or
`RejectBanknoteAsync` to give it back.

## Letting events come to you

The loop above works, but most hosts would rather register a callback and get on with something
else. `SspDeviceHost` owns the poll loop and raises events as they arrive:

```csharp
using CCS.SspNet.Hosting;

using var host = new SspDeviceHost(validator);

host.EventReceived += (_, e) =>
{
    if (e.Event.Event == SspEvent.NoteCredit)
    {
        var channel = setup.Channels[e.Event.Data.Span[0] - 1];
        Console.WriteLine($"credit {channel.Value} {channel.CountryCode}");
    }
};

host.Fault += (_, e) => Console.WriteLine($"poll trouble: {e}");

host.Start();
// ... your application runs ...
await host.StopAsync();
```

The loop does not die on its own. A failed poll, a handler that throws, or a reply that stopped at
an event the library has no payload length for all raise `Fault` and polling continues.

### Deciding about a note before it is accepted

**A poll is not a passive read.** When the validator reports `Read` with a non-zero payload, a note
has been validated and is sitting in escrow — and the *next poll* takes it. You have one poll
interval to say otherwise, and doing nothing accepts the note.

From a callback, say so on the event:

```csharp
host.EventReceived += (_, e) =>
{
    if (e.IsNoteInEscrow && !TillHasRoom())
    {
        e.Escrow = SspEscrowAction.Reject;   // or SspEscrowAction.Hold to decide next interval
    }
};
```

The host sends that before the next poll goes out.

A callback cannot await anything, so if the decision needs a database or a person, use
`await foreach` instead. It polls inside the enumeration, so the body of the loop runs in the gap
between one poll and the next:

```csharp
await foreach (var e in host.ReadEventsAsync(cancellationToken))
{
    if (!e.IsNoteInEscrow) continue;

    if (await CustomerStillWantsToPay(cancellationToken))
    {
        continue;                                        // the next poll accepts it
    }

    await e.Device.RejectBanknoteAsync(cancellationToken);
}
```

Either way there is a hard limit: the device rejects an escrowed note by itself if no poll arrives
for ten seconds, so a slow handler costs you the note. `SspDeviceHostOptions.PollInterval` refuses
any value at or beyond that timeout for the same reason.

### Not losing a credit if your host crashes

`AcknowledgeEvents` makes the device repeat each event until the host acknowledges it, and the host
acknowledges only once every handler has returned:

```csharp
using var host = new SspDeviceHost(validator, new SspDeviceHostOptions
{
    AcknowledgeEvents = true,
});
```

A host that dies between reading a credit and recording it then sees that credit again on restart
rather than losing it. The cost is one extra command per poll.

## Encrypted devices

A device that pays money out will not do it in clear. It answers every command `KEY_NOT_SET` until
a key has been agreed, so on such a device this comes before anything else, `ConnectAsync`
included:

```csharp
var validator = SspDevice.Attach(port.Stream);

await validator.NegotiateKeysAsync();
await validator.ConnectAsync();
```

Nothing else changes: commands and replies look the same whether or not the link is encrypted.

The manufacturer's half of the key is the one thing you may have to supply. A device ships
expecting `01 23 45 67 01 23 45 67`, and a machine builder who has changed it has to say so:

```csharp
await validator.NegotiateKeysAsync(new SspEncryptionOptions
{
    FixedKey = 0x0123456789ABCDEF,
});
```

If every encrypted command times out from the very first one, read the byte-order note in
[protocol-support.md](protocol-support.md) before looking anywhere else — it is the one detail of
this layer the protocol documents do not settle, and it is a one-line change:

```csharp
await validator.NegotiateKeysAsync(new SspEncryptionOptions
{
    CountByteOrder = SspCountByteOrder.BigEndian,
});
```

Running `NegotiateKeysAsync` again at any time starts a fresh key and puts both packet counters
back to zero, which is the way back from a conversation that has lost its place.

## Several devices on one bus

SSP is multi-drop, so a validator and a hopper can share one port, each on its own address:

```csharp
using var bus = SspBus.Open(port.Stream);

var validator = bus.Device(0x00);
var hopper = bus.Device(0x10);
```

The bus serialises exchanges, so calls to the two devices from two threads queue behind each other
rather than interleaving on the wire, and each address keeps its own sequence flag.

## Commands this library has not modelled

Nothing here is a dead end. A command code with no name on it still goes out:

```csharp
var reply = await validator.SendAsync(0x7C, new byte[] { 0x01 });
```

and an event code with no name still arrives, with its raw value on `SspPollEvent.Code`. Registering
its payload length is described in [protocol-support.md](protocol-support.md).


## Updating firmware or a dataset

Replacing a device's firmware or note dataset is deliberately its own operation, not a method on a
device you might be polling — it overwrites the program the device runs, and ITL warn that getting
it wrong can damage a unit. Parse the file first, then hand it and the connection to the downloader:

```csharp
var file = await SspFirmwareFile.LoadAsync("EUR02604_NV02004141498000_IF_01.bv1");

using var port = SspSerialPort.Open("COM3");

await SspFirmwareDownloader.DownloadAsync(
    port.Stream,
    file,
    new SspDownloadOptions { BaudRateControl = port },
    new Progress<SspDownloadProgress>(p => Console.WriteLine($"{p.Stage}: {p.Fraction:P0}")));
```

`SspFirmwareFile.Load` checks the file is an ITL file and is well formed before anything is sent, so
a wrong or corrupt file fails in memory rather than part way through a device. If the file is simply
not meant for this device, the device itself says so when it sees the header, and the download stops
before overwriting anything.

Passing `BaudRateControl = port` lets the download raise the line speed for the transfer, which a
real serial device expects. Over a stream with no line speed — a network or in-memory stream — leave
it out and the transfer runs at the connection's existing speed.

The connection is the download's alone for its whole length: do not run a poll loop or send commands
on the same device while it is flashing.
