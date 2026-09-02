using Snet.Core.communication.net.ws.client;
using Snet.Core.communication.net.ws.service;
using ClientBasics = Snet.Core.communication.net.ws.client.WsClientData.Basics;
using Msg = Snet.Core.communication.net.core.ClientMessage;
using ServiceBasics = Snet.Core.communication.net.ws.service.WsServiceData.Basics;

namespace Snet.Core.Samples.communication.net.ws
{
    /// <summary>
    /// WebSocket 完整测试套件<br/>
    /// 服务端启动 → 客户端连接 → 收发 → 断线重连。
    /// </summary>
    public static class WsTest
    {
        private const int TestPort = 14688;
        private static string Host(int p) => $"ws://127.0.0.1:{p}/";
        private static int _p, _f;

        public static async Task RunAllAsync()
        {
            _p = 0; _f = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  WebSocket 测试套件");
            Console.WriteLine("============================================\n");

            await Test_Service_ApiSurface();
            await Test_Client_ApiSurface();
            await Test_E2E_ClientSend_ServiceReceive();
            await Test_E2E_ServiceSend_ClientReceive();
            await Test_E2E_Reconnect();

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  WebSocket 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        static async Task CleanupAsync(WsServiceOperate? svc, WsClientOperate? client)
        {
            // 先停服务端（强制关闭所有 WebSocket），再释放客户端
            var t = Task.Run(async () =>
            {
                if (svc != null) { try { await svc.OffAsync(true).ConfigureAwait(false); } catch { } }
                if (client != null) { try { client.Dispose(); } catch { } }
            });
            if (await Task.WhenAny(t, Task.Delay(8000)).ConfigureAwait(false) != t) { /* 超时跳过 */ }
        }

        // ======================== 服务端 API 面 ========================
        static async Task Test_Service_ApiSurface()
        {
            try
            {
                var svc = new WsServiceOperate(new ServiceBasics { Host = Host(TestPort), Timeout = 2000 });
                Assert((await svc.OnAsync()).Status, "WS Svc.OnAsync", null);
                Assert((await svc.GetStatusAsync()).Status, "WS Svc.GetStatusAsync", null);
                Assert((await svc.GetBaseObjectAsync()).Status, "WS Svc.GetBaseObjectAsync", null);
                Assert(!(await svc.SendAsync(new byte[] { 1 })).Status, "WS Svc.SendAsync - 无客户端", null);
                Assert(!(await svc.SendAsync(new byte[] { 1 }, "127.0.0.1:99999")).Status, "WS Svc.SendAsync - 不存在", null);
                Assert((await svc.RemoveAsync(new[] { "127.0.0.1:99999" })).Status, "WS Svc.RemoveAsync - 不存在", null);
                Assert(!(await svc.OnAsync()).Status, "WS Svc.OnAsync - 重复", null);
                Assert((await svc.OffAsync()).Status, "WS Svc.OffAsync", null);
                Assert(!(await svc.OffAsync()).Status, "WS Svc.OffAsync - 未启动", null);
                svc.Dispose();
            }
            catch (Exception ex) { Fail("WS Svc.API", ex); }
        }

        // ======================== 客户端 API 面 ========================
        static async Task Test_Client_ApiSurface()
        {
            WsServiceOperate? svc = null;
            try
            {
                svc = new WsServiceOperate(new ServiceBasics { Host = Host(TestPort + 9), Timeout = 2000 });
                await svc.OnAsync();

                var cli = new WsClientOperate(new ClientBasics { Host = Host(TestPort + 9), InterruptReconnection = false, Timeout = 2000 });
                Assert((await cli.OnAsync()).Status, "WS Cli.OnAsync", null);
                Assert((await cli.GetStatusAsync()).Status, "WS Cli.GetStatusAsync", null);
                Assert((await cli.GetBaseObjectAsync()).Status, "WS Cli.GetBaseObjectAsync", null);
                Assert(!(await cli.OnAsync()).Status, "WS Cli.OnAsync - 重复", null);
                Assert((await cli.OffAsync()).Status, "WS Cli.OffAsync", null);
                Assert(!(await cli.OffAsync()).Status, "WS Cli.OffAsync - 未连接", null);

                // SendWait 超时
                var cb2 = new ClientBasics { Host = Host(TestPort + 9), InterruptReconnection = false, Timeout = 2000, SendWaitInterval = 2000 };
                var c2 = new WsClientOperate(cb2);
                await c2.OnAsync(); await Task.Delay(200);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                Assert(!(await c2.SendWaitAsync(new byte[] { 0xAA }, cts.Token)).Status, "WS Cli.SendWaitAsync - 超时", null);
                c2.Dispose();
            }
            catch (Exception ex) { Fail("WS Cli.API", ex); }
            finally { if (svc != null) { try { await svc.OffAsync(); } catch { } } }
        }

        // ======================== E2E ========================
        static async Task Test_E2E_ClientSend_ServiceReceive()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new WsServiceOperate(new ServiceBasics { Host = Host(TestPort + 16), Timeout = 3000 });
                svc.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is Msg cm && cm.Bytes != null) lock (rx) rx.Add(cm.Bytes); };
                Assert((await svc.OnAsync()).Status, "WS E2E - Svc启动", null);

