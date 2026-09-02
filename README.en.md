<h1 align="center">📡 Snet.Examples</h1>

<p align="center">
  <img width="120" height="120" src="https://api.snet.cn/pic/nuget.png" alt="Snet Logo"/>
</p>

<p align="center">
  <b>Official examples for the Snet.cn industrial IoT communication library</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-blue?logo=dotnet"/>
  <img src="https://img.shields.io/badge/license-MIT-green"/>
</p>

<p align="center">
  <a href="https://snet.cn"><b>🌐 Website</b></a> ·
  <a href="https://www.nuget.org/profiles/Shun"><b>📦 NuGet</b></a> ·
  <a href="https://github.com/shunnet/Daq"><b>🔌 Daq</b></a> ·
  <a href="https://github.com/shunnet/Debug"><b>🔧 Debug</b></a>
</p>

<p align="center">
  English | 📖 <a href="README.md"><b>简体中文</b></a>
</p>

## ✨ What is this repository

**Snet.Examples** is the **official examples repository** for the [Snet.cn](https://snet.cn) industrial IoT communication library. Snet is a .NET communication stack built for industrial field applications, published on [NuGet](https://www.nuget.org/profiles/Shun) as `Snet.*` packages. It covers data acquisition (DAQ), message buses (MQ), low-level communication, and device-level transport (TEP).

This repository does not implement the library. Instead, it turns the **most common and most typical usages** into runnable samples and console self-checks so you can get started with Snet quickly. Whether you are talking to a PLC, reading OPC UA, forwarding to a message queue, or building a data-acquisition Web API, you will find reference code here.

> Note: this is a *samples* repository. For the full capability set and the complete API, refer to the official [Snet.cn](https://snet.cn) documentation. The NuGet version used by an example is controlled by its `.csproj` references; this README is not updated item by item as the code grows.

## 📋 Requirements

| Component | Requirement |
|-----------|-------------|
| 🔧 **.NET SDK** | .NET 10.0 (all sample projects use `TargetFramework=net10.0`) |
| 🛠️ **IDE** | Visual Studio 2022+ (recommended), or the `dotnet` CLI |
| 🖥️ **Platform** | Console/service samples run on Windows, Linux and macOS |

## 🚀 Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/shunnet/Examples.git
cd Examples

# 2. Open the solution (open src\src.slnx with Visual Studio 2022+),
#    or build from the command line
cd src
dotnet build src.slnx
```

The solution is organized into independently runnable projects (see “[Project Structure](#-project-structure)”). Before starting a sample, make sure its host services are ready — for example, the DAQ sample launches local OPC UA and MQTT servers.

> Tip: the `DeleteBinAndObj.bat` at the repository root cleans up the `bin` / `obj` output of every project, ready for a rebuild.

## 🗂️ Project Structure

The solution `src\src.slnx` is grouped by **capability**; each group is independent and can be run on its own:

| Solution Folder | Project | Demonstrates |
|-----------------|---------|--------------|
| `core` | `Snet.Core.Samples` | Core layer: communication, caching, channel, handlers, address packing (Packer), and self-checks |
| `daq` | `Snet.DAQ.Samples` / `Snet.DAQ.Samples.DLL` | Data acquisition: OPC UA / MQTT servers & clients, reflection parsing, MQ forwarding |
| `relay` | `Snet.Relay.Samples` | Message bus: MQTT client & server, plus Kafka / RabbitMQ / RocketMQ / NetMQ / Netty |
| `tep` | `Snet.Tep.Client.Samples` / `Snet.Tep.Service.Samples` | Device-level transport (TEP): slave (device) and master (service / Web API) |
| `web` | `Snet.Service` / `Snet.Api` | Service layer: DI registration & registries; `Snet.Api` is an ASP.NET Core REST API |

> Samples grow alongside the library. Only the **stable groups** are described here — new samples drop into the matching folder without changing this structure.

## 📟 The Samples

### `core` — Snet.Core.Samples

Demonstrates Snet's core infrastructure. By default the entry point runs a **full address packing (Packer) verification** that checks address parsing, byte lengths, bit/word region mapping, overflow protection and performance across many protocol drivers. It also includes self-check suites for communication and infrastructure:

- **Communication**: TCP, UDP (unicast / broadcast / multicast), WebSocket, HTTP, serial
- **Caching**: `ProcessCache`, `ShareCache`
- **Channel** `Channel`, **Handler** (address value handling), **BytesTransform** (byte parsing & conversion)
- **Reflection** `Reflection`: load a DLL dynamically and invoke its methods
- **Address Packing** `Packer`: multi-protocol address parsing & batch packing

### `daq` — Snet.DAQ.Samples

Demonstrates a typical **data-acquisition** loop: start a local OPC UA server and two MQTT servers, read an address list from `config/addressList.json`, attach **reflection parsing** (custom data conversion via the `Snet.DAQ.Samples.DLL` class library) and **MQ forwarding** (publish acquired values to MQTT topics), then connect an OPC UA client to subscribe and read.

- `Snet.DAQ.Samples.DLL`: the reflection sample's class library; `Class1` exposes `R1/R2/R3` as custom parsing entry points.

### `relay` — Snet.Relay.Samples

Demonstrates **message-bus** usage: start an MQTT client and server, subscribe to a topic, produce/consume data, and switch the received-content parsing mode (`Content` / `ContentWithTopic`, etc.). The project also references Kafka, RabbitMQ, RocketMQ, NetMQ and Netty packages, making it a starting point for multi-broker integration.

### `tep` — TEP (device-level transport)

**TEP** is Snet's transport protocol for devices and edge gateways.

- `Snet.Tep.Client.Samples` (**slave / device side**): connect to a master, set the device name and ID, register “state” and “write” callbacks, upload point data, and include a mixed-type upload stress test (reporting sends-per-second and byte counts).
- `Snet.Tep.Service.Samples` (**master / service side**): host a Web API, define a device and its points, and demonstrate open / close / write / read / status / subscribe / unsubscribe / get-args.

### `web` — Snet.Service and Snet.Api

- `Snet.Service`: a **dependency-injection-friendly service layer**. The `AddDaqAsync` / `AddMqAsync` extensions register data-acquisition devices and message brokers into `IServiceCollection` in one go, then expose them through `IDaqRegistry` / `IMqRegistry` indexed by “SN” for easy lookup from controllers.
- `Snet.Api`: an `ASP.NET Core` Web API showing how to register devices via `Snet.Service` (built-in Sim and Modbus simulated devices) and expose REST endpoints (e.g. `/api/idaq/{sn}/read`, `/write`, `/on`, `/off`), with Swagger enabled in development.

## 🧩 Core Concepts at a Glance

A few concepts let you read every sample quickly:

| Concept | Description |
|---------|-------------|
| **OperateResult** | The unified operation result carrying `Status`, `Message` and `ResultData`; extract content with `GetDetails` / `GetSource` |
| **IDaq / IMq** | The uniform abstraction for acquisition devices and message brokers, exposing On/Off/Read/Write/Status |
| **Address / AddressDetails** | A unified address model describing name, data type, length, encoding, etc. `Address` is the collection |
| **Packer** | Batch-"packs" many addresses by driver semantics, handling byte length, bit/word regions, overflow splitting and cross-region downgrade |
| **Reflection parsing / MQ forwarding** | `AddressParseParam` calls a dynamic library via reflection for data conversion; `AddressMqParam` publishes results to a message bus |
| **Event-driven** | Subscribe to data and info via `OnDataEvent` / `OnInfoEvent` to receive results in real time |
| **Builder / Registry** | `Snet.Service` uses a Builder to register and a Registry indexed by SN, for centralized device management in a service |

The protocol drivers (PLC, OPC UA/DA, databases, MQTT/Kafka, etc.) ship as separate `Snet.*` packages that samples reference as needed. For the full parameters and integration guidance, see the official documentation.

## 📚 Resources & Community

| Channel | Link |
|---------|------|
| 🌐 **Website** | [snet.cn](https://snet.cn) |
| 📦 **NuGet** | [Snet (author Shun)](https://www.nuget.org/profiles/Shun) |
| 🔌 **Daq** | [github.com/shunnet/Daq](https://github.com/shunnet/Daq) — ready-to-use data collection & forwarding tool |
| 🔧 **Debug** | [github.com/shunnet/Debug](https://github.com/shunnet/Debug) — multi-protocol debugging & diagnostics tool |

## 💬 Community

- Join the **Snet QQ technical community** — [Click to join](https://qm.qq.com/q/gPjrD9wGty) to exchange experience and share feedback.
