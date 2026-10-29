using System.Collections.Concurrent;
using System.Text;
using Snet.Core.handler;
using Snet.Core.packer;
using Snet.Driver;
using Snet.Driver.Core.Address;
using Snet.Driver.Profinet.Beckhoff.Helper;
using Snet.Driver.Profinet.GE;
using Snet.Driver.Profinet.Vigor;
using Snet.Driver.Profinet.Vigor.Helper;
using Snet.Driver.ModBus;
using Snet.Driver.Profinet.Cimon.Helper;
using Snet.Driver.Profinet.Melsec.Helper;
using Snet.Driver.Profinet.Siemens;
using System.Reflection;
using Snet.Model.data;
using Snet.Model.@enum;

namespace Snet.Core.Samples;

/// <summary>2026年10月29日全协议复查回归，独立比较原生文本读取、命令字段边界及批首解析结果。</summary>
internal static class PackerCurrentAuditRegression
{
    /// <summary>运行新增缺陷回归，沿用样例入口的断言计数和非零失败退出码。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    internal static void Run(Action<bool, string> assert)
    {
        Console.WriteLine("--- 2026-10-29 原生单位与报文字段回归 ---");
        VerifyVigorText(assert);
        VerifyAddressFields(assert);
        VerifyCanonicalHeads(assert);
        VerifyModbusBits(assert);
        VerifyS7WordOnlyAreas(assert);
        VerifyWireTruncation(assert);
    }

    /// <summary>比较生产 Driver 的同步、异步 ReadString 与组包解码，覆盖普通字和32位计数器，数据按实际设备坐标生成。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyVigorText(Action<bool, string> assert)
    {
        using var device = new AuditVigorDevice();
        foreach (string first in new[] { "D100", "C100", "C200", "C300" })
        foreach (var encoding in new[] { EncodingType.UTF8, EncodingType.Unicode })
        {
            string next = first[0] + (int.Parse(first[1..]) + 1).ToString();
            var points = new[] { new AddressDetails(first, DataType.String, 4, encoding), new AddressDetails(next, DataType.String, 2, encoding) };
            var batch = Pack(ProtocolFamily.Vigor, points).Single();
            var models = (List<BytesModel>)batch.AddressExtendParam!;
            Encoding codec = encoding == EncodingType.Unicode ? Encoding.Unicode : Encoding.UTF8;
            var response = device.Read(batch.AddressName, batch.Length);
            var decoded = new BytesHandler().Transform(response.Content, DateTime.Now, models);
            var values = (ConcurrentDictionary<string, AddressValue>)decoded.ResultData!;
            var asyncDecoded = new BytesHandler().TransformAsync(response.Content, DateTime.Now, models).GetAwaiter().GetResult();
            var asyncValues = (ConcurrentDictionary<string, AddressValue>)asyncDecoded.ResultData!;
            foreach (var point in points)
            {
                var direct = device.ReadString(point.AddressName, point.Length, codec);
                var asyncDirect = device.ReadStringAsync(point.AddressName, point.Length, codec).GetAwaiter().GetResult();
                assert(decoded.Status && direct.IsSuccess && Equals(values.Values.Single(v => v.AddressName == point.AddressName).ResultValue, direct.Content),
                    $"Vigor {point.AddressName}/{encoding} 组包文本与原生 ReadString 完全一致");
                assert(asyncDecoded.Status && asyncDirect.IsSuccess && Equals(asyncValues.Values.Single(v => v.AddressName == point.AddressName).ResultValue, asyncDirect.Content),
                    $"Vigor {point.AddressName}/{encoding} 异步组包文本与原生 ReadStringAsync 完全一致");
            }
        }
        var oversized = new AddressDetails("C200", DataType.String, 4);
        var limited = PackerFactory.GetPacker(ProtocolFamily.Vigor)!.Pack(new Address(oversized), 8, DataFormat.ABCD)!;
        assert(ReferenceEquals(limited.AddressArray.Single(), oversized), "Vigor C200 文本需16字节，8字节上限时完整透传原点");
        var command = VigorVsHelper.BuildReadCommand(0, "C200", 4, false);
        assert(command.IsSuccess && command.Content.Count == 1 && command.Content[0][6] == 173 && command.Content[0][10] == 4,
            "Vigor 真实命令 DataCode173 的数量4表示四个32位计数器，不是四个16位字");
    }

