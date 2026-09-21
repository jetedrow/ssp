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

The device API that consumes this stream is still being built; see
[protocol-support.md](protocol-support.md) for what exists today.
