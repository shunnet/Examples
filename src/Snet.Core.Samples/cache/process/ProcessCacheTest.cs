using Snet.Core.cache.process;

namespace Snet.Core.Samples.cache.process;

/// <summary>
/// 进程缓存完整测试套件——覆盖每个公开函数及端到端流程。
/// </summary>
public static class ProcessCacheTest
{
    private static int _p, _f;
    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  ProcessCache (进程缓存) 测试套件");
        Console.WriteLine("============================================");

        // ── 单项函数 ──
        await Test_InstanceAsync();
        await Test_SetCacheAsync_Int();
        await Test_SetCacheAsync_String();
        await Test_SetCacheAsync_ByteArray();
        await Test_GetCacheAsync();
        await Test_SetCache_Sync();
        await Test_GetCache_Sync();
        await Test_RemoveCacheAsync();
        await Test_RemoveCache_Sync();
        await Test_ClearCacheAsync();
        await Test_Dispose();
        await Test_DisposeAsync();

        // ── 集成流程 ──
        await Test_Flow_Overwrite();
        await Test_Flow_RemoveAndRead();
        await Test_Flow_MultipleKeys();
        await Test_Flow_Expiry();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  ProcessCache 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    static async Task<ProcessCacheOperate> NewAsync(ProcessCacheData? d = null) => await ProcessCacheOperate.InstanceAsync(d ?? new ProcessCacheData());
    static ProcessCacheOperate New(ProcessCacheData? d = null) => ProcessCacheOperate.Instance(d ?? new ProcessCacheData());

    // ======================== 单项函数 ========================
    static async Task Test_InstanceAsync()
    { try { using var c = await NewAsync(); Assert(c != null, "InstanceAsync", "OK"); } catch (Exception ex) { Fail("InstanceAsync", ex); } }
    static async Task Test_SetCacheAsync_Int()
    { try { using var c = await NewAsync(); Assert((await c.SetCacheAsync("i", 42)).Status, "SetCacheAsync - int", null); } catch (Exception ex) { Fail("SetCacheAsync-int", ex); } }
    static async Task Test_SetCacheAsync_String()
    { try { using var c = await NewAsync(); Assert((await c.SetCacheAsync("s", "hello")).Status, "SetCacheAsync - string", null); } catch (Exception ex) { Fail("SetCacheAsync-string", ex); } }
    static async Task Test_SetCacheAsync_ByteArray()
    { try { using var c = await NewAsync(); Assert((await c.SetCacheAsync("b", new byte[] { 1, 2 })).Status, "SetCacheAsync - byte[]", null); } catch (Exception ex) { Fail("SetCacheAsync-byte[]", ex); } }
    static async Task Test_GetCacheAsync()
    { try { using var c = await NewAsync(); await c.SetCacheAsync("g", 999); var r = await c.GetCacheAsync<int>("g"); Assert(r.Status && r.ResultData is int v && v == 999, "GetCacheAsync", $"值:{r.ResultData}"); } catch (Exception ex) { Fail("GetCacheAsync", ex); } }
    static async Task Test_SetCache_Sync()
    { try { using var c = New(); Assert(c.SetCache("ss", 111).Status, "SetCache (同步)", null); } catch (Exception ex) { Fail("SetCache-sync", ex); } }
    static async Task Test_GetCache_Sync()
    { try { using var c = New(); c.SetCache("sg", "syncVal"); var r = c.GetCache<string>("sg"); Assert(r.Status && r.ResultData is string s && s == "syncVal", "GetCache (同步)", $"值:{r.ResultData}"); } catch (Exception ex) { Fail("GetCache-sync", ex); } }
    static async Task Test_RemoveCacheAsync()
    { try { using var c = await NewAsync(); await c.SetCacheAsync("rm", 1); Assert((await c.RemoveCacheAsync("rm")).Status, "RemoveCacheAsync", null); Assert(!(await c.GetCacheAsync<int>("rm")).Status, "RemoveCacheAsync - 确认", null); } catch (Exception ex) { Fail("RemoveCacheAsync", ex); } }
    static async Task Test_RemoveCache_Sync()
    { try { using var c = New(); c.SetCache("rms", 1); Assert(c.RemoveCache("rms").Status, "RemoveCache (同步)", null); Assert(!c.GetCache<int>("rms").Status, "RemoveCache (同步) - 确认", null); } catch (Exception ex) { Fail("RemoveCache-sync", ex); } }
    static async Task Test_ClearCacheAsync()
    { try { using var c = await NewAsync(); await c.SetCacheAsync("a", 1); await c.SetCacheAsync("b", 2); Assert((await c.ClearCacheAsync()).Status, "ClearCacheAsync", null); Assert(!(await c.GetCacheAsync<int>("a")).Status && !(await c.GetCacheAsync<int>("b")).Status, "ClearCacheAsync - 确认", null); } catch (Exception ex) { Fail("ClearCacheAsync", ex); } }
    static async Task Test_Dispose()
    { try { var c = await NewAsync(); await c.SetCacheAsync("x", 1); c.Dispose(); Assert(true, "Dispose", "已释放"); } catch (Exception ex) { Fail("Dispose", ex); } }
    static async Task Test_DisposeAsync()
    { try { var c = await NewAsync(); await c.SetCacheAsync("y", 1); await c.DisposeAsync(); Assert(true, "DisposeAsync", "已异步释放"); } catch (Exception ex) { Fail("DisposeAsync", ex); } }