    /// <summary>校验报文实际字段可表达的最大地址及其下一项；非法点必须保留对象身份，不污染相邻合法批。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyAddressFields(Action<bool, string> assert)
    {
        var cases = new (ProtocolFamily Family, string Last, string Overflow, DataType Type)[]
        {
            (ProtocolFamily.Siemens, "M2097151.7", "M2097152", DataType.Bool),
            (ProtocolFamily.Siemens, "DB1.DBB2097151", "DB1.DBB2097152", DataType.Byte),
            (ProtocolFamily.Siemens, "C16777215", "C16777216", DataType.Short),
            (ProtocolFamily.Siemens, "T16777215", "T16777216", DataType.Short),
            (ProtocolFamily.Mitsubishi, "D999999", "D1000000", DataType.Short),
            (ProtocolFamily.Mitsubishi, "WFFFFFF", "W1000000", DataType.Short),
            (ProtocolFamily.Mitsubishi, "ZR16777215", "ZR16777216", DataType.Short),
            (ProtocolFamily.Mitsubishi, "M999999", "M1000000", DataType.Bool),
            (ProtocolFamily.Keyence, "DM999999", "DM1000000", DataType.Short),
            (ProtocolFamily.Keyence, "EM899999", "EM900000", DataType.Short),
            (ProtocolFamily.Keyence, "ZRFFFFFF", "ZR1000000", DataType.Short),
            (ProtocolFamily.Keyence, "ZF16777215", "ZF16777216", DataType.Short),
            (ProtocolFamily.GE, "R65536", "R65537", DataType.Short),
            (ProtocolFamily.GE, "M65536", "M65537", DataType.Bool),
            (ProtocolFamily.Toyota, "DEFFF", "DF000", DataType.Short),
            (ProtocolFamily.Toyota, "EB1FFFF", "EB20000", DataType.Short),
            (ProtocolFamily.Toyota, "prg=255;K100", "prg=256;K100", DataType.Short),
            (ProtocolFamily.Vigor, "R999999", "R1000000", DataType.Short),
            (ProtocolFamily.Vigor, "D1008999", "D1009000", DataType.Short),
            (ProtocolFamily.Cimon, "D999999", "D1000000", DataType.Short),
        };
        foreach (var c in cases)
        {
            var p = PackerFactory.GetPacker(c.Family)!;
            var invalid = new AddressDetails(c.Overflow, c.Type) { AddressExtendParam = new object() };
            var output = Pack(c.Family, new(c.Last, c.Type), invalid);
            assert(p.ParseAddress(c.Last).ByteIndex >= 0 && output.Any(IsBatch), $"{c.Family} 报文字段末端 {c.Last} 仍能组包");
            assert(p.ParseAddress(c.Overflow).ByteIndex < 0 && output.Any(v => ReferenceEquals(v, invalid)),
                $"{c.Family} 超出字段 {c.Overflow} 原样保留，禁止截断或回绕后合批");
        }
        foreach (string name in new[] { "T1.0", "C1.0" })
        {
            var point = new AddressDetails(name, DataType.Short);
            assert(ReferenceEquals(Pack(ProtocolFamily.Siemens, point).Single(), point), $"S7 {name} 的 Driver 点号规则不同于纯元素编号，不合入普通 T/C 批");
        }
        var ge = GeHelper.BuildReadCommand("R65536", 2, false);
        var wrapped = GeHelper.BuildReadCommand("R65537", 2, false);
        assert(ge.IsSuccess && wrapped.IsSuccess && ge.Content[44] == 255 && ge.Content[45] == 255 && wrapped.Content[44] == 0 && wrapped.Content[45] == 0,
            "GE 真实命令证明16位地址字段溢出回绕，Packer 必须在合批前拒绝");
    }

