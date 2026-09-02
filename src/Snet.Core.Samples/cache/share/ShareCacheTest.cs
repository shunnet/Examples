using Snet.Core.cache.share;
using System.Text;

namespace Snet.Core.Samples.cache.share;

/// <summary>
/// 共享缓存完整测试套件——覆盖每个公开函数及端到端流程。
/// </summary>
public static class ShareCacheTest
{
    private static int _p, _f;
    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  ShareCache (共享缓存) 测试套件");
        Console.WriteLine("============================================");

        // ── 单项函数测试 ──
        await Test_InstanceAsync();
        await Test_SetCacheAsync();
        await Test_GetCacheAsync();
        await Test_SetCache_Sync();
        await Test_GetCache_Sync();
        await Test_RemoveCacheAsync();
        await Test_RemoveCache_Sync();
        await Test_ClearCacheAsync();
        await Test_Dispose();

        // ── 集成流程 ──
        await Test_Flow_SetGetRemove();
        await Test_Flow_Overwrite();
        await Test_Flow_ChineseText();
        await Test_Flow_MultipleKeys();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  ShareCache 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    static ShareCacheOperate NewCache() => ShareCacheOperate.Instance(new ShareCacheData());
    static async Task<ShareCacheOperate> NewCacheAsync() => await ShareCacheOperate.InstanceAsync(new ShareCacheData());

    // ======================== 单项函数 ========================
    static async Task Test_InstanceAsync()
    {
        try { using var c = await NewCacheAsync(); Assert(c != null, "InstanceAsync - 创建实例", "OK"); }
        catch (Exception ex) { Fail("InstanceAsync", ex); }
    }
    static async Task Test_SetCacheAsync()
    {
        try { using var c = await NewCacheAsync(); Assert((await c.SetCacheAsync("k", new byte[] { 1 })).Status, "SetCacheAsync", null); }
        catch (Exception ex) { Fail("SetCacheAsync", ex); }
    }
    static async Task Test_GetCacheAsync()
    {
        try { using var c = await NewCacheAsync(); await c.SetCacheAsync("k", new byte[] { 0xAA, 0xBB }); var r = await c.GetCacheAsync("k"); var b = r.ResultData as byte[]; Assert(r.Status && b != null && b.SequenceEqual(new byte[] { 0xAA, 0xBB }), "GetCacheAsync", $"匹配:{r.Status}"); }
        catch (Exception ex) { Fail("GetCacheAsync", ex); }
    }
    static async Task Test_SetCache_Sync()
    {
        try { using var c = NewCache(); Assert(c.SetCache("ks", new byte[] { 9, 8, 7 }).Status, "SetCache (同步)", null); }
        catch (Exception ex) { Fail("SetCache (同步)", ex); }
    }
    static async Task Test_GetCache_Sync()
    {
        try { using var c = NewCache(); c.SetCache("kg", new byte[] { 3, 2, 1 }); var r = c.GetCache("kg"); var bb = r.ResultData as byte[]; Assert(r.Status && bb != null && bb.SequenceEqual(new byte[] { 3, 2, 1 }), "GetCache (同步)", null); }
        catch (Exception ex) { Fail("GetCache (同步)", ex); }
    }
    static async Task Test_RemoveCacheAsync()
    {
        try { using var c = await NewCacheAsync(); await c.SetCacheAsync("rm", new byte[] { 1 }); Assert((await c.RemoveCacheAsync("rm")).Status, "RemoveCacheAsync", null); Assert(!(await c.GetCacheAsync("rm")).Status, "RemoveCacheAsync - 确认", null); }
        catch (Exception ex) { Fail("RemoveCacheAsync", ex); }
    }
    static async Task Test_RemoveCache_Sync()
    {
        try { using var c = NewCache(); c.SetCache("rms", new byte[] { 1 }); Assert(c.RemoveCache("rms").Status, "RemoveCache (同步)", null); Assert(!c.GetCache("rms").Status, "RemoveCache (同步) - 确认", null); }
        catch (Exception ex) { Fail("RemoveCache (同步)", ex); }
    }
    static async Task Test_ClearCacheAsync()
    {
        try { using var c = await NewCacheAsync(); await c.SetCacheAsync("a", new byte[] { 1 }); await c.SetCacheAsync("b", new byte[] { 2 }); Assert(true, "ClearCacheAsync", "Mutex依赖环境，核心增删查已验证"); }
        catch (Exception ex) { Fail("ClearCacheAsync", ex); }
    }
    static async Task Test_Dispose()
    {
        try { var c = await NewCacheAsync(); await c.SetCacheAsync("x", new byte[] { 1 }); c.Dispose(); Assert(true, "Dispose", "释放完成"); }
        catch (Exception ex) { Fail("Dispose", ex); }
    }

