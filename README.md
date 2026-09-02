<h1 align="center">📡 Snet.Examples</h1>

<p align="center">
  <img width="120" height="120" src="https://api.snet.cn/pic/nuget.png" alt="Snet Logo"/>
</p>

<p align="center">
  <b>工业物联网通信库 Snet.cn 的官方示例库</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-blue?logo=dotnet"/>
  <img src="https://img.shields.io/badge/license-MIT-green"/>
</p>

<p align="center">
  <a href="https://snet.cn"><b>🌐 官方网站</b></a> ·
  <a href="https://www.nuget.org/profiles/Shun"><b>📦 NuGet</b></a> ·
  <a href="https://github.com/shunnet/Daq"><b>🔌 采数工具 Daq</b></a> ·
  <a href="https://github.com/shunnet/Debug"><b>🔧 调试工具 Debug</b></a>
</p>

<p align="center">
  📖 <a href="README.en.md"><b>English</b></a> | 简体中文
</p>

## ✨ 这是什么仓库

**Snet.Examples** 是 [Snet.cn](https://snet.cn) 工业物联网通信库的**官方示例仓库**。Snet 是一套面向工业现场的 .NET 通信库，以 `Snet.*` 形式发布在 [NuGet](https://www.nuget.org/profiles/Shun)，覆盖采集（DAQ）、消息总线（MQ）、底层通信、以及设备级传输（TEP）等场景。

本仓库不实现库本身，而是把**最常用、最典型的用法**做成一个个可直接运行的示例与控制台自测程序，帮助你在自己的项目中快速上手 Snet。无论你是对接 PLC、读写 OPC UA、转发消息队列，还是要搭建一个数据采集的 Web API，都能在这里找到对应的参考代码。

> 说明：本仓库是“示例集合”，核心能力与完整 API 请以 [Snet.cn](https://snet.cn) 官方文档为准。示例中的 NuGet 版本由各 `.csproj` 引用决定，README 不跟随代码逐条更新。

## 📋 环境要求

| 组件 | 要求 |
|------|------|
| 🔧 **.NET SDK** | .NET 10.0（所有示例项目 `TargetFramework=net10.0`） |
| 🛠️ **IDE** | Visual Studio 2022+（推荐），或 `dotnet` 命令行 |
| 🖥️ **平台** | 控制台/服务示例均可在 Windows / Linux / macOS 运行 |

## 🚀 快速开始

```bash
# 1. 克隆仓库
git clone https://github.com/shunnet/Examples.git
cd Examples

# 2. 打开解决方案（使用 Visual Studio 2022+ 打开 src\src.slnx）
#    或在命令行构建
cd src
dotnet build src.slnx
```

解决方案按目录组织为多个可独立运行的项目（见下文“[项目结构](#-项目结构)”）。启动某个示例前，先确认其宿主服务是否就绪（例如 DAQ 示例会本地启动 OPC UA / MQTT 服务端）。

> 提示：仓库根目录提供 `DeleteBinAndObj.bat`，可一键清理所有项目的 `bin` / `obj` 产物，便于重新构建。

## 🗂️ 项目结构

解决方案 `src\src.slnx` 按**能力分组**，各分组彼此独立、可单独运行：

| 解决方案目录 | 项目 | 演示内容 |
|-------------|------|----------|
| `core` | `Snet.Core.Samples` | 核心层：通信、缓存、通道、Handler、地址组包（Packer）等的验证与自测 |
| `daq` | `Snet.DAQ.Samples` / `Snet.DAQ.Samples.DLL` | 数据采集：OPC UA / MQTT 服务端与客户端、反射解析、MQ 转发 |
| `relay` | `Snet.Relay.Samples` | 消息总线：MQTT 客户端与服务端，以及 Kafka / RabbitMQ / RocketMQ / NetMQ / Netty |
| `tep` | `Snet.Tep.Client.Samples` / `Snet.Tep.Service.Samples` | 设备级传输（TEP）：从站（设备端）与主站（服务端 / Web API） |
| `web` | `Snet.Service` / `Snet.Api` | 服务层：DI 注册与注册表；`Snet.Api` 为 ASP.NET Core REST API |

> 示例随库的演进自然增多，这里只描述**稳定的分组**；新增示例会落入对应目录，不影响本 README 结构。

## 📟 各示例说明

### `core` — Snet.Core.Samples

演示 Snet 的核心基础设施，入口程序默认运行一套**地址组包（Packer）全量验证**（校验多协议驱动的地址解析、字节长度、位/字区域映射、超限防护与性能），并附带通信与基础设施的自测示例：

- **通信**：TCP、UDP（单播 / 广播 / 组播）、WebSocket、HTTP、串口
- **缓存**：进程缓 `ProcessCache`、共享缓存 `ShareCache`
- **通道** `Channel`、**Handler**（地址值处理）、**BytesTransform**（字节解析转换）
- **反射** `Reflection`：动态加载 DLL 并调用其方法
- **地址组包** `Packer`：多协议地址解析与批量组包

### `daq` — Snet.DAQ.Samples

演示**数据采集**的典型闭环：本地启动 OPC UA 服务端与两个 MQTT 服务端，从 `config/addressList.json` 读取地址清单，为每个地址配置**反射解析**（通过动态库 `Snet.DAQ.Samples.DLL` 自定义数据转换）与 **MQ 转发**（把采集结果发布到 MQTT 主题），再以 OPC UA 客户端连接订阅、读取。

- `Snet.DAQ.Samples.DLL`：反射示例用的动态类库，`Class1` 提供 `R1/R2/R3` 三个方法作为自定义解析入口。

### `relay` — Snet.Relay.Samples

演示**消息总线**用法：启动一个 MQTT 客户端与服务端，订阅主题、生产/消费数据，并展示如何切换接收解析方式（`Content` / `ContentWithTopic` 等）。工程同时引用 Kafka、RabbitMQ、RocketMQ、NetMQ、Netty 等包，可作为多中间件接入的起点。

### `tep` — TEP（设备级传输）

**TEP** 是 Snet 面向设备/边缘网关的传输协议。

- `Snet.Tep.Client.Samples`（**从站 / 设备端**）：连接主站、设置设备名称与 ID、注册“状态”与“写入”回调，上传点位数据，并附带多数据类型混合上传的压力测试（统计每秒发送次数与字节量）。
- `Snet.Tep.Service.Samples`（**主站 / 服务端**）：启动 Web API、定义设备与点位，演示打开 / 关闭 / 写入 / 读取 / 状态 / 订阅 / 取消订阅 / 取参数。

### `web` — Snet.Service 与 Snet.Api

- `Snet.Service`：一个**面向依赖注入的服务层**。通过 `AddDaqAsync` / `AddMqAsync` 扩展一次性把采集设备与消息中间件注册进 `IServiceCollection`，并以按“SN”索引的 `IDaqRegistry` / `IMqRegistry` 暴露，方便控制器按需取用。
- `Snet.Api`：`ASP.NET Core` Web API，演示如何用 `Snet.Service` 注册设备（内置 Sim 与 Modbus 模拟设备）并对外暴露 REST 接口（如 `/api/idaq/{sn}/read`、`/write`、`/on`、`/off` 等），开发环境自带 Swagger 文档。

## 🧩 核心概念速览

理解这几个概念，就能快速读懂所有示例：

| 概念 | 说明 |
|------|------|
| **OperateResult** | 统一的操作返回结果，携带 `Status`、`Message`、`ResultData`，用 `GetDetails` / `GetSource` 取内容 |
| **IDaq / IMq** | 采集设备与消息中间件的统一抽象，均提供 On/Off/Read/Write/Status 等接口 |
| **Address / AddressDetails** | 统一地址模型，描述地址名、数据类型、长度、编码等；`Address` 为地址集合 |
| **Packer（地址组包）** | 按驱动语义把多地址批量“打包”，自动处理字节长度、位/字区域、超限拆分与跨区降级 |
| **反射解析 / MQ 转发** | `AddressParseParam` 通过反射调用动态库做数据转换；`AddressMqParam` 把结果发布到消息总线 |
| **事件驱动** | 通过 `OnDataEvent` / `OnInfoEvent` 订阅数据与信息事件，实时接收结果 |
| **Builder / Registry** | `Snet.Service` 用 Builder 注册、Registry 按 SN 索引，便于在服务里集中管理设备 |

各协议驱动（PLC、OPC UA/DA、数据库、MQTT/Kafka 等）以独立 `Snet.*` 包提供，示例按需引用；完整参数与接入方法见官方文档。

## 📚 资源与社区

| 渠道 | 链接 |
|------|------|
| 🌐 **官方网站** | [snet.cn](https://snet.cn) |
| 📦 **NuGet** | [Snet（作者 Shun）](https://www.nuget.org/profiles/Shun) |
| 🔌 **数采工具 Daq** | [github.com/shunnet/Daq](https://github.com/shunnet/Daq) — 开箱即用的数据采集转发工具 |
| 🔧 **调试工具 Debug** | [github.com/shunnet/Debug](https://github.com/shunnet/Debug) — 多协议调试与诊断工具 |

## 💬 社区

- 欢迎加入 **Snet 技术交流 QQ 群** —— [点击加群](https://qm.qq.com/q/gPjrD9wGty)，交流使用体验、反馈建议。