    /// <summary>对 Packer 接受的混合大小写地址运行真实 Driver 解析器，确保批首合法且模型保留原始地址。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyCanonicalHeads(Action<bool, string> assert)
    {
        foreach (var c in new (ProtocolFamily Family, string Address, Func<string, bool> Valid)[]
        {
            (ProtocolFamily.GE, "Ai100", a => GeSRTPAddress.ParseFrom(a, 2, false).IsSuccess),
            (ProtocolFamily.GE, "aQ100", a => GeSRTPAddress.ParseFrom(a, 2, false).IsSuccess),
            (ProtocolFamily.GE, "sA100", a => GeSRTPAddress.ParseFrom(a, 2, false).IsSuccess),
            (ProtocolFamily.Vigor, "Sd100", a => VigorAddress.ParseFrom(a, 2, false).IsSuccess),
            (ProtocolFamily.Beckhoff, "Ig=0x4020;100", a => AdsHelper.AnalysisAddress(a, false).IsSuccess),
            (ProtocolFamily.Cimon, "d100", CanBuildCimonRead),
        })
        {
            var type = c.Address == "sA100" ? DataType.Bool : DataType.Short;
            var batch = Pack(c.Family, new AddressDetails(c.Address, type)).Single();
            assert(IsBatch(batch) && c.Valid(batch.AddressName) && ((List<BytesModel>)batch.AddressExtendParam!).Single().Address == c.Address,
                $"{c.Family} {c.Address} 批首符合 Driver 大小写要求，原点名称保留");
        }
    }

    /// <summary>沿生产 ReadBool 路径核对四种全局字节序，避免组包器固定交换寄存器字节而改变单点读取结果。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyModbusBits(Action<bool, string> assert)
    {
        using var device = new AuditModbusDevice();
        foreach (DataFormat format in new[] { DataFormat.ABCD, DataFormat.BADC, DataFormat.CDAB, DataFormat.DCBA })
        {
            device.DataFormat = (Snet.Driver.Core.DataFormat)format;
            var points = new[] { 0, 7, 8, 15 }.Select(bit => new AddressDetails("100." + bit, DataType.Bool)).ToList();
            var batch = PackerFactory.GetPacker(ProtocolFamily.Modbus)!.Pack(new Address(points), 0, format)!.AddressArray.Single();
            var models = (List<BytesModel>)batch.AddressExtendParam!;
            var response = device.Read(batch.AddressName, batch.Length).Content;
            var result = new BytesHandler().Transform(response, DateTime.Now, models);
            var values = (ConcurrentDictionary<string, AddressValue>)result.ResultData!;
            var asyncResult = new BytesHandler().TransformAsync(response, DateTime.Now, models).GetAwaiter().GetResult();
            var asyncValues = (ConcurrentDictionary<string, AddressValue>)asyncResult.ResultData!;
            foreach (var point in points)
            {
                var direct = device.ReadBool(point.AddressName);
                var asyncDirect = device.ReadBoolAsync(point.AddressName).GetAwaiter().GetResult();
                assert(direct.IsSuccess && Equals(values.Values.Single(v => v.AddressName == point.AddressName).ResultValue, direct.Content),
                    $"Modbus {format}/{point.AddressName} 组包位值与生产 ReadBool 一致");
                assert(asyncDirect.IsSuccess && Equals(asyncValues.Values.Single(v => v.AddressName == point.AddressName).ResultValue, asyncDirect.Content),
                    $"Modbus {format}/{point.AddressName} 异步组包位值与生产 ReadBoolAsync 一致");
            }
        }
        foreach (var point in new[]
        {
            new AddressDetails("x=3;100", DataType.Bool), new AddressDetails("x=4;100", DataType.Bool),
            new AddressDetails("file=1;100", DataType.Bool), new AddressDetails("format=BADC;100.1", DataType.Bool),
            new AddressDetails("format=BADC;100", DataType.Short), new AddressDetails("format=BADC;100", DataType.String, 4),
            new AddressDetails("100.1", DataType.Int), new AddressDetails("file=1;100.1", DataType.Short)
        })
            assert(ReferenceEquals(Pack(ProtocolFamily.Modbus, point).Single(), point),
                $"Modbus {point.AddressName}/{point.AddressDataType} 不将不兼容原生类型读取伪装为成功批");
        var oldModel = new BytesModel("100.0", "", 0, 2, DataType.Bool, boolIndex: 8);
        var rawPoint = new AddressDetails("100", DataType.ByteArray, 2) { AddressExtendParam = new List<BytesModel> { oldModel } };
        var rawOutput = PackerFactory.GetPacker(ProtocolFamily.Modbus)!.Pack(new Address(rawPoint), 0, DataFormat.BADC)!;
        assert(ReferenceEquals(rawOutput.AddressArray.Single(), rawPoint) && oldModel.BoolIndex == 8,
            "Modbus 字节序修正不修改原样透传点的历史扩展模型");
    }

