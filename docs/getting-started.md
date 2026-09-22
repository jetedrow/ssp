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