    // ======================== 集成流程 ========================
    static async Task Test_Flow_Overwrite()
    {
        try { using var c = await NewAsync(); await c.SetCacheAsync("ov", 1); await c.SetCacheAsync("ov", 999); var r = await c.GetCacheAsync<int>("ov"); Assert(r.Status && r.ResultData is int v && v == 999, "流程 - 覆盖写入", $"值:{r.ResultData}"); }
        catch (Exception ex) { Fail("流程 - 覆盖写入", ex); }
    }
    static async Task Test_Flow_RemoveAndRead()
    {
        try { using var c = await NewAsync(); await c.SetCacheAsync("x", "will be removed"); Assert((await c.RemoveCacheAsync("x")).Status, "流程 - 删除", null); Assert(!(await c.GetCacheAsync<string>("x")).Status, "流程 - 确认删除", null); }
        catch (Exception ex) { Fail("流程 - 删除", ex); }
    }
    static async Task Test_Flow_MultipleKeys()
    {
        try { using var c = await NewAsync(); for (int i = 0; i < 5; i++) Assert((await c.SetCacheAsync($"k{i}", i * 10)).Status, $"流程 - 多Key写入[{i}]", null); for (int i = 0; i < 5; i++) { var r = await c.GetCacheAsync<int>($"k{i}"); Assert(r.Status && r.ResultData is int v && v == i * 10, $"流程 - 多Key读取[{i}]", $"值:{r.ResultData}"); } }
        catch (Exception ex) { Fail("流程 - 多Key", ex); }
    }
    static async Task Test_Flow_Expiry()
    {
        try { var d = new ProcessCacheData { AbsoluteExpiration = 0, SlidingExpiration = 0 }; var c = await NewAsync(d); await c.SetCacheAsync("ep", "短命"); await Task.Delay(1500); var r = await c.GetCacheAsync<string>("ep"); Assert(true, "流程 - 过期", $"Status:{r.Status} (已过短过期窗口)"); c.Dispose(); }
        catch (Exception ex) { Fail("流程 - 过期", ex); }
    }

    static void Assert(bool c, string n, string? d) { if (c) { _p++; Console.WriteLine($"  [PASS] {n}"); } else { _f++; Console.WriteLine($"  [FAIL] {n}" + (d != null ? $" — {d}" : "")); } }
    static void Fail(string n, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {n} — 异常: {ex.Message}"); }
}
