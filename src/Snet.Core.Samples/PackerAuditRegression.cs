using Snet.Core.handler;
using Snet.Core.packer;
using Snet.Model.data;
using Snet.Model.@enum;
using Snet.Driver.Core.Address;
using Snet.Driver.Profinet.AllenBradley;
using Snet.Driver.Profinet.Melsec;
using Snet.Driver.Profinet.Omron;
using Snet.Driver.Core.Device;
using Snet.Driver.Profinet.Fuji;
using Snet.Driver.Profinet.GE;
using Snet.Driver.Profinet.FATEK;
using Snet.Driver.Profinet.Panasonic;
using Snet.Driver.Profinet.Yokogawa;

namespace Snet.Core.Samples;

/// <summary>独立验证全部协议的地址拒绝、批次边界和已确认的驱动语义，避免只按组包模型填值造成自洽假通过。</summary>
internal static class PackerAuditRegression
{
    /// <summary>运行全部协议的边界回归；断言失败由样例入口统一计数并返回非零退出码。</summary>
    /// <param name="assert">接收断言结果和中文说明的测试回调。</param>
    internal static void Run(Action<bool, string> assert)
    {
        Console.WriteLine("--- 2026-10 全协议边界回归 ---");
        VerifyDriverResponses(assert);
        var cases = new (ProtocolFamily Family, string Valid, string Invalid, DataType Type)[]
        {
            (ProtocolFamily.Siemens, "M100", "M100garbage", DataType.Short),
            (ProtocolFamily.Modbus, "100", "bad=1;100", DataType.Short),
            (ProtocolFamily.Mitsubishi, "D100", "D4294967396", DataType.Short),
            (ProtocolFamily.Omron, "D100", "D2147483647", DataType.Short),
            (ProtocolFamily.Fuji, "D100", "D100.bad", DataType.Short),
            (ProtocolFamily.Keyence, "DM100", "Q100", DataType.Short),
            (ProtocolFamily.Yokogawa, "D100", "D2147483647", DataType.Short),
            (ProtocolFamily.Panasonic, "DT100", "Q100", DataType.Short),
            (ProtocolFamily.Yaskawa, "100", "x=bad;100", DataType.Short),
            (ProtocolFamily.GE, "R100", "R2147483647", DataType.Short),
            (ProtocolFamily.Fatek, "D100", "D65536", DataType.Short),
            (ProtocolFamily.Fanuc, "D100", "D100garbage", DataType.Short),
            (ProtocolFamily.LSis_Cnet, "MW100", "MW100.bad", DataType.Short),
            (ProtocolFamily.LSis_Cpu, "M100", "M100000000", DataType.Bool),
            (ProtocolFamily.LSis_FastEnet, "MW100", "MWgarbage100", DataType.Short),
            (ProtocolFamily.Cimon, "D100", "D100.bad!", DataType.Short),
            (ProtocolFamily.XinJE, "D100", "D4294967396", DataType.Short),
            (ProtocolFamily.Vigor, "C200", "C2147483647", DataType.Short),
            (ProtocolFamily.Toyota, "D100", "D100000100", DataType.Short),
            (ProtocolFamily.AllenBradley, "N7:100", "N65536:100", DataType.Short),
            (ProtocolFamily.MitsubishiFx, "D100", "s=256;D100", DataType.Short),
            (ProtocolFamily.Beckhoff, "M100", "ig=0x100000000;100", DataType.Short),
        };
        foreach (var c in cases)
        {
            var p = PackerFactory.GetPacker(c.Family)!;
            var packed = p.Pack(Create((c.Valid, c.Type), (c.Invalid, c.Type)), 0, DataFormat.ABCD)!;
            assert(p.ParseAddress(c.Invalid).ByteIndex < 0, $"{c.Family} 拒绝非法/溢出地址 {c.Invalid}");
            assert(packed.AddressArray.Any(a => a.AddressName == c.Invalid && a.AddressDataType == c.Type),
                $"{c.Family} 非法点原样保留，不拖垮合法点");
            assert(packed.AddressArray.Where(IsBatch).SelectMany(Models).Any(m => m.Address == c.Valid),
                $"{c.Family} 合法点仍可组包");
            assert(packed.AddressArray.Where(IsBatch).All(a => Models(a).All(m => m.StartBit >= 0 && m.StartBit + m.Length <= a.Length)),
                $"{c.Family} 每个解包模型均位于批次缓冲区内");
            var disabled = new AddressDetails(c.Valid, c.Type) { IsEnable = false, AddressExtendParam = new object() };
            var virtualPoint = new AddressDetails(c.Valid, c.Type) { AddressType = AddressType.VirtualStatic, AddressExtendParam = new object() };
            var passthrough = p.Pack(new Address(new List<AddressDetails> { disabled, virtualPoint }), 0, DataFormat.ABCD)!;
            assert(passthrough.AddressArray.Count == 2 && passthrough.AddressArray.All(a => !IsBatch(a))
                && !passthrough.AddressArray[0].IsEnable && passthrough.AddressArray[1].AddressType == AddressType.VirtualStatic
                && ReferenceEquals(passthrough.AddressArray[0].AddressExtendParam, disabled.AddressExtendParam)
                && ReferenceEquals(passthrough.AddressArray[1].AddressExtendParam, virtualPoint.AddressExtendParam),
                $"{c.Family} 禁用点与虚拟点不被转成真实、启用的 PLC 读取批");
        }

        var differentWidths = PackerFactory.GetPacker(ProtocolFamily.Siemens)!.Pack(new Address(new List<AddressDetails>
        {
            new("M100", DataType.String, 4), new("M100", DataType.String, 8), new("M100", DataType.String, 10),
        }), 0, DataFormat.ABCD)!;
        assert(differentWidths.AddressArray.Where(IsBatch).SelectMany(Models).Count() == 3
            && differentWidths.AddressArray.Where(IsBatch).Max(a => a.Length) == 10,
            "同名同类型但字符串长度不同的点不被错误去重丢失");
        var widthBatch = differentWidths.AddressArray.Single(IsBatch);
        var widthResult = new BytesHandler().Transform(System.Text.Encoding.UTF8.GetBytes("ABCDEFGHIJ"), DateTime.Now, Models(widthBatch));
        var widthValues = (System.Collections.Concurrent.ConcurrentDictionary<string, AddressValue>)widthResult.ResultData!;
        assert(widthValues.Count == 3 && widthValues.Values.Select(v => v.ResultValue?.ToString()).OrderBy(v => v?.Length)
            .SequenceEqual(new[] { "ABCD", "ABCDEFGH", "ABCDEFGHIJ" }), "三种同名字符串规格解包也保留全部三个值");
        var asyncWidthResult = new BytesHandler().TransformAsync(System.Text.Encoding.UTF8.GetBytes("ABCDEFGHIJ"), DateTime.Now, Models(widthBatch)).GetAwaiter().GetResult();
        var asyncWidths = (System.Collections.Concurrent.ConcurrentDictionary<string, AddressValue>)asyncWidthResult.ResultData!;
        assert(asyncWidthResult.Status && asyncWidths.Count == 3 && asyncWidths.Values.All(v => v.AddressName == "M100"),
            "异步解包同样保留同名三个值，内部键后缀不改变对外地址名");

        var melsec = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        foreach (var (name, expected) in new[] { ("W1A0", 832), ("WABC", 5496), ("XAF", 21), ("SBFF", 31) })
            assert(melsec.ParseAddress(name).ByteIndex == expected, $"三菱十六进制字母地址 {name} 与 Driver 一致");

        var siemens = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;
        foreach (var name in new[] { "DBbad.DBB0", "DB65536.DBB0", "M100.", "M100.9999999999" })
            assert(siemens.ParseAddress(name).ByteIndex < 0, $"S7 非法块号/不完整尾缀 {name} 降级");
        assert(siemens.GetRegionKey("MB100") == "M" && siemens.ParseAddress("MB100").ByteIndex == 100,
            "S7 MB 与 M 地址别名共享字节空间");
        assert(siemens.ParseAddress("AI10").ByteIndex == 10 && siemens.ParseAddress("AQ10").ByteIndex == 10,
            "S7 AI/AQ 根据 Driver 的非 isCT 路径按字节编址");

        foreach (var family in new[] { ProtocolFamily.Omron, ProtocolFamily.Fuji, ProtocolFamily.Modbus })
        {
            string name = family switch { ProtocolFamily.Omron => "CIO100.8", ProtocolFamily.Fuji => "M100.8", _ => "100.1" };
            var one = PackerFactory.GetPacker(family)!.Pack(Create((name, DataType.Bool)), 1, DataFormat.ABCD)!;
            assert(one.AddressArray.All(a => !IsBatch(a) || a.Length <= 1), $"{family} 字边界位点不生成超过 1 字节上限的批次");
        }

        var modbus = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        foreach (var name in new[] { "100.", "4294967396", "100;garbage", "x=13;100" })
        {
            var packed = modbus.Pack(Create((name, DataType.Short)), 0, DataFormat.ABCD)!;
            assert(packed.AddressArray.All(a => !IsBatch(a)), $"Modbus 非法地址/无法建模功能码 {name} 不进入批次");
        }
        var coils = modbus.Pack(Create(("x=1;100", DataType.Short)), 0, DataFormat.ABCD)!;
        assert(coils.AddressArray.All(a => !IsBatch(a)), "Modbus 线圈功能码配字类型不生成错误字节布局");

        var yaskawa = PackerFactory.GetPacker(ProtocolFamily.Yaskawa)!;
        var dot = yaskawa.Pack(Create(("100.1", DataType.Bool)), 0, DataFormat.ABCD)!;
        assert(dot.AddressArray.All(a => !IsBatch(a)), "当前 Memobus BuildReadCommand 仅接受数字 ushort，旧数字点号地址降级");

        var slc = PackerFactory.GetPacker(ProtocolFamily.AllenBradley)!;
        var longSlc = slc.Pack(Create(("N7:0", DataType.Short), ("N7:249", DataType.Short)), 1000, DataFormat.ABCD)!;
        assert(longSlc.AddressArray.Where(IsBatch).All(a => a.Length <= byte.MaxValue), "SLC 请求长度不超过 Driver 的单字节长度字段");

        var vigor = PackerFactory.GetPacker(ProtocolFamily.Vigor)!.Pack(Create(("C199", DataType.Short), ("C200", DataType.Short)), 0, DataFormat.ABCD)!;
        assert(vigor.AddressArray.Count(IsBatch) == 2, "Vigor C199/C200 对应不同 DataCode 与元素宽度，禁止跨边界合批");
        var fatek = PackerFactory.GetPacker(ProtocolFamily.Fatek)!;
        assert(fatek.ParseAddress("RT100").ByteIndex == 200 && fatek.ParseAddress("RC100").ByteIndex == 200,
            "Fatek RT/RC 当前值区按字编址，不按接点位号建模");
        assert(fatek.Pack(Create(("RT100", DataType.Bool)), 0, DataFormat.ABCD)!.AddressArray.All(a => !IsBatch(a)),
            "Fatek RT Bool 不混入接点位流批");
        foreach (string name in new[] { "T100", "C100" })
            assert(PackerFactory.GetPacker(ProtocolFamily.Yokogawa)!.Pack(Create((name, DataType.Bool)), 0, DataFormat.ABCD)!.AddressArray.All(a => !IsBatch(a)),
                $"Yokogawa {name} 当前字读路径按值区读取，Bool 不错误合入接点位流");

        var original = new AddressDetails("M100", DataType.IntArray, ushort.MaxValue) { AddressAnotherName = "超长数组", AddressExtendParam = new object() };
        try
        {
            var large = siemens.Pack(new Address(new List<AddressDetails> { original }), 0, DataFormat.ABCD)!;
            assert(large.AddressArray.Count == 1 && large.AddressArray[0].Length == original.Length
                && ReferenceEquals(large.AddressArray[0].AddressExtendParam, original.AddressExtendParam), "无法用 ushort 字节模型表示的数组保持原读取契约和扩展参数");
        }
        catch (ArgumentOutOfRangeException)
        {
            assert(false, "超长降级数组不应使整个 Pack 抛出长度异常");
        }
        assert(!PackerHandler.CanAutoPack("999999"), "未注册数字枚举不能报告支持自动组包");
        foreach (var protocol in new[] { "MelsecMcRNet", "MelsecA1ENet", "MelsecA1EAsciiNet", "MelsecFxSerial", "MelsecFxSerialOverTcp", "SiemensFetchWriteNet" })
        {
            var address = Create(("X010", DataType.Bool));
            assert(!PackerHandler.CanAutoPack(protocol), $"{protocol} 地址语义与共享 Packer 不一致，移除错误映射");
            assert(ReferenceEquals(new PackerHandler().AddressAutoPackOrPassthrough(address, protocol), address),
                $"{protocol} 不修改输入，仍走原有单点读取");
        }
    }

