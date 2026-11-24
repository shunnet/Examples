using Snet.Core.handler;
using Snet.Core.packer;
using Snet.Driver.Profinet.LSIS;
using Snet.Driver.Profinet.Siemens;
using Snet.Model.data;
using Snet.Model.@enum;
using System.Collections.Concurrent;
using System.Net;
using TcpListener = System.Net.Sockets.TcpListener;

namespace Snet.Core.Samples;

/// <summary>
/// 2026年11月2日全量审查回归：以进程内回环服务端对照 Driver 的真实单点读取，
/// 并固化本轮修复的离线判定（大小写去重、批首大写、Yaskawa 地址语法、LS Cnet Bool 写法、FastEnet 位宽、S7 T/C/AI/AQ）。
/// </summary>
internal static class PackerLoopbackRegression
{
    /// <summary>运行本轮回归，沿用样例入口的断言计数和非零失败退出码。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    internal static void Run(Action<bool, string> assert)
    {
        Console.WriteLine("--- 2026-11-02 回环对照与地址写法回归 ---");
        VerifyCaseSensitiveDedupe(assert);
        VerifyUppercaseHeads(assert);
        VerifyYaskawaGrammar(assert);
        VerifyCnetBoolForms(assert);
        VerifyFastEnetLoopback(assert);
        VerifyS7Loopback(assert);
    }

    /// <summary>点名区分大小写：解包字典以点名为键，仅大小写不同的两个点必须各自保留并各自解出。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyCaseSensitiveDedupe(Action<bool, string> assert)
    {
        var batch = Pack(ProtocolFamily.Mitsubishi, new AddressDetails("D168", DataType.Short), new AddressDetails("d168", DataType.Short)).Single();
        var models = batch.AddressExtendParam as List<BytesModel> ?? new List<BytesModel>();
        assert(IsBatch(batch) && models.Select(m => m.Address).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(new[] { "D168", "d168" }),
            "仅大小写不同的 D168/d168 在批内各保留一个解包模型，不被去重吞掉");
        var decoded = new BytesHandler().Transform(new byte[] { 0x12, 0x34 }, DateTime.Now, models);
        var values = decoded.ResultData as ConcurrentDictionary<string, AddressValue>;
        assert(decoded.Status && values?.Count == 2 && values.Values.Select(v => v.AddressName).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(new[] { "D168", "d168" }),
            "D168/d168 两个点都能从同一批响应解出，结果字典不丢点");
    }

    /// <summary>Panasonic/Fanuc/Yokogawa 的 Driver 只认全大写或全小写的双字母设备码，批首必须规范为大写，模型点名保留用户输入。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyUppercaseHeads(Action<bool, string> assert)
    {
        foreach (var c in new (ProtocolFamily Family, string Address, string Head)[]
        {
            (ProtocolFamily.Panasonic, "Ld44", "LD44"), (ProtocolFamily.Panasonic, "lD44", "LD44"),
            (ProtocolFamily.Fanuc, "aI100", "AI100"), (ProtocolFamily.Fanuc, "Go100", "GO100"),
            (ProtocolFamily.Yokogawa, "Tn100", "TN100"), (ProtocolFamily.Yokogawa, "cN5", "CN5")
        })
        {
            var batch = Pack(c.Family, new AddressDetails(c.Address, DataType.Short)).Single();
            var models = batch.AddressExtendParam as List<BytesModel>;
            assert(IsBatch(batch) && batch.AddressName == c.Head && models?.Single().Address == c.Address,
                $"{c.Family} {c.Address} 批首规范为 {c.Head}，模型点名保留 {c.Address}");
        }
    }

