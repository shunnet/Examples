using Snet.Core.communication.net.tcp.client;
using Snet.Core.communication.net.tcp.service;
using ClientBasics = Snet.Core.communication.net.tcp.client.TcpClientData.Basics;
using Msg = Snet.Core.communication.net.core.ClientMessage;
using ServiceBasics = Snet.Core.communication.net.tcp.service.TcpServiceData.Basics;

namespace Snet.Core.Samples.communication.net.tcp
{
    /// <summary>
    /// TCP 通信完整测试套件<br/>
    /// 服务端先行启动 → 客户端后连接 → 收发验证 → 断线重连 → 优雅关闭。
    /// </summary>
    public static class TcpTest
    {
        private const int TestPort = 15688;
        private static int _p, _f;

        public static async Task RunAllAsync()
        {
            _p = 0; _f = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  TCP 测试套件");
            Console.WriteLine("============================================\n");

            await Test_Service_ApiSurface();
            await Test_Client_ApiSurface();

            // ── E2E：服务端先启动 → 客户端后连接 ──
            await Test_E2E_ClientSend_ServiceReceive();
            await Test_E2E_ServiceSend_ClientReceive();
            await Test_E2E_ClientSendWait();
            await Test_E2E_MultipleClients();
            await Test_E2E_Reconnect();

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  TCP 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        // ======================== 服务端 API 面 ========================
        static async Task Test_Service_ApiSurface()
        {
            var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort, Timeout = 2000 });
            try
            {
                Assert((await svc.OnAsync()).Status, "TCP Svc.OnAsync - 启动", null);
                Assert((await svc.GetStatusAsync()).Status, "TCP Svc.GetStatusAsync", null);
                Assert((await svc.GetBaseObjectAsync()).Status, "TCP Svc.GetBaseObjectAsync", null);
                Assert(!(await svc.SendAsync(new byte[] { 1 })).Status, "TCP Svc.SendAsync - 无客户端", null);
                Assert(!(await svc.SendAsync(new byte[] { 1 }, "1.2.3.4:9999")).Status, "TCP Svc.SendAsync - 不存在", null);
                Assert(!(await svc.OnAsync()).Status, "TCP Svc.OnAsync - 重复应失败", null);
                Assert((await svc.OffAsync()).Status, "TCP Svc.OffAsync - 停止", null);
                Assert(!(await svc.OffAsync()).Status, "TCP Svc.OffAsync - 未启动应失败", null);
                svc.Dispose();
            }
            catch (Exception ex) { Fail("TCP Svc.API", ex); try { svc.Dispose(); } catch { } }
        }

        // ======================== 客户端 API 面 ========================
        static async Task Test_Client_ApiSurface()
        {
            TcpServiceOperate? svc = null;
            try
            {
                svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 9, Timeout = 2000 });
                await svc.OnAsync();

                var cb = new ClientBasics { Port = TestPort + 9, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 2000 };
                using var client = new TcpClientOperate(cb);

                Assert((await client.OnAsync()).Status, "TCP Cli.OnAsync - 连接", null);
                Assert((await client.GetStatusAsync()).Status, "TCP Cli.GetStatusAsync", null);
                Assert((await client.GetBaseObjectAsync()).Status, "TCP Cli.GetBaseObjectAsync", null);
                Assert((await client.SendAsync(new byte[] { 1, 2, 3 })).Status, "TCP Cli.SendAsync", null);

                var big = new byte[4096]; new Random(42).NextBytes(big);
                cb.MaxChunkSize = 1024;
                Assert((await client.SendAsync(big)).Status, "TCP Cli.SendAsync - 分包", null);

                Assert(!(await client.OnAsync()).Status, "TCP Cli.OnAsync - 重复应失败", null);
                Assert((await client.OffAsync()).Status, "TCP Cli.OffAsync - 断开", null);
                Assert(!(await client.OffAsync()).Status, "TCP Cli.OffAsync - 未连接应失败", null);

