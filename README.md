# SmileySecure.Net

A .NET library for talking to devices that speak SSP — Innovative Technology's Smiley Secure
Protocol — such as banknote validators, coin hoppers and payout units.

The library talks to a `System.IO.Stream` and nothing else, so the same code drives a device over a
serial port, over the network, over I2C or SPI, or over an in-memory stream in a test.

```csharp
using SmileySecure.Net.Serial;

using var port = SspSerialPort.Open("COM3");
// port.Stream is an ordinary Stream, opened with SSP's wire settings.
```

## Status

Feature complete for banknote validators: the framing layer, the command codec, the procedural
device API, the event-driven poll loop, the encrypted (eSSP) layer and firmware/dataset download
are all in. See [docs/protocol-support.md](docs/protocol-support.md) for
what is implemented and against which protocol version.

## Packages

| Package | What it is |
| --- | --- |
| `SmileySecure.Net` | The library. No transport dependencies. |
| `SmileySecure.Net.Serial` | Serial port transport, for devices on a COM port. |

The library was started as SSP.net, but the `SSP.` package ID prefix is reserved on nuget.org, so the
repository, packages, assemblies and namespaces are all named `SmileySecure.Net`.

Both target `netstandard2.0` and `net10.0`. Pre-releases are on nuget.org:

```bash
dotnet add package SmileySecure.Net --prerelease
dotnet add package SmileySecure.Net.Serial --prerelease
```

## C++ (ESP32 and Android)

A C++17 port for microcontrollers and Android lives in [`cpp/`](cpp/README.md). It has no
operating system dependencies and is being ported from this library layer by layer; its README
says which layers are in so far.

## Documentation

- [Getting started](docs/getting-started.md)
- [Architecture](docs/architecture.md)
- [Protocol support](docs/protocol-support.md)
- [Releasing](docs/releasing.md)

## Building

Requires the .NET 10 SDK.

```bash
dotnet build src/SmileySecure.Net.sln
dotnet test src/SmileySecure.Net.sln
```

## Licence

LGPL-3.0. You can link this library from a closed-source application; changes to the
library itself stay under the LGPL. See [LICENSE](LICENSE), and
[LICENSE.GPL-3.0](LICENSE.GPL-3.0) for the GPL text the LGPL builds on.