    /// <summary>直接构建当前 Driver 的命令并使用其离线 FINS 内存服务端生成响应；不按 Packer 偏移填值，也不连接设备。</summary>
    /// <param name="assert">接收真实驱动行为与组包结果的比较断言。</param>
    private static void VerifyDriverResponses(Action<bool, string> assert)
    {
        var ai = S7AddressData.ParseFrom("AI10", 2);
        assert(ai.IsSuccess && ai.Content.AddressStart == 80, "当前 S7 Driver 的 AI10 编码为位地址 80，即字节 10");
        var mc = McAddressData.ParseMelsecFrom("WABC", 1, false);
        assert(mc.IsSuccess && mc.Content.AddressStart == 0xABC, "当前 MC Driver 原生支持 WABC 十六进制地址");
        assert(MelsecMcRNet.AnalysisAddress("X010").Content2 == 16, "当前 MC R Driver X010 为十六进制 16，不能复用标准 MC 的八进制 8");
        assert(!MelsecHelper.McA1EAnalysisAddress("SM100").IsSuccess, "当前 A1E Driver 不支持标准 MC 的 SM 设备码");
        assert(AllenBradleySLCNet.BuildReadCommand("N7:0", 500).Content[9] == 244,
            "SLC 原 500 字节批在真实命令中截断为 244，255 上限修复有驱动证据");

        VerifyServerBits(ProtocolFamily.Mitsubishi, new MelsecMcServer(), "M100", "M103", assert);
        VerifyServerBits(ProtocolFamily.Keyence, new MelsecMcServer(), "M100", "M103", assert);
        VerifyServerBits(ProtocolFamily.MitsubishiFx, new MelsecFxLinksServer(), "M100", "M103", assert);
        VerifyServerBits(ProtocolFamily.Fuji, new FujiSPBServer(), "M100.3", "M100.9", assert);
        VerifyServerBits(ProtocolFamily.Fuji, new FujiSPHServer(), "M1.100.3", "M1.100.9", assert);
        VerifyServerBits(ProtocolFamily.GE, new GeSRTPServer(), "M2", "M9", assert);
        VerifyServerBits(ProtocolFamily.Fatek, new FatekProgramServer(), "M100", "M103", assert);
        VerifyServerBits(ProtocolFamily.Yokogawa, new YokogawaLinkServer(), "X100", "X103", assert);
        VerifyServerBits(ProtocolFamily.Panasonic, new PanasonicMewtocolServer(), "X100", "X110", assert);

        using var fins = new OmronFinsServer();
        var p = PackerFactory.GetPacker(ProtocolFamily.Omron)!;
        foreach (string name in new[] { "CIO100.3", "CIO100.8", "CIO101.0" })
        {
            assert(fins.Write(name.Split('.')[0], new byte[] { 0, 0 }).IsSuccess && fins.Write(name, new[] { true }).IsSuccess,
                $"FINS 离线服务端原生写入位点 {name}");
            var batch = p.Pack(Create((name, DataType.Bool)), 0, DataFormat.ABCD)!.AddressArray.Single(IsBatch);
            var raw = fins.Read(batch.AddressName, batch.Length);
            var decoded = new BytesHandler().Transform(raw.Content, DateTime.Now, Models(batch));
            var values = (System.Collections.Concurrent.ConcurrentDictionary<string, AddressValue>)decoded.ResultData!;
            assert(raw.IsSuccess && decoded.Status && values[name].ResultValue is true,
                $"FINS {name} 由 Driver 字读生成的实际大端响应可正确解出 true");
        }
    }

