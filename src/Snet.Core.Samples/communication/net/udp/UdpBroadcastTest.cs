using Snet.Core.communication.net.udp.broadcast;
using static Snet.Core.communication.net.udp.broadcast.UdpBroadcastData;

namespace Snet.Core.Samples.communication.net.udp
{
    /// <summary>
    /// UDP 广播完整测试套件<br/>
    /// 绑定 → 收发 → 分包大数据 → 多接收方。
    /// </summary>
    public static class UdpBroadcastTest
    {
        private const int TestPort = 17777;
        private static int _p, _f;

        public static async Task RunAllAsync()
        {
            _p = 0; _f = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  UDP Broadcast 测试套件");
            Console.WriteLine("============================================\n");

            await Test_ApiSurface();
            await Test_E2E_SendReceive();
            await Test_E2E_SendWait_Timeout();
            await Test_E2E_LargeData();
            await Test_E2E_MultipleReceivers();

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  UDP Broadcast 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        static async Task Test_ApiSurface()
        {
            try
            {
                using var bc = new UdpBroadcastOperate(new Basics { Port = TestPort, Timeout = 2000 });
                Assert((await bc.OnAsync()).Status, "Bcast.OnAsync", null);
                Assert((await bc.GetStatusAsync()).Status, "Bcast.GetStatusAsync", null);
                Assert((await bc.GetBaseObjectAsync()).Status, "Bcast.GetBaseObjectAsync", null);
                Assert((await bc.SendAsync(new byte[] { 1, 2 })).Status, "Bcast.SendAsync", null);
                Assert(!(await bc.OnAsync()).Status, "Bcast.OnAsync - 重复", null);
                Assert((await bc.OffAsync()).Status, "Bcast.OffAsync", null);
                Assert(!(await bc.OffAsync()).Status, "Bcast.OffAsync - 未启动", null);

                // 同步方法
                using var bc2 = new UdpBroadcastOperate(new Basics { Port = TestPort + 15, Timeout = 2000 });
                Assert(bc2.On().Status, "Bcast.On (同步)", null);
                Assert(bc2.GetStatus().Status, "Bcast.GetStatus (同步)", null);
                Assert(bc2.Send(new byte[] { 1 }).Status, "Bcast.Send (同步)", null);
                Assert(bc2.Off().Status, "Bcast.Off (同步)", null);
            }
            catch (Exception ex) { Fail("Bcast.API", ex); }
        }

        static async Task Test_E2E_SendReceive()
        {
            try
            {
                var rx = new List<byte[]>();
                int rxPort = TestPort + 1;

                // 接收方绑定 rxPort
                var rxBc = new UdpBroadcastOperate(new Basics { Port = rxPort, Timeout = 3000 });
                rxBc.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx) rx.Add(b); };
                Assert((await rxBc.OnAsync()).Status, "Bcast E2E - Rx绑定", null);

                // 用裸 UdpClient 发送广播数据到 rxPort
                var d = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
                using var tx = new System.Net.Sockets.UdpClient();
                tx.EnableBroadcast = true;
                await tx.SendAsync(d, new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, rxPort));
                await Task.Delay(500);

                Assert(rx.Count > 0 && rx[0].SequenceEqual(d), "Bcast E2E - 收发", $"收到{rx.Count}条 匹配:{rx.FirstOrDefault()?.SequenceEqual(d) ?? false}");
                rxBc.Dispose();
            }
            catch (Exception ex) { Fail("Bcast E2E - 收发", ex); }
        }

        static async Task Test_E2E_SendWait_Timeout()
        {
            try
            {
                using var bc = new UdpBroadcastOperate(new Basics { Port = TestPort + 2, Timeout = 2000, SendWaitInterval = 1000 });
                Assert((await bc.OnAsync()).Status, "Bcast E2E - 绑定", null);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                Assert(!(await bc.SendWaitAsync(new byte[] { 0xAA }, cts.Token)).Status, "Bcast SendWaitAsync - 超时", null);
            }
            catch (Exception ex) { Fail("Bcast SendWaitAsync - 超时", ex); }
        }

        static async Task Test_E2E_LargeData()
        {
            try
            {
                using var bc = new UdpBroadcastOperate(new Basics { Port = TestPort + 3, Timeout = 2000, MaxChunkSize = 1024 });
                Assert((await bc.OnAsync()).Status, "Bcast E2E - 绑定", null);
                var d = new byte[4096]; new Random(42).NextBytes(d);
                Assert((await bc.SendAsync(d)).Status, "Bcast E2E - 分包大数据", null);
            }
            catch (Exception ex) { Fail("Bcast E2E - 分包", ex); }
        }

        static async Task Test_E2E_MultipleReceivers()
        {
            try
            {
                int port = TestPort + 4;
                var rx1 = new List<byte[]>(); var rx2 = new List<byte[]>();
                var r1 = new UdpBroadcastOperate(new Basics { Port = port, Timeout = 3000 });
                var r2 = new UdpBroadcastOperate(new Basics { Port = port + 10, Timeout = 3000 }); // 不同端口监听
                r1.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx1) rx1.Add(b); };
                r2.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx2) rx2.Add(b); };
                await r1.OnAsync(); await r2.OnAsync();
                await Task.Delay(200);

                // 向 port 发送广播，只有 r1 收到（port 匹配）
                var d = new byte[] { 0xBC, 0xAD };
                using var tx = new System.Net.Sockets.UdpClient(); tx.EnableBroadcast = true;
                await tx.SendAsync(d, new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, port));
                await Task.Delay(500);

                Assert(rx1.Count > 0, "Bcast E2E - 多接收方", $"Rx1:{rx1.Count} Rx2:{rx2.Count}");
                r1.Dispose(); r2.Dispose();
            }
            catch (Exception ex) { Fail("Bcast E2E - 多接收方", ex); }
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
            else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