    /// <summary>校验 S7 按字编码数量的区域，禁止从合并批掩盖单字节和奇数字节文本的原生读取差异。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyS7WordOnlyAreas(Action<bool, string> assert)
    {
        foreach (string region in new[] { "T", "C", "AI", "AQ" })
        {
            var singleByte = new AddressDetails(region + "100", DataType.Byte);
            var oddText = new AddressDetails(region + "100", DataType.String, 3);
            var evenText = new AddressDetails(region + "100", DataType.String, 4);
            assert(ReferenceEquals(Pack(ProtocolFamily.Siemens, singleByte).Single(), singleByte), $"S7 {region} 单字节点保留原生按字读取行为");
            assert(ReferenceEquals(Pack(ProtocolFamily.Siemens, oddText).Single(), oddText), $"S7 {region} 奇数文本原样保留，避免批长度与真实响应不符");
            assert(IsBatch(Pack(ProtocolFamily.Siemens, evenText).Single()), $"S7 {region} 偶数文本继续按原生字节数优化");
        }
        assert(IsBatch(Pack(ProtocolFamily.Siemens, new AddressDetails("M100", DataType.String, 3)).Single()),
            "S7 普通字节内存仍支持奇数字节文本");
    }

    /// <summary>使用真实命令生成器独立证明字段截断和数量取整，避免测试只复述 Packer 自己定义的上限。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyWireTruncation(Action<bool, string> assert)
    {
        var mcDecimal = McAddressData.ParseMelsecFrom("D1000000", 1, false).Content;
        var mcHex = McAddressData.ParseMelsecFrom("ZR16777216", 1, false).Content;
        var keyence = McAddressData.ParseKeyenceFrom("EM900000", 1, false).Content;
        assert(Encoding.ASCII.GetString(McAsciiHelper.BuildAsciiReadMcCoreCommand(mcDecimal, false), 10, 6) == "100000",
            "三菱真实 ASCII 命令将七位十进制地址截为前六位，不能继续自动合批");
        assert(McBinaryHelper.BuildReadMcCoreCommand(mcHex, false).Skip(4).Take(3).All(b => b == 0),
            "三菱真实 Binary 命令的24位地址溢出到零");
        assert(Encoding.ASCII.GetString(McAsciiHelper.BuildAsciiReadMcCoreCommand(keyence, false), 10, 6) == "100000",
            "基恩士 EM 先加100000后再进入六位 ASCII 字段，不能只检查输入数字");
        foreach (string area in new[] { "T", "C", "AI", "AQ" })
        {
            var parsed = S7AddressData.ParseFrom(area + "100", 3).Content;
            var command = SiemensS7Net.BuildReadCommand(new[] { parsed }, 1).Content;
            assert(command[23] == 0 && command[24] == 1,
                $"S7 {area} 真实命令将3字节编码为1个字，Packer 奇数文本必须保留直接读取");
        }
        var badModifier = ModbusInfo.BuildReadModbusCommand("format=BADC;100", 1, 1, true, 3);
        assert(!badModifier.IsSuccess, "Modbus 真实原始读命令不接受 format=，仅支持的数值类型可剥除该修饰符");
        assert(!ModbusInfo.BuildReadModbusCommand("100.1", 2, 1, true, 3).IsSuccess,
            "Modbus 真实字读命令不能解析点号，非Bool点必须保留原始读取契约");
    }

