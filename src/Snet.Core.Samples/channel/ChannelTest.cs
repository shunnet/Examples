using Snet.Core.channel;

namespace Snet.Core.Samples.channel;

/// <summary>
/// 通道完整测试套件——覆盖每个公开函数及端到端流程。
/// </summary>
public static class ChannelTest
{
    private static int _p, _f;
    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  Channel (通道) 测试套件");
        Console.WriteLine("============================================");

        // ── 单项函数 ──
        await Test_InstanceAsync();
        await Test_WriteAsync();
        await Test_ReadAsync();
        await Test_ReadWaitAsync();
        await Test_TryRead();
        await Test_TryRead_Out();
        await Test_TryWrite();
        await Test_Count();
        await Test_IsDisposed();
        await Test_ResetChannelAsync();
        await Test_ResetChannel_Sync();
        await Test_Dispose();
        await Test_DisposeAsync();

        // ── 集成流程 ──
        await Test_Flow_SyncMode();
        await Test_Flow_AsyncEventMode();
        await Test_Flow_WriteReadWait();
        await Test_Flow_ResetAndReuse();
        await Test_Flow_BatchThroughput();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  Channel 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    static async Task<ChannelOperate<string>> NewAsync(bool sync = true) => await ChannelOperate<string>.InstanceAsync(new ChannelData { IsSync = sync });
    static ChannelOperate<string> New(bool sync = true) => ChannelOperate<string>.Instance(new ChannelData { IsSync = sync });
    static CancellationToken Ct => CancellationToken.None;

    // ======================== 单项函数 ========================
    static async Task Test_InstanceAsync()
    { try { using var c = await NewAsync(); Assert(c != null, "InstanceAsync", "OK"); } catch (Exception ex) { Fail("InstanceAsync", ex); } }
    static async Task Test_WriteAsync()
    { try { using var c = await NewAsync(); Assert((await c.WriteAsync("hello", Ct)).Status, "WriteAsync", null); } catch (Exception ex) { Fail("WriteAsync", ex); } }
    static async Task Test_ReadAsync()
    { try { using var c = await NewAsync(); await c.WriteAsync("world", Ct); var r = await c.ReadAsync(Ct); Assert(r.Status && r.ResultData is string s && s == "world", "ReadAsync", $"值:{r.ResultData}"); } catch (Exception ex) { Fail("ReadAsync", ex); } }
    static async Task Test_ReadWaitAsync()
    { try { using var c = await NewAsync(); await c.WriteAsync("wait", Ct); var r = await c.ReadWaitAsync(5000); Assert(r.Status && r.ResultData is string s && s == "wait", "ReadWaitAsync - 有数据", null); var r2 = await c.ReadWaitAsync(500); Assert(!r2.Status, "ReadWaitAsync - 超时", r2.Message); } catch (Exception ex) { Fail("ReadWaitAsync", ex); } }
    static async Task Test_TryRead()
    { try { using var c = await NewAsync(); await c.WriteAsync("try", Ct); var r = c.TryRead(); Assert(r.Status, "TryRead - 返回OperateResult", null); Assert(!c.TryRead().Status, "TryRead - 空通道返回失败", null); } catch (Exception ex) { Fail("TryRead", ex); } }
    static async Task Test_TryRead_Out()
    { try { using var c = await NewAsync(); await c.WriteAsync("out", Ct); Assert(c.TryRead(out string val) && val == "out", "TryRead - out参数", $"值:{val}"); Assert(!c.TryRead(out _), "TryRead - out空通道", null); } catch (Exception ex) { Fail("TryRead-out", ex); } }
    static async Task Test_TryWrite()
    { try { using var c = await NewAsync(); Assert(c.TryWrite("tw").Status, "TryWrite", null); var r = await c.ReadAsync(Ct); Assert(r.ResultData is string s && s == "tw", "TryWrite - 验证读到", $"值:{r.ResultData}"); } catch (Exception ex) { Fail("TryWrite", ex); } }
    static async Task Test_Count()
    { try { using var c = await NewAsync(); Assert(c.Count == 0, "Count - 初始0", null); await c.WriteAsync("a", Ct); Assert(c.Count == 1, "Count - 写入后1", null); await c.WriteAsync("b", Ct); Assert(c.Count == 2, "Count - 写入后2", null); } catch (Exception ex) { Fail("Count", ex); } }
    static async Task Test_IsDisposed()
    { try { using var c = await NewAsync(); Assert(!c.IsDisposed, "IsDisposed - false", null); c.Dispose(); Assert(c.IsDisposed, "IsDisposed - true", null); } catch (Exception ex) { Fail("IsDisposed", ex); } }
    static async Task Test_ResetChannelAsync()
    { try { using var c = await NewAsync(); await c.WriteAsync("before", Ct); await c.ResetChannelAsync(); Assert(c.Count == 0, "ResetChannelAsync - 清空", null); } catch (Exception ex) { Fail("ResetChannelAsync", ex); } }
    static async Task Test_ResetChannel_Sync()
    { try { using var c = New(); c.WriteAsync("syncReset", CancellationToken.None).AsTask().Wait(); c.ResetChannel(); Assert(c.Count == 0, "ResetChannel (同步)", null); } catch (Exception ex) { Fail("ResetChannel-sync", ex); } }
    static async Task Test_Dispose()
    { try { var c = await NewAsync(); await c.WriteAsync("x", Ct); c.Dispose(); Assert(true, "Dispose", "已释放"); } catch (Exception ex) { Fail("Dispose", ex); } }
    static async Task Test_DisposeAsync()
    { try { var c = await NewAsync(); await c.WriteAsync("y", Ct); await c.DisposeAsync(); Assert(true, "DisposeAsync", "已异步释放"); } catch (Exception ex) { Fail("DisposeAsync", ex); } }

