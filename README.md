# SSP.net

A .NET library for talking to devices that speak SSP — Innovative Technology's Smiley Secure
Protocol — such as banknote validators, coin hoppers and payout units.

The library talks to a `System.IO.Stream` and nothing else, so the same code drives a device over a
serial port, over the network, over I2C or SPI, or over an in-memory stream in a test.

```csharp
using CCS.SspNet.Serial;

using var port = SspSerialPort.Open("COM3");
// port.Stream is an ordinary Stream, opened with SSP's wire settings.
```

## Status

Under active development. The framing layer exists; the device API, the event-driven surface and the
encrypted (eSSP) layer are being built. See [docs/protocol-support.md](docs/protocol-support.md) for
what is implemented and against which protocol version.

## Packages

| Package | What it is |
| --- | --- |
| `SSP.net` | The library. No transport dependencies. |
| `SSP.net.Serial` | Serial port transport, for devices on a COM port. |

Both target `netstandard2.0` and `net10.0`.

## Documentation

- [Getting started](docs/getting-started.md)
- [Architecture](docs/architecture.md)
- [Protocol support](docs/protocol-support.md)

## Building

Requires the .NET 10 SDK.

```bash
dotnet build src/CCS.SspNet.sln
dotnet test src/CCS.SspNet.sln
```

## Licence

LGPL-3.0. You can link this library from a closed-source application; changes to the
library itself stay under the LGPL. See [LICENSE](LICENSE), and
[LICENSE.GPL-3.0](LICENSE.GPL-3.0) for the GPL text the LGPL builds on.
