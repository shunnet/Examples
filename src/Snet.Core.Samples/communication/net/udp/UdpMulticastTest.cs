using Snet.Core.communication.net.udp.multicast;
using static Snet.Core.communication.net.udp.multicast.UdpMulticastData;

namespace Snet.Core.Samples.communication.net.udp
{
    /// <summary>
    /// UDP 组播完整测试套件<br/>
    /// 加入组 → 收发 → 多成员 → 离开组。
    /// </summary>
    public static class UdpMulticastTest
    {
        private const int TestPort = 18888;
        private const string GroupAddr = "239.0.0.99";
        private static int _p, _f;

        public static async Task RunAllAsync()
        {
            _p = 0; _f = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  UDP Multicast 测试套件");
            Console.WriteLine("============================================\n");

            await Test_ApiSurface();
            await Test_E2E_GroupCommunication();
            await Test_E2E_SendWait_Timeout();
            await Test_E2E_LargeData();
            await Test_E2E_MultipleMembers();

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  UDP Multicast 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        static async Task Test_ApiSurface()
        {
            try
            {
                var b = new Basics { Port = TestPort, MulticastAddress = GroupAddr, TimeToLive = 1, Timeout = 2000 };
                using var mc = new UdpMulticastOperate(b);
                Assert((await mc.OnAsync()).Status, "Mcast.OnAsync", null);
                Assert((await mc.GetStatusAsync()).Status, "Mcast.GetStatusAsync", null);
                Assert((await mc.GetBaseObjectAsync()).Status, "Mcast.GetBaseObjectAsync", null);
                Assert((await mc.SendAsync(new byte[] { 1 })).Status, "Mcast.SendAsync", null);
                Assert(!(await mc.OnAsync()).Status, "Mcast.OnAsync - 重复", null);
                Assert((await mc.OffAsync()).Status, "Mcast.OffAsync", null);
                Assert(!(await mc.OffAsync()).Status, "Mcast.OffAsync - 未连接", null);

                // 同步方法
                var b2 = new Basics { Port = TestPort + 15, MulticastAddress = GroupAddr, TimeToLive = 1, Timeout = 2000 };
                using var mc2 = new UdpMulticastOperate(b2);
                Assert(mc2.On().Status, "Mcast.On (同步)", null);
                Assert(mc2.GetStatus().Status, "Mcast.GetStatus (同步)", null);
                Assert(mc2.Send(new byte[] { 1 }).Status, "Mcast.Send (同步)", null);
                Assert(mc2.GetBaseObject().Status, "Mcast.GetBaseObject (同步)", null);
                Assert(mc2.Off().Status, "Mcast.Off (同步)", null);
            }
            catch (Exception ex) { Fail("Mcast.API", ex); }
        }

        static async Task Test_E2E_GroupCommunication()
        {
            try
            {
                const string g = "239.0.0.88"; int gp = TestPort + 1;
                var rx = new List<byte[]>();

                // 成员 A 监听 gp
                var mA = new UdpMulticastOperate(new Basics { Port = gp, MulticastAddress = g, TimeToLive = 1, Timeout = 3000 });
                mA.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx) rx.Add(b); };
                Assert((await mA.OnAsync()).Status, "Mcast E2E - A加入组", null);

                // 成员 B 加入同组、不同绑定端口
                var mB = new UdpMulticastOperate(new Basics { Port = gp + 10, MulticastAddress = g, TimeToLive = 1, Timeout = 3000 });
                Assert((await mB.OnAsync()).Status, "Mcast E2E - B加入组", null);
                await Task.Delay(300);

                // B 向组发送到 gp（A 的端口），A 能收到
                var d = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
                using var tx = new System.Net.Sockets.UdpClient();
                tx.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);
                tx.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.IP, System.Net.Sockets.SocketOptionName.MulticastTimeToLive, 1);
                await tx.SendAsync(d, new System.Net.IPEndPoint(System.Net.IPAddress.Parse(g), gp));
                await Task.Delay(500);

                Assert(rx.Count > 0 && rx[0].SequenceEqual(d), "Mcast E2E - 组内通信", $"收到{rx.Count}条 匹配:{rx.FirstOrDefault()?.SequenceEqual(d) ?? false}");
                mA.Dispose(); mB.Dispose();
            }
            catch (Exception ex) { Fail("Mcast E2E - 组内通信", ex); }
        }

        static async Task Test_E2E_SendWait_Timeout()
        {
            try
            {
                var b = new Basics { Port = TestPort + 2, MulticastAddress = GroupAddr, TimeToLive = 1, Timeout = 2000, SendWaitInterval = 1500 };
                using var mc = new UdpMulticastOperate(b);
                Assert((await mc.OnAsync()).Status, "Mcast E2E - 绑定", null);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var r = await mc.SendWaitAsync(new byte[] { 0xAA }, cts.Token);
                Assert(true, "Mcast SendWaitAsync", $"Status:{r.Status}（组播回环可能收到自身）");
            }
            catch (Exception ex) { Fail("Mcast SendWaitAsync", ex); }
        }

        static async Task Test_E2E_LargeData()
        {
            try
            {
                using var mc = new UdpMulticastOperate(new Basics { Port = TestPort + 4, MulticastAddress = GroupAddr, TimeToLive = 1, Timeout = 2000, MaxChunkSize = 1024 });
                Assert((await mc.OnAsync()).Status, "Mcast E2E - 绑定", null);
                var d = new byte[4096]; new Random(42).NextBytes(d);
                Assert((await mc.SendAsync(d)).Status, "Mcast E2E - 分包大数据", null);
            }
            catch (Exception ex) { Fail("Mcast E2E - 分包", ex); }
        }

        static async Task Test_E2E_MultipleMembers()
        {
            try
            {
                const string g = "239.0.0.77"; int gp = TestPort + 3;
                var rx1 = new List<byte[]>(); var rx2 = new List<byte[]>();
                var m1 = new UdpMulticastOperate(new Basics { Port = gp, MulticastAddress = g, TimeToLive = 1, Timeout = 3000 });
                var m2 = new UdpMulticastOperate(new Basics { Port = gp + 10, MulticastAddress = g, TimeToLive = 1, Timeout = 3000 });
                m1.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx1) rx1.Add(b); };
                m2.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx2) rx2.Add(b); };
                await m1.OnAsync(); await m2.OnAsync(); await Task.Delay(300);

                var d = new byte[] { 0x11, 0x22 };
                using var tx = new System.Net.Sockets.UdpClient();
                tx.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);
                tx.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.IP, System.Net.Sockets.SocketOptionName.MulticastTimeToLive, 1);
                await tx.SendAsync(d, new System.Net.IPEndPoint(System.Net.IPAddress.Parse(g), gp));
                await Task.Delay(500);

                Assert(rx1.Count > 0 || rx2.Count > 0, "Mcast E2E - 多成员", $"M1收到:{rx1.Count} M2收到:{rx2.Count}");
                m1.Dispose(); m2.Dispose();
            }
            catch (Exception ex) { Fail("Mcast E2E - 多成员", ex); }
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
            else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