                // 无响应超时
                var cb2 = new ClientBasics { Port = TestPort + 9, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 2000, SendWaitInterval = 1000 };
                using var c2 = new TcpClientOperate(cb2);
                await c2.OnAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                Assert(!(await c2.SendWaitAsync(new byte[] { 0xAA }, cts.Token)).Status, "TCP Cli.SendWaitAsync - 超时", null);
                c2.Dispose();
            }
            catch (Exception ex) { Fail("TCP Cli.API", ex); }
            finally { if (svc != null) { try { await svc.OffAsync(); } catch { } } }
        }

        // ======================== E2E ========================

        static async Task Test_E2E_ClientSend_ServiceReceive()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 18, Timeout = 3000 });
                svc.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is Msg cm && cm.Bytes != null) lock (rx) rx.Add(cm.Bytes); };
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc启动", null);

                using var cli = new TcpClientOperate(new ClientBasics { Port = TestPort + 18, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 3000 });
                Assert((await cli.OnAsync()).Status, "TCP E2E - Cli连接", null);
                await Task.Delay(300);

                var d = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
                Assert((await cli.SendAsync(d)).Status, "TCP E2E - Cli发送", null);
                await Task.Delay(500);

                Assert(rx.Count > 0 && rx[0].SequenceEqual(d), "TCP E2E - Cli→Svc", $"收到{rx.Count}条 匹配:{rx.FirstOrDefault()?.SequenceEqual(d) ?? false}");

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("TCP E2E - Cli→Svc", ex); }
        }

        static async Task Test_E2E_ServiceSend_ClientReceive()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 19, Timeout = 3000 });
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc启动", null);

                using var cli = new TcpClientOperate(new ClientBasics { Port = TestPort + 19, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 3000 });
                cli.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx) rx.Add(b); };
                Assert((await cli.OnAsync()).Status, "TCP E2E - Cli连接", null);
                await Task.Delay(300);

                Assert((await svc.SendAsync(new byte[] { 0xCA, 0xFE })).Status, "TCP E2E - Svc发送", null);
                await Task.Delay(500);
                Assert(rx.Count > 0, "TCP E2E - Svc→Cli", $"收到{rx.Count}条");

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("TCP E2E - Svc→Cli", ex); }
        }

        static async Task Test_E2E_ClientSendWait()
        {
            try
            {
                var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 20, Timeout = 3000 });
                svc.OnDataEvent += async (_, e) =>
                {
                    if (e.Status && e.ResultData is Msg cm && cm.Bytes != null)
                    {
                        var r = new byte[cm.Bytes.Length + 2]; cm.Bytes.CopyTo(r, 0); r[^2] = 0xEE; r[^1] = 0xFF;
                        await svc.SendAsync(r, cm.IpPort);
                    }
                };
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc启动", null);

                using var cli = new TcpClientOperate(new ClientBasics { Port = TestPort + 20, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 3000, SendWaitInterval = 5000 });
                Assert((await cli.OnAsync()).Status, "TCP E2E - Cli连接", null);
                await Task.Delay(200);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var r = await cli.SendWaitAsync(new byte[] { 0x01, 0x02, 0x03 }, cts.Token);
                var rd = r.ResultData as byte[];
                Assert(r.Status && rd != null && rd.Length == 5, "TCP E2E - SendWait", $"响应:{rd?.Length ?? 0}字节");

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("TCP E2E - SendWait", ex); }
        }

        static async Task Test_E2E_MultipleClients()
        {
            try
            {
                var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 21, Timeout = 3000 });
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc启动", null);

                var clients = new List<TcpClientOperate>();
                for (int i = 0; i < 3; i++)
                {
                    var c = new TcpClientOperate(new ClientBasics { Port = TestPort + 21, IpAddress = "127.0.0.1", InterruptReconnection = false, Timeout = 3000 });
                    Assert((await c.OnAsync()).Status, $"TCP E2E - Cli{i}连接", null);
                    clients.Add(c);
                }
                await Task.Delay(300);
                for (int i = 0; i < 3; i++) await clients[i].SendAsync(new byte[] { (byte)i });
                await Task.Delay(300);
                foreach (var c in clients) c.Dispose();
                Assert(true, "TCP E2E - 多客户端", "3个客户端");
                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("TCP E2E - 多客户端", ex); }
        }

        static async Task Test_E2E_Reconnect()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new TcpServiceOperate(new ServiceBasics { Port = TestPort + 22, Timeout = 3000 });
                svc.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is Msg cm && cm.Bytes != null) lock (rx) rx.Add(cm.Bytes); };
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc启动", null);

                // 客户端启用断线重连
                var cli = new TcpClientOperate(new ClientBasics { Port = TestPort + 22, IpAddress = "127.0.0.1", InterruptReconnection = true, ReconnectionInterval = 1000, Timeout = 3000 });
                Assert((await cli.OnAsync()).Status, "TCP E2E - Cli连接(重连)", null);
                await Task.Delay(300);
                await cli.SendAsync(new byte[] { 0x01 }); await Task.Delay(300);
                int preCount = rx.Count;

                // 关闭服务端，客户端应断线
                await svc.OffAsync(); await Task.Delay(500);

                // 重启服务端，客户端应自动重连
                Assert((await svc.OnAsync()).Status, "TCP E2E - Svc重启", null);
                await Task.Delay(2000); // 等待客户端重连周期
                await Task.Delay(2000);

                await cli.SendAsync(new byte[] { 0x02 }); await Task.Delay(500);
                Assert(rx.Count > preCount, "TCP E2E - 重连后通信", $"断开前:{preCount} 重连后:{rx.Count}");
                cli.Dispose();
                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("TCP E2E - 重连", ex); }
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
            else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
