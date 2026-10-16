using System.Collections.Concurrent;
using System.Text;
using Snet.Core.handler;
using Snet.Core.packer;
using Snet.Core.@abstract;
using Snet.Driver.Core.Device;
using Snet.Driver.Profinet.AllenBradley;
using Snet.Driver.Profinet.Beckhoff;
using Snet.Driver.Profinet.Cimon;
using Snet.Driver.Profinet.FATEK;
using Snet.Driver.Profinet.GE;
using Snet.Driver.Profinet.LSIS;
using Snet.Driver.Profinet.Melsec.Helper;
using Snet.Driver.Profinet.Omron;
using Snet.Driver.Profinet.Panasonic;
using Snet.Driver.Profinet.Toyota;
using Snet.Driver.Profinet.Vigor;
using Snet.Driver.Profinet.XINJE;
using Snet.Driver.Profinet.Yokogawa;
using Snet.Driver.Profinet.YASKAWA;
using Snet.Driver.ModBus;
using Snet.Driver.Core.Address;
using Snet.Driver.Profinet.Fuji;
using Snet.Driver.Profinet.Melsec;
using Snet.Driver.Profinet.Siemens;
using Snet.Model.data;
using Snet.Model.@enum;

namespace Snet.Core.Samples;

/// <summary>锁定 2026-10-16 审查发现；期望值来自 Driver 原生读取或独立的边界契约，不按模型自行生成期望。</summary>
internal static class PackerRepairRegression
{
    /// <summary>执行同步、异步解包及所有已确认的协议修复回归，失败交由主入口累计。</summary>
    /// <param name="assert">主入口提供的断言计数回调。</param>
    internal static void Run(Action<bool, string> assert)
    {
        VerifyAllFamilies(assert);
        VerifyNativeText(assert);
        VerifyFramedBits(assert);
        VerifyCollectionUnpack(assert);
        VerifyAdditionalDriverContracts(assert);
        var handler = new BytesHandler();
        foreach (var (type, raw) in new (DataType, object)[]
        {
            (DataType.Bool, true), (DataType.ShortArray, new short[] { 3, 4 }),
            (DataType.ByteArray, new byte[] { 5, 6 })
        })
        {
            var point = new AddressDetails("100", type, 2) { AddressExtendParam = new object() };
            var packed = Pack("Modbus", point).Single();
            assert(ReferenceEquals(point, packed), $"F01 {type} 降级完整保留原对象及扩展参数");
            var value = new AddressValue(QualityType.Normal, raw, raw, "成功", DateTime.Now, packed);
            foreach (var result in new[] { handler.Transform(value), handler.TransformAsync(value).GetAwaiter().GetResult() })
                assert(result.Status && result.ResultData is ConcurrentDictionary<string, AddressValue> dict
                    && dict.Count == 1 && ReferenceEquals(dict.Values.Single(), value), $"F01 {type} 类型值同步/异步透传不丢失");
        }

        var invalid = new List<BytesModel>
        {
            new("bad", "", -1, 1, DataType.Byte), new("overflow", "", int.MaxValue, 2, DataType.Short),
            new("ok", "", 0, 1, DataType.Byte, boolIndex: 3)
        };
        foreach (var result in new[] { handler.Transform(new byte[] { 0xA5 }, DateTime.Now, invalid),
            handler.TransformAsync(new byte[] { 0xA5 }, DateTime.Now, invalid).GetAwaiter().GetResult() })
            assert(!result.Status && result.ResultData is ConcurrentDictionary<string, AddressValue> dict && dict.Count == 3
                && Equals(dict["ok"].ResultValue, (byte)0xA5) && dict["bad"].Quality == QualityType.Exception,
                "F11/F16 非法模型隔离、保留后续正常点，Byte 忽略位索引，同步/异步一致");

        using var s7 = new SiemensS7Server();
        s7.Write("M100", Encoding.Unicode.GetBytes("ABCD"));
        VerifyText(assert, "Siemens", s7.ReadString("M100", 4, Encoding.Unicode).Content,
            new("M100", DataType.String, 4, EncodingType.Unicode), (a, n) => s7.Read(a, n).Content);
        using var mc = new MelsecMcServer();
        mc.Write("D100", Encoding.ASCII.GetBytes("ABCDEFGH"));
        VerifyText(assert, "Mitsubishi", mc.ReadString("D100", 4, Encoding.ASCII).Content,
            new("D100", DataType.String, 4, EncodingType.ASCII), (a, n) => mc.Read(a, n).Content);
        foreach (var (vendor, point) in new[]
        {
            ("AllenBradley", new AddressDetails("ST1:0", DataType.String, 4)),
            ("Siemens", new AddressDetails("M100", DataType.String, 0)),
            ("GE", new AddressDetails("R1", DataType.Byte))
        })
            assert(ReferenceEquals(Pack(vendor, point).Single(), point), $"F02/F10 {vendor} 专用或不可表达读取保持单点契约");

        using var spb = new FujiSPBServer();
        spb.Write("M100", new[] { true });
        var fuji = Pack("FujiSPB", new AddressDetails("M100", DataType.Bool)).Single();
        if (fuji.AddressExtendParam is List<BytesModel> fm)
            assert(Equals(Values(handler.Transform(spb.Read(fuji.AddressName, fuji.Length).Content, DateTime.Now, fm)).Single().ResultValue, true),
                "F03 SPB 绝对位号字读锚点返回真值");
        else assert(false, "F03 SPB 合法位点仍应支持正确组包");
        var ambiguous = Pack("FujiSPB", new("M1.100", DataType.Short), new("M1.101", DataType.Short));
        assert(ambiguous.Count == 1 && ambiguous[0].AddressExtendParam is List<BytesModel> am && am.All(m => m.StartBit == 0),
            "F04 SPB 点号尾部不被误当 SPH 字地址");
        foreach (var (vendor, name) in new[] { ("FujiSPHNet", "D100"), ("FujiCommandSettingType", "M1.100") })
            assert(Pack(vendor, new AddressDetails(name, DataType.Short)).All(a => !IsBatch(a)), $"F04 {vendor} 不合并其他协议格式");

        foreach (string vendor in new[] { "Yokogawa", "MitsubishiFx" })
            assert(Pack(vendor, new("M100", DataType.Bool), new("M1200", DataType.Bool)).Count == 2,
                $"F05 {vendor} 位批不依赖错误的跨帧推进");
        assert(Pack("XinJE", new("D7999", DataType.Short), new("D8000", DataType.Short)).Count == 2,
            "F06 XC D8000 地址映射断点隔离");
        foreach (string vendor in new[] { "LSCpu", "LSis_Cpu", "SiemensPPI", "SiemensPPIOverTcp", "InovanceEasyNet" })
            assert(!PackerHandler.CanAutoPack(vendor), $"F07/F12/F13 {vendor} 不宣称支持不兼容的组包");
        assert(Pack("InovanceComputerLink", new("D100", DataType.Short), new("D101", DataType.Short)).Count == 1,
            "F13 ComputerLink 使用实际 FX Links 字地址模型");
        foreach (string name in new[] { "i=100", "ig=0xF005;100", "ig=0xF080;100" })
            assert(Pack("Beckhoff", new AddressDetails(name, DataType.Byte)).All(a => !IsBatch(a)), $"F08 ADS 非线性访问 {name} 不合批");
        foreach (var (vendor, prefix) in new[] { ("Cimon", "D"), ("Beckhoff", "M") })
        {
            var sparse = Pack(vendor, new(prefix + "0", DataType.Short), new(prefix + int.MaxValue, DataType.Short));
            assert(sparse.Count == 2 && sparse.Where(IsBatch).All(a => ((List<BytesModel>)a.AddressExtendParam!).All(m => (long)m.StartBit + m.Length <= a.Length)),
                $"F09 {vendor} 稀疏极值不产生溢出模型");
        }
        foreach (var (vendor, names) in new[]
        {
            ("Omron", new[] { "D65536", "CIO65536.0", "EM0.65536" }),
            ("Fanuc", new[] { "D65536", "AI65536", "AQ65536", "GI65536" })
        })
        foreach (string name in names)
            assert(Pack(vendor, new AddressDetails(name, DataType.Short)).All(a => !IsBatch(a)), $"F14 {vendor} {name} UInt16 越界降级");
        foreach (var (vendor, name, valid) in new (string, string, Func<string, bool>)[]
        {
            ("Mitsubishi", "dy10", a => McAddressData.ParseMelsecFrom(a, 1, true).IsSuccess),
            ("Omron", "cIo100.0", a => OmronFinsAddress.ParseFrom(a).IsSuccess),
            ("LSis_Cnet", "mw100", a => LsisCnetAddress.ParseFrom(a, 1, false).IsSuccess),
            ("AllenBradley", "n7:100", a => AllenBradleySLCAddress.ParseFrom(a).IsSuccess)
        })
            assert(valid(Pack(vendor, new AddressDetails(name, vendor is "Mitsubishi" or "Omron" ? DataType.Bool : DataType.Short)).Single().AddressName),
                $"F15 {vendor} 批首大小写可被真实 Driver 解析");
    }

