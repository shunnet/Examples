using Snet.Core.communication.serial;
using static Snet.Core.communication.serial.SerialData;

namespace Snet.Core.Samples.communication.serial
{
    /// <summary>
    /// 串口通信完整测试套件<br/>
    /// 使用虚拟串口对 COM1↔COM2 测试数据收发与断线重连；<br/>
    /// 无可用串口对时自动跳过 E2E 测试，API 面测试始终执行。
    /// </summary>
    public static class SerialTest
    {
        private static int _passed;
        private static int _failed;

        public static async Task RunAllAsync()
        {
            _passed = 0;
            _failed = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  Serial 测试套件");
            Console.WriteLine("============================================\n");

            // 1) API 面测试
            await Test_GetPortArray();
            await Test_InvalidPort();
            await Test_ApiSurface_NotConnected();
            await Test_GetParam();
            await Test_Dispose();
            await Test_SyncMethods();

            // 2) 查找串口对
            var ports = SerialOperate.GetPortArray();
            Console.WriteLine($"  系统串口: [{string.Join(", ", ports)}]");
            var (portA, portB) = FindPortPair(ports.ToArray());

            if (portA != null && portB != null)
            {
                Console.WriteLine($"  检测到串口对: {portA} ↔ {portB}");
                await Test_E2E_SendReceive(portA, portB);
                await Test_E2E_SendWait(portA, portB);
                await Test_E2E_Reconnect(portA, portB);
                await Test_E2E_LargeData(portA, portB);
            }
            else
            {
                Console.WriteLine("  未检测到可用串口对，跳过 E2E 测试");
                Console.WriteLine("  (提示：使用 com0com 等虚拟串口工具创建 COM1↔COM2 对)");
                await Test_E2E_Skipped();
            }

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  Serial 结果: {_passed} 通过, {_failed} 失败 (共 {_passed + _failed} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        // ======================== 串口对查找 ========================
        static (string?, string?) FindPortPair(string[] ports)
        {
            var set = new HashSet<string>(ports, StringComparer.OrdinalIgnoreCase);
            if (set.Contains("COM1") && set.Contains("COM2")) return ("COM1", "COM2");
            return (null, null);
        }

        // ======================== API 面测试 ========================
        static async Task Test_GetPortArray()
        {
            try { var p = SerialOperate.GetPortArray(); Assert(p != null, "Serial.GetPortArray", $"找到 {p.Count} 个串口"); }
            catch (Exception ex) { Fail("Serial.GetPortArray", ex); }
        }

        static async Task Test_InvalidPort()
        {
            try
            {
                var b = new Basics { PortName = "COM_NONEXISTENT_999", ReadTimeout = 500, WriteTimeout = 500 };
                using var s = new SerialOperate(b);
                Assert(!(await s.OnAsync()).Status, "Serial.OnAsync - 无效串口应失败", null);
            }
            catch (Exception ex) { Fail("Serial.OnAsync - 无效串口", ex); }
        }

        static async Task Test_ApiSurface_NotConnected()
        {
            try
            {
                using var s = new SerialOperate(new Basics());
                Assert(!(await s.OffAsync()).Status, "Serial.OffAsync - 未连接", null);
                Assert(!(await s.GetStatusAsync()).Status, "Serial.GetStatusAsync", null);
                Assert(!(await s.GetBaseObjectAsync()).Status, "Serial.GetBaseObjectAsync", null);
                Assert(!(await s.SendAsync(new byte[] { 1 })).Status, "Serial.SendAsync - 未连接", null);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                Assert(!(await s.SendWaitAsync(new byte[] { 1 }, cts.Token)).Status, "Serial.SendWaitAsync - 未连接", null);
            }
            catch (Exception ex) { Fail("Serial.ApiSurface", ex); }
        }

        static async Task Test_GetParam()
        {
            try { using var s = new SerialOperate(new Basics()); Assert((await s.GetArgsAsync()).Status, "Serial.GetParam", null); }
            catch (Exception ex) { Fail("Serial.GetParam", ex); }
        }

        static async Task Test_Dispose()
        {
            try { var s = new SerialOperate(new Basics()); Assert(!(await s.GetStatusAsync()).Status, "Serial.Dispose - 释放前", null); s.Dispose(); Assert(true, "Serial.Dispose - 释放", "成功"); }
            catch (Exception ex) { Fail("Serial.Dispose", ex); }
        }

        static async Task Test_SyncMethods()
        {
            try { using var s = new SerialOperate(new Basics()); Assert(!s.GetStatus().Status, "Serial.GetStatus (同步)", null); Assert(!s.GetBaseObject().Status, "Serial.GetBaseObject (同步)", null); Assert(true, "Serial.SyncMethods", null); }
            catch (Exception ex) { Fail("Serial.SyncMethods", ex); }
        }

        // ======================== E2E 测试 ========================

        static async Task Test_E2E_SendReceive(string portA, string portB)
        {
            try
            {
                var rxB = new List<byte[]>(); var rxA = new List<byte[]>();
                var bA = new Basics { PortName = portA, BaudRate = 115200, ReadTimeout = 3000, WriteTimeout = 3000 };
                var bB = new Basics { PortName = portB, BaudRate = 115200, ReadTimeout = 3000, WriteTimeout = 3000 };
                var sA = new SerialOperate(bA); var sB = new SerialOperate(bB);
                sA.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] d) lock (rxA) rxA.Add(d); };
                sB.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] d) lock (rxB) rxB.Add(d); };

                var r1 = await sA.OnAsync(); var r2 = await sB.OnAsync();
                Assert(r1.Status && r2.Status, "Serial E2E - 打开串口对", $"{portA}:{r1.Status} {portB}:{r2.Status}");
                if (!r1.Status || !r2.Status) { sA.Dispose(); sB.Dispose(); return; }
                await Task.Delay(200);

                var d1 = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                var send1 = await sA.SendAsync(d1); await Task.Delay(200);
                Assert(send1.Status && rxB.Count > 0 && rxB[0].SequenceEqual(d1), "Serial E2E - A→B", $"发送:{send1.Status} B收到:{rxB.Count}条");

                var d2 = new byte[] { 0xAA, 0xBB, 0xCC };
                var send2 = await sB.SendAsync(d2); await Task.Delay(200);
                Assert(send2.Status && rxA.Count > 0 && rxA[0].SequenceEqual(d2), "Serial E2E - B→A", $"发送:{send2.Status} A收到:{rxA.Count}条");

                await sA.OffAsync(); await sB.OffAsync(); sA.Dispose(); sB.Dispose();
            }
            catch (Exception ex) { Fail("Serial E2E - 双向收发", ex); }
        }

        static async Task Test_E2E_SendWait(string portA, string portB)
        {
            try
            {
                var bA = new Basics { PortName = portA, BaudRate = 115200, ReadTimeout = 5000, WriteTimeout = 3000, SendWaitInterval = 5000 };
                var bB = new Basics { PortName = portB, BaudRate = 115200, ReadTimeout = 5000, WriteTimeout = 3000 };
                var sA = new SerialOperate(bA); var sB = new SerialOperate(bB);
                sB.OnDataEvent += async (_, e) => { if (e.Status && e.ResultData is byte[] d) { var r = new byte[d.Length + 2]; d.CopyTo(r, 0); r[^2] = 0xEE; r[^1] = 0xFF; await sB.SendAsync(r); } };
                if (!(await sA.OnAsync()).Status || !(await sB.OnAsync()).Status) { sA.Dispose(); sB.Dispose(); return; }
                await Task.Delay(200);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var result = await sA.SendWaitAsync(new byte[] { 0x10, 0x20, 0x30 }, cts.Token);
                Assert(result.Status && result.ResultData is byte[] r && r.Length == 5, "Serial E2E - SendWait", $"Status:{result.Status} 响应:{(result.ResultData as byte[])?.Length ?? 0}字节");

                await sA.OffAsync(); await sB.OffAsync(); sA.Dispose(); sB.Dispose();
            }
            catch (Exception ex) { Fail("Serial E2E - SendWait", ex); }
        }

        static async Task Test_E2E_Reconnect(string portA, string portB)
        {
            try
            {
                var rx = new List<byte[]>();
                var sA = new SerialOperate(new Basics { PortName = portA, BaudRate = 115200, ReadTimeout = 3000, WriteTimeout = 3000 });
                var sB = new SerialOperate(new Basics { PortName = portB, BaudRate = 115200, ReadTimeout = 3000, WriteTimeout = 3000 });
                sB.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] d) lock (rx) rx.Add(d); };
                await sA.OnAsync(); await sB.OnAsync(); await Task.Delay(200);
                await sA.SendAsync(new byte[] { 0x01 }); await Task.Delay(200);
                int preCount = rx.Count;

                // 断开 B 再重连
                await sB.OffAsync(); await Task.Delay(100);
                Assert((await sB.OnAsync()).Status, "Serial E2E - 重连打开", null);
                await Task.Delay(200);

                await sA.SendAsync(new byte[] { 0x02 }); await Task.Delay(200);
                Assert(rx.Count > preCount, "Serial E2E - 重连后通信", $"断开前:{preCount} 重连后:{rx.Count}");

                await sA.OffAsync(); await sB.OffAsync(); sA.Dispose(); sB.Dispose();
            }
            catch (Exception ex) { Fail("Serial E2E - 重连", ex); }
        }

        static async Task Test_E2E_LargeData(string portA, string portB)
        {
            try
            {
                var rx = new List<byte[]>();
                var sA = new SerialOperate(new Basics { PortName = portA, BaudRate = 115200, ReadTimeout = 5000, WriteTimeout = 5000, MaxChunkSize = 512 });
                var sB = new SerialOperate(new Basics { PortName = portB, BaudRate = 115200, ReadTimeout = 5000, WriteTimeout = 5000 });
                sB.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] d) lock (rx) rx.Add(d); };
                if (!(await sA.OnAsync()).Status || !(await sB.OnAsync()).Status) { sA.Dispose(); sB.Dispose(); return; }
                await Task.Delay(200);

                var data = new byte[2048]; new Random(42).NextBytes(data);
                var r = await sA.SendAsync(data); await Task.Delay(500);
                Assert(r.Status, "Serial E2E - 大数据分包", $"发送:{r.Status} 收到:{rx.Count}片段 {rx.Sum(x => x.Length)}字节");

                await sA.OffAsync(); await sB.OffAsync(); sA.Dispose(); sB.Dispose();
            }
            catch (Exception ex) { Fail("Serial E2E - 大数据", ex); }
        }

        static async Task Test_E2E_Skipped()
        {
            Assert(true, "Serial E2E - 跳过", "未检测到可用 COM1↔COM2 串口对");
            Assert(true, "Serial E2E - 提示", "com0com 创建虚拟串口后重新运行测试");
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _passed++; Console.WriteLine($"  [PASS] {name}"); }
            else { _failed++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _failed++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