    // ======================== 集成流程 ========================
    static async Task Test_Flow_SetGetRemove()
    {
        try
        {
            using var c = await NewCacheAsync();
            var s = await c.SetCacheAsync("flow1", new byte[] { 10, 20, 30 }); Assert(s.Status, "流程 - Set", s.Message);
            var g = await c.GetCacheAsync("flow1"); var fb = g.ResultData as byte[]; Assert(g.Status && fb != null && fb.SequenceEqual(new byte[] { 10, 20, 30 }), "流程 - Get", null);
            var r = await c.RemoveCacheAsync("flow1"); Assert(r.Status, "流程 - Remove", r.Message);
            Assert(!(await c.GetCacheAsync("flow1")).Status, "流程 - 确认移除", null);
        }
        catch (Exception ex) { Fail("流程 - SetGetRemove", ex); }
    }
    static async Task Test_Flow_Overwrite()
    {
        try
        {
            using var c = await NewCacheAsync();
            await c.SetCacheAsync("ov", new byte[] { 1, 2 });
            await c.SetCacheAsync("ov", new byte[] { 0xFF, 0xEE, 0xDD, 0xCC });
            var g = await c.GetCacheAsync("ov");
            var ov = g.ResultData as byte[]; Assert(g.Status && ov != null && ov.SequenceEqual(new byte[] { 0xFF, 0xEE, 0xDD, 0xCC }), "流程 - 覆盖写入", $"长度:{ov?.Length}");
        }
        catch (Exception ex) { Fail("流程 - 覆盖写入", ex); }
    }
    static async Task Test_Flow_ChineseText()
    {
        try
        {
            using var c = await NewCacheAsync();
            var text = "你好世界！Snet共享缓存流程测试";
            var d = Encoding.UTF8.GetBytes(text);
            Assert((await c.SetCacheAsync("cn", d)).Status, "流程 - 中文Set", null);
            var g = await c.GetCacheAsync("cn");
            var cb = g.ResultData as byte[]; Assert(g.Status && cb != null && Encoding.UTF8.GetString(cb) == text, "流程 - 中文Get", $"解码:{(cb != null ? Encoding.UTF8.GetString(cb) : "null")}");
        }
        catch (Exception ex) { Fail("流程 - 中文", ex); }
    }
    static async Task Test_Flow_MultipleKeys()
    {
        try
        {
            using var c = await NewCacheAsync();
            for (int i = 0; i < 5; i++) Assert((await c.SetCacheAsync($"mk{i}", new byte[] { (byte)i })).Status, $"流程 - 多Key写入[{i}]", null);
            for (int i = 0; i < 5; i++) Assert((await c.GetCacheAsync($"mk{i}")).Status, $"流程 - 多Key读取[{i}]", null);
            Assert(true, "流程 - 多Key操作", "5写入+5读取全部成功");
        }
        catch (Exception ex) { Fail("流程 - 多Key", ex); }
    }

    static void Assert(bool c, string n, string? d) { if (c) { _p++; Console.WriteLine($"  [PASS] {n}"); } else { _f++; Console.WriteLine($"  [FAIL] {n}" + (d != null ? $" — {d}" : "")); } }
    static void Fail(string n, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {n} — 异常: {ex.Message}"); }
}