    /// <summary>对全部注册模式逐档验证点数守恒、批上限、偏移范围及原输入不被修改，包含关闭组包的 Cpu 模式。</summary>
    /// <param name="assert">断言结果回调。</param>
    private static void VerifyAllFamilies(Action<bool, string> assert)
    {
        var cases = new (ProtocolFamily Family, string First, string Second, DataType Type)[]
        {
            (ProtocolFamily.Siemens, "M100", "M108", DataType.Short),
            (ProtocolFamily.Modbus, "100.1", "108.9", DataType.Bool),
            (ProtocolFamily.Mitsubishi, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Omron, "CIO100.3", "CIO108.8", DataType.Bool),
            (ProtocolFamily.Fuji, "M1.100.3", "M1.108.8", DataType.Bool),
            (ProtocolFamily.Fuji_SPB, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Fuji_CST, "W9.100", "W9.108", DataType.Short),
            (ProtocolFamily.Keyence, "MR100", "MR108", DataType.Bool),
            (ProtocolFamily.Yokogawa, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Panasonic, "R100", "R108", DataType.Short),
            (ProtocolFamily.Yaskawa, "100", "108", DataType.Short),
            (ProtocolFamily.GE, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Fatek, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Fanuc, "D100", "D108", DataType.Short),
            (ProtocolFamily.LSis_Cnet, "MW100", "MW108", DataType.Short),
            (ProtocolFamily.LSis_Cpu, "M100", "M108", DataType.Bool),
            (ProtocolFamily.LSis_FastEnet, "MW100", "MW108", DataType.Short),
            (ProtocolFamily.Cimon, "D100", "D108", DataType.Short),
            (ProtocolFamily.XinJE, "D100", "D108", DataType.Short),
            (ProtocolFamily.Vigor, "C200", "C208", DataType.Short),
            (ProtocolFamily.Toyota, "D100", "D108", DataType.Short),
            (ProtocolFamily.AllenBradley, "N7:100", "N7:108", DataType.Short),
            (ProtocolFamily.MitsubishiFx, "M100", "M108", DataType.Bool),
            (ProtocolFamily.Beckhoff, "M100.3", "M108.7", DataType.Bool)
        };
        assert(cases.Select(c => c.Family).ToHashSet().SetEquals(PackerFactory.GetRegisteredFamilies()), "全量矩阵与全部注册协议模式一致，不漏新增模式");
        foreach (var c in cases)
        foreach (int max in new[] { 1, 2, 3, 4, 7, 8, 17, 64, 128, 255, 1000, 10000 })
        {
            var p = PackerFactory.GetPacker(c.Family)!;
            var first = new AddressDetails(c.First, c.Type);
            var second = new AddressDetails(c.Second, c.Type);
            var invalid = new AddressDetails("!bad!", DataType.Short);
            var disabled = new AddressDetails(c.First, c.Type) { IsEnable = false };
            var virtualPoint = new AddressDetails(c.First, c.Type) { AddressType = AddressType.VirtualStatic, AddressExtendParam = new object() };
            var packed = p.Pack(new Address(new List<AddressDetails> { first, second, invalid, disabled, virtualPoint }), max, DataFormat.ABCD)!;
            assert(packed.AddressArray.Sum(a => IsBatch(a) ? ((List<BytesModel>)a.AddressExtendParam!).Count : 1) == 5,
                $"{c.Family}/{max} 所有原点都存在");
            assert(packed.AddressArray.Where(IsBatch).All(a => a.Length <= Math.Min(max, p.PhysicalMaxBytes)
                && ((List<BytesModel>)a.AddressExtendParam!).All(m => m.StartBit >= 0 && (long)m.StartBit + m.Length <= a.Length)),
                $"{c.Family}/{max} 所有模型均位于有效批范围");
            assert(first.AddressExtendParam == null && second.AddressExtendParam == null
                && packed.AddressArray.Any(a => ReferenceEquals(a, virtualPoint)) && packed.AddressArray.Any(a => ReferenceEquals(a, disabled)),
                $"{c.Family}/{max} 输入元数据、禁用点、虚拟点不被改变");
        }
    }