    // ======================== 集成流程 ========================
    static async Task Test_Flow_SyncMode()
    {
        try
        {
            using var c = await NewAsync(true);
            for (int i = 0; i < 10; i++) Assert((await c.WriteAsync($"Item_{i}", Ct)).Status, $"流程Sync - 写[{i}]", null);
            Assert(c.Count == 10, "流程Sync - Count=10", null);
            for (int i = 0; i < 10; i++) { var r = await c.ReadAsync(Ct); Assert(r.Status, $"流程Sync - 读[{i}]", null); }
            Assert(c.Count == 0, "流程Sync - 读完后Count=0", null);
        }
        catch (Exception ex) { Fail("流程Sync", ex); }
    }
    static async Task Test_Flow_AsyncEventMode()
    {
        try
        {
            var rx = new List<string>();
            var c = await ChannelOperate<string>.InstanceAsync(new ChannelData { IsSync = false });
            c.OnDataEvent += (_, e) => { if (e.Status && e.ResultData is string s) lock (rx) rx.Add(s); };
            await c.WriteAsync("E1", Ct); await c.WriteAsync("E2", Ct); await c.WriteAsync("E3", Ct);
            await Task.Delay(500);
            Assert(rx.Count == 3, "流程Async - 事件回调", $"收到:{rx.Count}条 [{string.Join(",", rx)}]");
            await c.DisposeAsync();
        }
        catch (Exception ex) { Fail("流程Async", ex); }
    }
    static async Task Test_Flow_WriteReadWait()
    {
        try
        {
            using var c = await NewAsync();
            _ = Task.Run(async () => { await Task.Delay(300); await c.WriteAsync("delayed", Ct); });
            var r = await c.ReadWaitAsync(5000);
            Assert(r.Status && r.ResultData is string s && s == "delayed", "流程ReadWait - 延迟写入后读到", $"值:{r.ResultData}");
        }
        catch (Exception ex) { Fail("流程ReadWait", ex); }
    }
    static async Task Test_Flow_ResetAndReuse()
    {
        try
        {
            using var c = await NewAsync();
            await c.WriteAsync("A", Ct); await c.WriteAsync("B", Ct); await c.WriteAsync("C", Ct);
            Assert(c.Count == 3, "流程Reset - 写3条", null);
            await c.ResetChannelAsync();
            Assert(c.Count == 0, "流程Reset - 清空", null);
            await c.WriteAsync("D", Ct);
            var r = await c.ReadAsync(Ct);
            Assert(r.Status && r.ResultData is string s && s == "D", "流程Reset - 重置后可读写", $"值:{r.ResultData}");
        }
        catch (Exception ex) { Fail("流程Reset", ex); }
    }
    static async Task Test_Flow_BatchThroughput()
    {
        try
        {
            const int N = 1000;
            using var c = await NewAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < N; i++) await c.WriteAsync($"Batch_{i}", Ct);
            sw.Stop();
            Assert(c.Count == N, "流程Batch - 批量写入", $"写{N}条耗时:{sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex) { Fail("流程Batch", ex); }
    }

    static void Assert(bool c, string n, string? d) { if (c) { _p++; Console.WriteLine($"  [PASS] {n}"); } else { _f++; Console.WriteLine($"  [FAIL] {n}" + (d != null ? $" — {d}" : "")); } }
    static void Fail(string n, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {n} — 异常: {ex.Message}"); }
}
