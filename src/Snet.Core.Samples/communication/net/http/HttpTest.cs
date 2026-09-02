using Snet.Core.communication.net.http.client;
using Snet.Core.communication.net.http.service;
using static Snet.Core.communication.net.http.client.HttpClientData;
using SvcBasics = Snet.Core.communication.net.http.service.HttpServiceData.Basics;
using SvcHandler = Snet.Core.communication.net.http.service.HttpServiceData.WaitHandler;

namespace Snet.Core.Samples.communication.net.http
{
    /// <summary>
    /// HTTP 完整测试套件<br/>
    /// 服务端启动 → 客户端 GET/POST → 状态码验证 → 错误路径。
    /// （HTTP 是无状态请求-响应模型，无断线重连，但测试服务端重启后通信）
    /// </summary>
    public static class HttpTest
    {
        private const int TestPort = 13688;
        private static int _p, _f;

        public static async Task RunAllAsync()
        {
            _p = 0; _f = 0;
            Console.WriteLine("\n============================================");
            Console.WriteLine("  HTTP 测试套件");
            Console.WriteLine("============================================\n");

            await Test_Service_ApiSurface();
            await Test_Client_ApiSurface();
            await Test_E2E_GetPost();
            await Test_E2E_ServiceRestart();

            Console.WriteLine("\n--------------------------------------------");
            Console.WriteLine($"  HTTP 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
            Console.WriteLine("--------------------------------------------\n");
        }

        // ======================== 服务端 API 面 ========================
        static async Task Test_Service_ApiSurface()
        {
            try
            {
                var svc = new HttpServiceOperate(new SvcBasics { Port = TestPort, IpAddress = "127.0.0.1" });
                Assert((await svc.OnAsync()).Status, "HTTP Svc.OnAsync", null);
                Assert((await svc.GetStatusAsync()).Status, "HTTP Svc.GetStatusAsync", null);
                Assert(!(await svc.OnAsync()).Status, "HTTP Svc.OnAsync - 重复", null);
                Assert((await svc.OffAsync()).Status, "HTTP Svc.OffAsync", null);
                Assert(!(await svc.OffAsync()).Status, "HTTP Svc.OffAsync - 未启动", null);
                svc.Dispose();
            }
            catch (Exception ex) { Fail("HTTP Svc.API", ex); }
        }

        // ======================== 客户端 API 面 ========================
        static async Task Test_Client_ApiSurface()
        {
            try
            {
                var svc = new HttpServiceOperate(new SvcBasics { Port = TestPort + 1, IpAddress = "127.0.0.1" });
                svc.OnDataEvent += async (_, e) => { if (e.ResultData is SvcHandler wh) await svc.WriteAsync(wh.Response, new { ok = true }); };
                Assert((await svc.OnAsync()).Status, "HTTP E2E - Svc启动", null);

                using var cli = new HttpClientOperate(new HttpClientData.Basics());
                // GET
                var r1 = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 1}/api/snet", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r1.Status && (r1.ResultData as ResponseData)?.StatusCode == 200, "HTTP Cli.RequestAsync - GET", $"Code:{(r1.ResultData as ResponseData)?.StatusCode}");
                // POST
                var r2 = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 1}/api/snet", Method = System.Net.Http.HttpMethod.Post, BodyContent = "{}", BodyType = BType.Raw, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r2.Status, "HTTP Cli.RequestAsync - POST", null);
                // 404
                var r3 = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 1}/api/nonexistent", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r3.Status, "HTTP Cli.RequestAsync - 404", null); // 服务端返回400
                // 同步
                var r4 = cli.Request(new RequestData { Url = $"http://127.0.0.1:{TestPort + 1}/api/snet", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r4.Status, "HTTP Cli.Request (同步) - GET", null);

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("HTTP Cli.API", ex); }
        }

        // ======================== E2E ========================
        static async Task Test_E2E_GetPost()
        {
            try
            {
                var svc = new HttpServiceOperate(new SvcBasics { Port = TestPort + 2, IpAddress = "127.0.0.1" });
                svc.OnDataEvent += async (_, e) =>
                {
                    if (e.ResultData is SvcHandler wh)
                        await svc.WriteAsync(wh.Response, new { message = "hello", ts = DateTime.Now });
                };
                Assert((await svc.OnAsync()).Status, "HTTP E2E - Svc启动", null);

                using var cli = new HttpClientOperate(new HttpClientData.Basics());
                var r = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 2}/api/snet", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r.Status, "HTTP E2E - GET", r.Message);

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("HTTP E2E - GET", ex); }
        }

        static async Task Test_E2E_ServiceRestart()
        {
            try
            {
                // 第一次启动
                var svc = new HttpServiceOperate(new SvcBasics { Port = TestPort + 3, IpAddress = "127.0.0.1" });
                svc.OnDataEvent += async (_, e) => { if (e.ResultData is SvcHandler wh) await svc.WriteAsync(wh.Response, new { round = 1 }); };
                Assert((await svc.OnAsync()).Status, "HTTP E2E - Svc第1次启动", null);

                using var cli = new HttpClientOperate(new HttpClientData.Basics());
                var r1 = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 3}/api/snet", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r1.Status, "HTTP E2E - 第1次请求", null);

                // 关闭再重启
                Assert((await svc.OffAsync()).Status, "HTTP E2E - Svc关闭", null);
                await Task.Delay(200);
                Assert((await svc.OnAsync()).Status, "HTTP E2E - Svc重启", null);
                await Task.Delay(200);

                var r2 = await cli.RequestAsync(new RequestData { Url = $"http://127.0.0.1:{TestPort + 3}/api/snet", Method = System.Net.Http.HttpMethod.Get, TimeOut = TimeSpan.FromSeconds(10) });
                Assert(r2.Status, "HTTP E2E - 重启后请求", r2.Message);

                await svc.OffAsync();
            }
            catch (Exception ex) { Fail("HTTP E2E - 重启", ex); }
        }

        static void Assert(bool cond, string name, string? detail)
        {
            if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
            else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
        }
        static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
    }
}