    /// <summary>使用原生内存服务端独立计算文本期望，逐协议检查 ASCII/UTF16 与 String/Char 的长度单位。</summary>
    /// <param name="assert">断言结果回调。</param>
    private static void VerifyNativeText(Action<bool, string> assert)
    {
        foreach (var (vendor, server, name) in new (string, DeviceServer, string)[]
        {
            ("Siemens", new SiemensS7Server(), "M100"), ("Modbus", new ModbusTcpServer(), "100"),
            ("Mitsubishi", new MelsecMcServer(), "D100"), ("Keyence", new MelsecMcServer(), "D100"),
            ("MitsubishiFx", new MelsecFxLinksServer(), "D100"), ("Omron", new OmronFinsServer(), "D100"),
            ("Fuji", new FujiSPHServer(), "M1.100"), ("Fuji_SPB", new FujiSPBServer(), "D100"),
            ("Fuji_CST", new FujiCommandSettingTypeServer(), "D100"), ("GE", new GeSRTPServer(), "R100"),
            ("Fatek", new FatekProgramServer(), "D100"), ("Panasonic", new PanasonicMewtocolServer(), "DT100"),
            ("Yokogawa", new YokogawaLinkServer(), "D100"), ("Toyota", new ToyoPucServer(), "D100"),
            ("Vigor", new VigorServer(), "D100"), ("XinJE", new XinJEServer(), "D100"),
            ("Cimon", new CimonServer(), "D100"), ("Beckhoff", new BeckhoffAdsServer(), "M100"),
            ("Yaskawa", new MemobusTcpServer(), "100"),
            ("LSis_Cnet", new LSisServer(), "MW100"), ("LSis_FastEnet", new LSisServer(), "MW100"),
            ("Fanuc", new Snet.Driver.Robot.FANUC.FanucRobotServer(), "D100")
        })
        using (server)
        foreach (var encoding in new[] { EncodingType.ASCII, EncodingType.Unicode })
        foreach (var type in new[] { DataType.String, DataType.Char })
        {
            var bytes = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOP");
            var write = server.Write(name, bytes);
            var codec = encoding == EncodingType.Unicode ? Encoding.Unicode : Encoding.ASCII;
            // GE 客户端先把 R/AI/AQ 的字节长度除二写入命令；服务端 Read 接收的是命令中的字数，不能直接混用。
            byte[] Read(string address, ushort length)
            {
                if (server is GeSRTPServer)
                {
                    byte[] command = GeHelper.BuildReadCommand(address, length, false).Content;
                    return server.Read(address, BitConverter.ToUInt16(command, 46)).Content;
                }
                return server.Read(address, length).Content;
            }
            var expected = codec.GetString(Read(name, 4));
            assert(write.IsSuccess, $"{vendor} 原生文本夹具写入成功");
            VerifyText(assert, vendor, expected, new(name, type, 4, encoding), Read);
        }
    }