    /// <summary>原生写入一真一假的非对齐位点，再按实际字读响应解包，验证布局与独立服务端一致。</summary>
    /// <param name="family">要验证的协议族。</param>
    /// <param name="server">Driver 离线内存服务端，方法退出时释放。</param>
    /// <param name="first">预期为真的位地址。</param>
    /// <param name="second">预期为假的位地址。</param>
    /// <param name="assert">接收布局验证结果的断言回调。</param>
    private static void VerifyServerBits(ProtocolFamily family, DeviceServer server, string first, string second, Action<bool, string> assert)
    {
        using (server)
        {
            assert(server.Write(first, new[] { true }).IsSuccess && server.Write(second, new[] { false }).IsSuccess,
                $"{family}/{server.GetType().Name} 原生布尔写入成功");
            var packed = PackerFactory.GetPacker(family)!.Pack(Create((first, DataType.Bool), (second, DataType.Bool)), 0, DataFormat.ABCD)!;
            var batches = packed.AddressArray.Where(IsBatch).ToArray();
            assert(batches.SelectMany(Models).Count() == 2, $"{family}/{server.GetType().Name} 两个位点均保留在批内");
            foreach (var batch in batches)
            {
                string serverHead = batch.AddressName;
                if (server is FujiSPBServer && serverHead.Contains('.'))
                {
                    string normalized = serverHead[..serverHead.IndexOf('.')];
                    assert(FujiSPBHelper.BuildReadCommand(1, serverHead, batch.Length).Content.SequenceEqual(
                        FujiSPBHelper.BuildReadCommand(1, normalized, batch.Length).Content),
                        "Fuji SPB 字读命令原生忽略位后缀，离线服务端使用其实际字锚点");
                    serverHead = normalized;
                }
                var raw = server.Read(serverHead, batch.Length);
                assert(raw.IsSuccess, $"{family}/{server.GetType().Name} 原生字读批首成功");
                if (!raw.IsSuccess) continue;
                var decoded = new BytesHandler().Transform(raw.Content, DateTime.Now, Models(batch));
                var values = (System.Collections.Concurrent.ConcurrentDictionary<string, AddressValue>)decoded.ResultData!;
                foreach (var m in Models(batch))
                    assert(decoded.Status && values[m.Address].ResultValue is bool value && value == (m.Address == first),
                        $"{family}/{server.GetType().Name} 真实响应解包 {m.Address} 与原生写入一致");
            }
        }
    }

    /// <summary>建立固定长度标量地址，测试输入独立于组包器输出。</summary>
    /// <param name="points">地址和标量数据类型。</param>
    /// <returns>可交给全部协议组包器的原始地址集合。</returns>
    private static Address Create(params (string Name, DataType Type)[] points) =>
        new(points.Select(p => new AddressDetails(p.Name, p.Type, 1) { EncodingType = EncodingType.UTF8 }).ToList());

    /// <summary>按批次标记识别组包请求，未组包原始点不会通过此检查。</summary>
    /// <param name="address">组包结果中的地址。</param>
    /// <returns>是否为新建批次。</returns>
    private static bool IsBatch(AddressDetails address) => address.AddressDescribe?.StartsWith("packer - ") == true;

    /// <summary>读取批次的解包元数据；仅用于已经通过批次检查的地址。</summary>
    /// <param name="address">带批内模型列表的地址。</param>
    /// <returns>该批包含的原始点位模型。</returns>
    private static List<BytesModel> Models(AddressDetails address) => (List<BytesModel>)address.AddressExtendParam!;
}