    /// <summary>只调用生产 CIMON 内部命令生成器，验证大小写要求，不连接 PLC。</summary>
    /// <param name="address">待验证的生产批首地址。</param>
    /// <returns>生产命令生成器认可该地址时为真。</returns>
    private static bool CanBuildCimonRead(string address)
    {
        var builder = typeof(CimonHelper).GetMethod("PackReadWriteCommand", BindingFlags.Static | BindingFlags.NonPublic)!;
        return ((OResult<byte[]>)builder.Invoke(null, new object?[] { (byte)1, (byte)82, address, 1, null })!).IsSuccess;
    }

    /// <summary>通过生产组包入口构造批次，使用显式大上限暴露仅默认长度测试看不到的边界。</summary>
    /// <param name="family">协议模式。</param>
    /// <param name="points">原始点位集合。</param>
    /// <returns>全部优化批和原样透传点。</returns>
    private static List<AddressDetails> Pack(ProtocolFamily family, params AddressDetails[] points) =>
        PackerFactory.GetPacker(family)!.Pack(new Address(points.ToList()), 10000, DataFormat.ABCD)!.AddressArray;

    /// <summary>通过模型和类型识别优化批，避免把原样点误当成优化成功。</summary>
    /// <param name="point">组包输出点。</param>
    /// <returns>携带生产解包模型的字节批时为真。</returns>
    private static bool IsBatch(AddressDetails point) => point.AddressDataType == DataType.ByteArray && point.AddressExtendParam is List<BytesModel>;
}

/// <summary>仅替代通信层的离线 Vigor 夹具，保留生产 ReadString、ReadStringAsync 和文本变换；不打开网络或串口。</summary>
internal sealed class AuditVigorDevice : VigorSerialOverTcp
{
    /// <summary>根据生产地址解析器选择16/32位元素，生成稳定的字节内容；与 Packer 模型无依赖。</summary>
    /// <param name="address">生产文本读取传入的原始地址。</param>
    /// <param name="length">原始 Driver 读取的元素数。</param>
    /// <returns>指定设备坐标起连续元素的响应字节；解析失败时返回 Driver 错误。</returns>
    public override OResult<byte[]> Read(string address, ushort length)
    {
        var parsed = VigorAddress.ParseFrom(address, length, false);
        if (!parsed.IsSuccess) return OResult.CreateFailedResult<byte[]>(parsed);
        int width = parsed.Content.DataCode == 173 ? 4 : 2;
        return OResult.CreateSuccessResult(Enumerable.Range(0, length * width)
            .Select(i => (byte)('A' + (parsed.Content.AddressStart * width + i) % 26)).ToArray());
    }

    /// <summary>复用同一离线存储响应，让生产异步文本读取完成原有解码路径。</summary>
    /// <param name="address">生产异步文本读取传入的地址。</param>
    /// <param name="length">读取元素数。</param>
    /// <returns>已完成的离线响应任务。</returns>
    public override Task<OResult<byte[]>> ReadAsync(string address, ushort length) => Task.FromResult(Read(address, length));
}

/// <summary>替代 Modbus 通信读取，ReadBool 及其字节序判断仍由生产 Driver 执行；固定两个字节故意使用不同位模式。</summary>
internal sealed class AuditModbusDevice : ModbusTcpNet
{
    /// <summary>返回高字节最低位置位、低字节最高位置位的寄存器，以区分是否交换字节。</summary>
    /// <param name="address">原生位读取传入的无点号寄存器地址。</param>
    /// <param name="length">请求寄存器数量。</param>
    /// <returns>长度匹配原生字读取的固定响应。</returns>
    public override OResult<byte[]> Read(string address, ushort length) =>
        OResult.CreateSuccessResult(Enumerable.Range(0, length * 2).Select(i => (byte)(i % 2 == 0 ? 1 : 128)).ToArray());

    /// <summary>为生产异步位读取提供相同寄存器响应，不创建外部连接。</summary>
    /// <param name="address">寄存器地址。</param>
    /// <param name="length">请求寄存器数量。</param>
    /// <returns>已完成的离线响应任务。</returns>
    public override Task<OResult<byte[]>> ReadAsync(string address, ushort length) => Task.FromResult(Read(address, length));
}