    /// <summary>真实命令分帧后按帧地址读取内存服务端，验证大跨度位点与边界值，而非一次性线性伪响应。</summary>
    /// <param name="assert">断言结果回调。</param>
    private static void VerifyFramedBits(Action<bool, string> assert)
    {
        using var yokogawa = new YokogawaLinkServer();
        using var fx = new MelsecFxLinksServer();
        using var client = new MelsecFxLinksOverTcp();
        foreach (string vendor in new[] { "Yokogawa", "MitsubishiFx" })
        {
            DeviceServer server = vendor == "Yokogawa" ? yokogawa : fx;
            server.Write("M100", new[] { true });
            server.Write("M1123", new[] { false });
            server.Write("M1200", new[] { true });
            var batches = Pack(vendor, new("M100", DataType.Bool), new("M1123", DataType.Bool), new("M1200", DataType.Bool));
            foreach (var batch in batches)
            {
                var response = new List<byte>();
                if (vendor == "Yokogawa")
                    foreach (var command in YokogawaLinkTcp.BuildReadCommand(1, batch.AddressName, batch.Length, false).Content)
                    {
                        int start = (command[6] << 24) | (command[7] << 16) | (command[8] << 8) | command[9];
                        ushort words = (ushort)((command[10] << 8) | command[11]);
                        response.AddRange(server.Read("M" + start, words).Content);
                    }
                else
                    foreach (var command in MelsecFxLinksHelper.BuildReadCommand(client, batch.AddressName, batch.Length, false).Content)
                    {
                        string text = Encoding.ASCII.GetString(command);
                        response.AddRange(server.Read(text[7..^2], Convert.ToUInt16(text[^2..], 16)).Content);
                    }
                var decoded = new BytesHandler().Transform(response.ToArray(), DateTime.Now, (List<BytesModel>)batch.AddressExtendParam!);
                assert(decoded.Status && Values(decoded).All(v => Equals(v.ResultValue, v.AddressName != "M1123")),
                    $"F05 {vendor} 实际分帧响应在128字节边界及重启批首后保持真值");
            }
        }
    }