    /// <summary>Yaskawa 地址语法与 Driver 一致：修饰符只能在数字本体之前且各出现一次，值按十进制或 0x 十六进制，前导 0 是八进制而不能合批。</summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyYaskawaGrammar(Action<bool, string> assert)
    {
        var yaskawa = PackerFactory.GetPacker(ProtocolFamily.Yaskawa)!;
        foreach (string valid in new[] { "100", "x=3;100", "X=4;100", "x=0x03;100", "x=3;mfc=1;100", "mfc=1;x=3;100" })
            assert(yaskawa.ParseAddress(valid).ByteIndex >= 0 && IsBatch(Pack(ProtocolFamily.Yaskawa, new AddressDetails(valid, DataType.Short)).Single()),
                $"Yaskawa {valid} 是 Driver 接受的写法，可以组包");
        foreach (string invalid in new[] { "x=010;100", "x=3;x=4;100", "100;", ";100", "x=3;;100", "100;x=3", "x=;100", "x=256;100", "x=0x;100" })
        {
            var point = new AddressDetails(invalid, DataType.Short);
            assert(yaskawa.ParseAddress(invalid).ByteIndex < 0 && ReferenceEquals(Pack(ProtocolFamily.Yaskawa, point).Single(), point),
                $"Yaskawa {invalid} 不是 Driver 能解析的写法，原样透传");
        }
    }

    /// <summary>
    /// LS Cnet 的 Bool 单点读取走 AnalysisAddress：只有无位宽字母、非 U/I/Q 区的 M100.3 / M100 与批读位置一致；
    /// 显式 B/W/D/L/X 位宽会把地址号再乘一次位宽，I/Q/U 点号写法无法解析成同一位置，这些 Bool 必须透传。
    /// Word 类型走 LsisCnetAddress.ParseFrom：位宽缩放与批读一致，但点号只有 U/I/Q 的原生写法（不带位宽字母）有效——
    /// 其余区的点号使 Convert.ToInt32 抛异常，U 区 UB1.2 会被 Driver 当成库号 0xB1，I/Q 带位宽字母时抛异常，都必须透传。
    /// 回环与地址级对照（LSCnet 环境逐区、逐位宽、含点号与无点号 Bool；ParseFrom 逐语法形式比较）见审查产物的差分工具。
    /// </summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyCnetBoolForms(Action<bool, string> assert)
    {
        foreach (string packable in new[] { "M100.3", "D5.7", "P10.0", "M100" })
            assert(IsBatch(Pack(ProtocolFamily.LSis_Cnet, new AddressDetails(packable, DataType.Bool)).Single()),
                $"LSis_Cnet Bool {packable} 无位宽写法与批读一致，可组包");
        foreach (string passthrough in new[]
                 { "MB100.3", "MW100.3", "MD100.3", "ML100.3", "MX100.3", "MX100", "MB100", "MW100", "Q0.10.0", "Q100", "I0.1.2", "U1.2", "U3.2.5" })
        {
            var point = new AddressDetails(passthrough, DataType.Bool);
            assert(ReferenceEquals(Pack(ProtocolFamily.LSis_Cnet, point).Single(), point),
                $"LSis_Cnet Bool {passthrough} 单点位读与批读落在不同位置，原样透传");
        }
        foreach (string word in new[] { "MB100", "MW100", "MD100", "ML100", "MX100", "M100", "Q0.10.0", "I3.2.10", "U1.2", "U3.2.5" })
            assert(IsBatch(Pack(ProtocolFamily.LSis_Cnet, new AddressDetails(word, DataType.Short)).Single()),
                $"LSis_Cnet Word {word} 走 ParseFrom 位宽缩放，批读与单点一致，继续组包");
        foreach (string word in new[] { "M100.3", "MB100.3", "MW1.2", "D1.2", "UB1.2", "UD1.2", "UW1.2", "QW0.10.1", "IB3.2.5", "U1.2.3.4", "Q0.10" })
        {
            var point = new AddressDetails(word, DataType.Short);
            assert(ReferenceEquals(Pack(ProtocolFamily.LSis_Cnet, point).Single(), point),
                $"LSis_Cnet Word {word} 的点号写法 Driver 字读无法解析成同一坐标（抛异常或库号不同），原样透传");
        }
    }