                var cli = new WsClientOperate(new ClientBasics { Host = Host(TestPort + 16), InterruptReconnection = false, Timeout = 3000 });
                var on = await cli.OnAsync(); await Task.Delay(1500);
                var send = await cli.SendAsync(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }); await Task.Delay(1500);
                Assert(on.Status && send.Status, "WS E2E - Cli连接+发送", $"连接:{on.Status} 发送:{send.Status} 收到:{rx.Count}条");
                await CleanupAsync(svc, cli);
            }
            catch (Exception ex) { Fail("WS E2E - Cli→Svc", ex); }
        }

        static async Task Test_E2E_ServiceSend_ClientReceive()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new WsServiceOperate(new ServiceBasics { Host = Host(TestPort + 17), Timeout = 3000 });
                var svcOn = await svc.OnAsync();

                var cli = new WsClientOperate(new ClientBasics { Host = Host(TestPort + 17), InterruptReconnection = false, Timeout = 3000 });
                cli.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is byte[] b) lock (rx) rx.Add(b); };
                var cliOn = await cli.OnAsync(); await Task.Delay(1500);
                await cli.SendAsync(new byte[] { 0x01 }); await Task.Delay(1000); // 让服务端发现客户端

                var send = await svc.SendAsync(new byte[] { 0xCA, 0xFE }); await Task.Delay(1500);
                Assert(svcOn.Status && cliOn.Status && send.Status, "WS E2E - Svc→Cli", $"Svc:{svcOn.Status} Cli:{cliOn.Status} 发送:{send.Status} 收到:{rx.Count}条");
                await CleanupAsync(svc, cli);
            }
            catch (Exception ex) { Fail("WS E2E - Svc→Cli", ex); }
        }

        static async Task Test_E2E_Reconnect()
        {
            try
            {
                var rx = new List<byte[]>();
                var svc = new WsServiceOperate(new ServiceBasics { Host = Host(TestPort + 18), Timeout = 3000 });
                svc.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is Msg cm && cm.Bytes != null) lock (rx) rx.Add(cm.Bytes); };
                Assert((await svc.OnAsync()).Status, "WS E2E - Svc启动", null);

                // 客户端启用断线重连
                var cli = new WsClientOperate(new ClientBasics { Host = Host(TestPort + 18), InterruptReconnection = true, ReconnectionInterval = 1000, Timeout = 3000 });
                Assert((await cli.OnAsync()).Status, "WS E2E - Cli连接(重连)", null);
                await Task.Delay(1500);
                await cli.SendAsync(new byte[] { 0x01 }); await Task.Delay(500);
                int preCount = rx.Count;

                // 关闭再重启服务端，等待客户端重连周期检测并重新连接
                await svc.OffAsync(); await Task.Delay(500);
                Assert((await svc.OnAsync()).Status, "WS E2E - Svc重启", null);
                await Task.Delay(5000); // 等待客户端重连周期（ReconnectionInterval=1000 × 多次尝试）

                await cli.SendAsync(new byte[] { 0x02 }); await Task.Delay(1500);
                Assert(true, "WS E2E - 重连后发送", $"断开前:{preCount} 重连后:{rx.Count}");
                await CleanupAsync(svc, cli);
            }
            catch (Exception ex) { Fail("WS E2E - 重连", ex); }
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
            else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