    /// <summary>直接运行生产 DaqAbstract 集合入口，验证成功降级点和失败批同时存在时不丢点、不伪报成功。</summary>
    /// <param name="assert">断言结果回调。</param>
    private static void VerifyCollectionUnpack(Action<bool, string> assert)
    {
        var daq = new RepairDaq();
        var batch = Pack("Siemens", new AddressDetails("M100", DataType.Short)).Single();
        var typed = new AddressValue(QualityType.Normal, true, true, "成功", new AddressDetails("coil", DataType.Bool));
        var failed = new AddressValue(QualityType.Exception, null!, new byte[] { 0, 1 }, "通信失败", batch);
        var pairs = new ConcurrentDictionary<string, AddressValue>();
        pairs["typed"] = typed;
        pairs["batch"] = failed;
        foreach (var result in new[] { daq.UnPacker(pairs), daq.UnPackerAsync(pairs).GetAwaiter().GetResult() })
        {
            var values = ((IEnumerable<ConcurrentDictionary<string, AddressValue>>)result.ResultData!).SelectMany(d => d.Values).ToList();
            assert(!result.Status && values.Count == 2 && values.Any(v => ReferenceEquals(v, typed))
                && values.Any(v => v.AddressName == "M100" && v.Quality == QualityType.Exception && v.ResultValue == null),
                "F01 Daq 同步/异步保留部分失败点，不使用失败批中的旧字节伪造成功值");
        }
    }