    /// <summary>
    /// FastEnet 回环：B 位宽是连续块读取，批读与逐点类型读取完全一致；W/D/L 位宽每次只返回一个元素且地址号被再乘位宽，
    /// 不能合批，必须保持单点读取。
    /// </summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyFastEnetLoopback(Action<bool, string> assert)
    {
        int port = FreePort();
        using var server = new LSisServer();
        server.ServerStart(port);
        Thread.Sleep(250);
        var client = new LSFastEnet("127.0.0.1", port) { ReceiveTimeOut = 2000 };
        try
        {
            var connect = client.ConnectServer();
            assert(connect.IsSuccess, "FastEnet 回环客户端连接内置服务端");
            if (!connect.IsSuccess) return;
            var data = Enumerable.Range(0, 2000).Select(i => (byte)(i % 251 + 1)).ToArray();
            assert(client.Write("MB0", data).IsSuccess, "FastEnet 以 B 位宽整块写入回环内存");

            foreach (DataFormat format in new[] { DataFormat.ABCD, DataFormat.BADC, DataFormat.CDAB, DataFormat.DCBA })
            {
                client.ByteTransform.DataFormat = (Snet.Driver.Core.DataFormat)format;
                var points = new[]
                {
                    new AddressDetails("MB100", DataType.Short), new AddressDetails("MB102", DataType.Short),
                    new AddressDetails("MB104", DataType.Int), new AddressDetails("MB108", DataType.Short)
                };
                var batch = PackerFactory.GetPacker(ProtocolFamily.LSis_FastEnet)!.Pack(new Address(points.ToList()), 0, format)!.AddressArray.Single();
                var response = client.Read(batch.AddressName, batch.Length);
                var decoded = new BytesHandler().Transform(response.Content, DateTime.Now, (List<BytesModel>)batch.AddressExtendParam!);
                var values = (ConcurrentDictionary<string, AddressValue>)decoded.ResultData!;
                assert(IsBatch(batch) && batch.AddressName == "MB100" && batch.Length == 10 && response.IsSuccess && decoded.Status,
                    $"FastEnet {format} MB100~MB109 合成一个 MB100 长度 10 的批并读取成功");
                foreach (var point in points)
                {
                    object direct = point.AddressDataType == DataType.Int
                        ? (object)client.ReadInt32(point.AddressName).Content
                        : client.ReadInt16(point.AddressName).Content;
                    assert(Equals(values.Values.Single(v => v.AddressName == point.AddressName).ResultValue, direct),
                        $"FastEnet {format}/{point.AddressName} 组包解码与原生类型读取一致");
                }
            }

            var wide = client.Read("MW100", 16);
            assert(wide.IsSuccess && wide.Content.Length == 2, "FastEnet MW 请求 16 字节只返回一个字（2 字节），不是连续块");
            assert(wide.IsSuccess && wide.Content.SequenceEqual(client.Read("MB400", 2).Content), "FastEnet MW100 实际落在 MB400（序号被再乘位宽），不是 n×2 = 200");
            var dword = client.Read("MD50", 16);
            assert(dword.IsSuccess && dword.Content.Length == 4, "FastEnet MD 请求 16 字节只返回一个双字（4 字节）");
            var lword = client.Read("ML25", 16);
            assert(lword.IsSuccess && lword.Content.Length == 8, "FastEnet ML 请求 16 字节只返回一个长字（8 字节）");
            foreach (var point in new[]
            {
                new AddressDetails("MW100", DataType.Short), new AddressDetails("MW101", DataType.Short),
                new AddressDetails("MD50", DataType.Int), new AddressDetails("ML25", DataType.Double)
            })
                assert(ReferenceEquals(Pack(ProtocolFamily.LSis_FastEnet, point).Single(), point), $"FastEnet {point.AddressName} 位宽独立读取，保留单点读取");
        }
        finally { client.ConnectClose(); }
    }