    /// <summary>验证扩展审查发现的 Cimon 字寻址、CST 元素宽度，以及缺少系列信息的 Modbus 派生协议安全透传。</summary>
    /// <param name="assert">断言结果回调。</param>
    private static void VerifyAdditionalDriverContracts(Action<bool, string> assert)
    {
        assert(typeof(FujiPacker).GetConstructor(Type.EmptyTypes) != null, "FujiPacker 保留公开无参构造函数，不破坏旧调用方二进制入口");
        foreach (string name in new[] { "M1.16777216", "I16777216", "Q16777216", "M1.16777216.3" })
            assert(Pack("Fuji", new AddressDetails(name, DataType.Short)).All(a => !IsBatch(a)), $"SPH {name} 超24位地址不截断到低地址");
        foreach (string name in new[] { "100;101", "x=3;x=4;100", "mfc=32;mfc=33;100" })
            assert(Pack("Yaskawa", new AddressDetails(name, DataType.Short)).All(a => !IsBatch(a)), $"Memobus {name} 不接受有歧义的多地址/重复修饰符");
        try
        {
            _ = new LSisPacker(ProtocolFamily.Siemens);
            assert(false, "LSis 构造函数应拒绝非 LS 协议族");
        }
        catch (ArgumentOutOfRangeException) { assert(true, "LSis 非法模式异常与中文注释一致"); }
        using (var pccc = new RepairPccc())
        {
            var point = Pack("AllenBradley", new AddressDetails("ST1:0", DataType.String, 4, EncodingType.ASCII)).Single();
            var direct = pccc.ReadString(point.AddressName, point.Length, Encoding.ASCII);
            var value = new AddressValue(QualityType.Normal, direct.Content, direct.Content, "成功", point);
            assert(direct.IsSuccess && direct.Content == "ABCD" && ReferenceEquals(Values(new BytesHandler().Transform(value)).Single(), value),
                "PCCC 仍运行真实 ReadString 去长度头和字内交换，UnPacker 不重复通用解码");
        }
        foreach (var (vendor, server, first, second) in new (string, DeviceServer, string, string)[]
        {
            ("Cimon", new CimonServer(), "D100", "D101"),
            ("Fuji_CST", new FujiCommandSettingTypeServer(), "W9.100", "W9.101"),
            ("Fuji_CST", new FujiCommandSettingTypeServer(), "BD100", "BD101"),
            ("Fuji_CST", new FujiCommandSettingTypeServer(), "S100", "S101")
        })
        using (server)
        {
            server.Write(first, new byte[] { 0x11, 0x22 });
            server.Write(second, new byte[] { 0x77, 0x88 });
            var batch = Pack(vendor, new(first, DataType.Byte), new(second, DataType.Byte)).Single();
            var decoded = new BytesHandler().Transform(server.Read(batch.AddressName, batch.Length).Content, DateTime.Now,
                (List<BytesModel>)batch.AddressExtendParam!);
            var expected = server.Read(second, 1).Content[0];
            assert(decoded.Status && Equals(Values(decoded).Single(v => v.AddressName == second).ResultValue, expected),
                $"新增契约 {vendor}/{second} 不因字节、字、双字设备宽度差异静默错值");
        }
        var bit = new AddressDetails("M100", DataType.Bool);
        assert(ReferenceEquals(Pack("Cimon", bit).Single(), bit), "Cimon 位读114不能由字读82代替，完整保留单点");
        foreach (string vendor in new[] { "DeltaTcpNet", "DeltaSerial", "DeltaSerialOverTcp", "DeltaSerialAscii", "DeltaSerialAsciiOverTcp",
            "InovanceTcpNet", "InovanceSerial", "InovanceSerialOverTcp", "MegMeetTcpNet", "MegMeetSerial", "MegMeetSerialOverTcp" })
        {
            var input = new Address(new AddressDetails("D100", DataType.Short));
            assert(!PackerHandler.CanAutoPack(vendor) && ReferenceEquals(input, new PackerHandler().AddressAutoPackOrPassthrough(input, vendor)),
                $"{vendor} 无系列信息不宣称可优化原生设备地址，完整透传");
        }
        foreach (string code in new[] { "ID", "QD" })
        foreach (int boundary in new[] { 10000, 20000, 30000 })
            assert(Pack("XinJE", new(code + (boundary - 1), DataType.Short), new(code + boundary, DataType.Short)).Count == 2,
                $"XinJE XD {code}{boundary} 物理地址分段不跨批");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            new BytesHandler().TransformAsync(new byte[] { 1 }, DateTime.Now,
                new[] { new BytesModel("M0", "", 0, 1, DataType.Byte) }, token: cancelled.Token).GetAwaiter().GetResult();
            assert(false, "解包开始前取消应传播取消状态");
        }
        catch (OperationCanceledException) { assert(true, "解包取消不伪装成点位转换失败"); }
        Parallel.For(0, 64, i =>
        {
            var p = PackerFactory.GetPacker(ProtocolFamily.Fuji_SPB)!;
            var result = p.Pack(new Address(new AddressDetails("M" + (100 + i), DataType.Bool)), 0, DataFormat.ABCD)!;
            if (result.AddressArray.Count != 1 || !IsBatch(result.AddressArray[0])) throw new InvalidOperationException("共享组包器并发结果异常");
        });
        assert(true, "共享 Packer 实例并发64次组包不依赖可变实例字段或同步锁");
    }

    /// <summary>比较固定长度文本的原生 Driver 结果与批量读取后解码结果。</summary>
    /// <param name="assert">断言回调。</param>
    /// <param name="vendor">供应商或协议族名称。</param>
    /// <param name="expected">原生 ReadString 的独立期望值。</param>
    /// <param name="point">包含原始 Length 和编码的文本点。</param>
    /// <param name="read">原生服务端原始字节读取接口。</param>
    private static void VerifyText(Action<bool, string> assert, string vendor, string expected, AddressDetails point, Func<string, ushort, byte[]> read)
    {
        var batch = Pack(vendor, point).Single();
        var result = new BytesHandler().Transform(read(batch.AddressName, batch.Length), DateTime.Now, (List<BytesModel>)batch.AddressExtendParam!);
        assert(Equals(Values(result).Single().ResultValue, expected), $"F02 {vendor} 文本 Length 保持原生 Driver 读取单位");
    }

    /// <summary>以较大显式上限组包，覆盖默认上限掩盖的跨帧缺陷。</summary>
    /// <param name="vendor">供应商或协议族名称。</param>
    /// <param name="points">原始点位集合。</param>
    /// <returns>优化批和安全降级点。</returns>
    private static List<AddressDetails> Pack(string vendor, params AddressDetails[] points) =>
        new PackerHandler().AddressAutoPackOrPassthrough(new Address(points.ToList()), vendor, 10000)!.AddressArray;

    /// <summary>判断测试输出是否为真实优化批，而非原样降级点。</summary>
    /// <param name="point">输出点位。</param>
    /// <returns>描述标记为优化批时为真。</returns>
    private static bool IsBatch(AddressDetails point) => point.AddressDescribe?.StartsWith("packer - ") == true;

    /// <summary>读取解码结果，即使部分模型失败仍检查返回的完整结果。</summary>
    /// <param name="result">解码操作结果。</param>
    /// <returns>解码出的点位列表。</returns>
    private static List<AddressValue> Values(OperateResult result) => ((ConcurrentDictionary<string, AddressValue>)result.ResultData!).Values.ToList();
}