    /// <summary>
    /// S7 回环：用真实命令读取 T/C/AI/AQ 与 M 区，留下 Driver 对多元素响应不可靠的证据——
    /// 定时器 3 个元素按 3 字节误判、≥48 个定时器或 ≥80 个计数器整批失败、AI/AQ 奇数字节抛越界；M 区各长度均正确，继续组包。
    /// Driver 修复上述问题后，这里的断言会翻转，届时应同步放开 SiemensPacker 对 T/C/AI/AQ 的限制。
    /// </summary>
    /// <param name="assert">接收断言结果及中文说明的回调。</param>
    private static void VerifyS7Loopback(Action<bool, string> assert)
    {
        int port = FreePort();
        using var server = new SiemensS7Server();
        server.ServerStart(port);
        Thread.Sleep(250);
        var client = new SiemensS7Net(SiemensPLCS.S1200, "127.0.0.1") { Port = port, ReceiveTimeOut = 2000 };
        try
        {
            var connect = client.ConnectServer();
            assert(connect.IsSuccess, "S7 回环客户端连接内置服务端");
            if (!connect.IsSuccess) return;
            var data = new byte[1200];
            new Random(3).NextBytes(data);
            foreach (string area in new[] { "T", "C", "AI", "AQ", "M" })
                assert(server.Write(area + "0", data).IsSuccess, $"S7 回环内存 {area} 区写入测试数据");

            // T/C 的响应按 Driver 的元素膨胀返回，长度大于请求字节数，因此以前缀比较；M/AI/AQ 的响应长度与请求相同
            bool Exact(string address, int bytes)
            {
                var r = client.Read(address, (ushort)bytes);
                return r.IsSuccess && r.Content.Length >= bytes && r.Content.Take(bytes).SequenceEqual(data[..bytes]);
            }
            bool Failed(string address, int bytes) => !client.Read(address, (ushort)bytes).IsSuccess;

            assert(Exact("T0", 2) && Exact("T0", 4) && Exact("C0", 6) && Exact("AI0", 4) && Exact("AQ0", 4), "S7 T/C/AI/AQ 单点或少量偶数字节读取正确");
            assert(!Exact("T0", 6), "S7 3 个定时器（6 字节）被 Driver 按 3 字节元素误解析，返回数据与内存不符");
            assert(Failed("T0", 96), "S7 48 个定时器（96 字节）响应超出 PDU，整批读取失败");
            assert(Failed("C0", 160), "S7 80 个计数器（160 字节）响应超出 PDU，整批读取失败");
            assert(Failed("AI0", 3) && Failed("AQ0", 3), "S7 AI/AQ 奇数字节请求被 Driver 按字数取整后越界，读取失败");
            assert(Exact("M0", 7) && Exact("M0", 121) && Exact("M0", 240), "S7 M 字节内存任意长度读取正确，继续组包");

            var points = new[] { new AddressDetails("M100", DataType.Short), new AddressDetails("M102", DataType.Short), new AddressDetails("M104", DataType.Int) };
            var batch = Pack(ProtocolFamily.Siemens, points).Single();
            var response = client.Read(batch.AddressName, batch.Length);
            var decoded = new BytesHandler().Transform(response.Content, DateTime.Now, (List<BytesModel>)batch.AddressExtendParam!);
            var values = (ConcurrentDictionary<string, AddressValue>)decoded.ResultData!;
            assert(IsBatch(batch) && response.IsSuccess && decoded.Status, "S7 M100~M107 合成一个批并读取成功");
            foreach (var point in points)
            {
                object direct = point.AddressDataType == DataType.Int ? (object)client.ReadInt32(point.AddressName).Content : client.ReadInt16(point.AddressName).Content;
                assert(Equals(values.Values.Single(v => v.AddressName == point.AddressName).ResultValue, direct), $"S7 {point.AddressName} 组包解码与原生类型读取一致");
            }
            var timers = new[] { new AddressDetails("T0", DataType.Short), new AddressDetails("T1", DataType.Short), new AddressDetails("T2", DataType.Short) };
            assert(Pack(ProtocolFamily.Siemens, timers).All(p => !IsBatch(p)), "S7 T0~T2 三个相邻定时器不合批，避开 3 字节误判");
        }
        finally { client.ConnectClose(); }
    }

    /// <summary>向系统申请一个当前空闲的回环端口，供内置服务端监听。</summary>
    /// <returns>可用的本机 TCP 端口号。</returns>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