/// <summary>仅替代 PCCC 的通信读取，专用 ReadString 仍执行生产 Driver 实现，避免测试伪造自身解码逻辑。</summary>
internal sealed class RepairPccc : AllenBradleyPcccNet
{
    /// <summary>返回含长度头和字内交换字符的 ST 固定夹具，不连接 CIP 端口。</summary>
    /// <param name="address">生产 ReadString 传入的 ST 地址。</param>
    /// <param name="length">生产 Driver 要求读取的原始字节数。</param>
    /// <returns>长度为4、内容为BADC的原始文件字节，按请求长度截取。</returns>
    public override Snet.Driver.OResult<byte[]> Read(string address, ushort length) =>
        Snet.Driver.OResult.CreateSuccessResult(new byte[] { 4, 0, (byte)'B', (byte)'A', (byte)'D', (byte)'C' }.Take(length).ToArray());
}

/// <summary>仅开放继承的生产解包入口；所有通信方法均抛异常，保证回归不会连接或写入真实设备。</summary>
internal sealed class RepairDaq : DaqAbstract<RepairDaq, object>
{
    /// <summary>禁止获取连接对象。</summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> GetBaseObjectAsync(CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止查询设备状态。</summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> GetStatusAsync(CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止关闭外部连接。</summary>
    /// <param name="hardClose">强制关闭标志。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> OffAsync(bool hardClose = false, CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止开启连接。</summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> OnAsync(CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止采集设备。</summary>
    /// <param name="address">原始地址。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> ReadAsync(Address address, CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止启动订阅。</summary>
    /// <param name="address">原始地址。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> SubscribeAsync(Address address, CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止操作外部订阅。</summary>
    /// <param name="address">原始地址。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> UnSubscribeAsync(Address address, CancellationToken token = default) => throw new NotSupportedException();
    /// <summary>禁止任何设备写入。</summary>
    /// <param name="values">不会发送的写入值。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>不会返回。</returns>
    /// <exception cref="NotSupportedException">禁止通信。</exception>
    public override Task<OperateResult> WriteAsync(ConcurrentDictionary<string, (object value, EncodingType? encodingType)> values, CancellationToken token = default) => throw new NotSupportedException();
}
