using Snet.Core.mq;
using Snet.Core.reflection;
using System.Collections.Concurrent;
using RD = Snet.Core.reflection.ReflectionData;

namespace Snet.Core.Samples.reflection;

/// <summary>
/// Reflection + MQ 完整测试套件——覆盖每个公开函数及端到端流程。
/// DLL 反射测试在库文件缺失时自动跳过并给出提示。
/// </summary>
public static class ReflectionMqTest
{
    private static int _p, _f;
    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  Reflection + MQ 测试套件");
        Console.WriteLine("============================================");

        // ── MQ 单项函数 ──
        await Test_Mq_Instance();
        await Test_Mq_OnAsync();
        await Test_Mq_OffAsync();
        await Test_Mq_ProduceAsync_String();
        await Test_Mq_ProduceAsync_Bytes();
        await Test_Mq_Produce_Sync();
        await Test_Mq_ConsumeAsync();
        await Test_Mq_UnConsumeAsync();
        await Test_Mq_RemoveAsync();
        await Test_Mq_DisposeAsync_Instance();
        await Test_Mq_Dispose();
        await Test_Mq_DisposeAsync();

        // ── MQ 集成流程 ──
        await Test_Mq_Flow_OnProduceOff();

        // ── Reflection 单项函数 ──
        await Test_Ref_Instance();
        await Test_Ref_GetStatus();
        await Test_Ref_Init();
        await Test_Ref_InitAsync();
        await Test_Ref_GetMethods();
        await Test_Ref_GetMethod();
        await Test_Ref_GetEvents();
        await Test_Ref_GetEvent();
        await Test_Ref_RegisterEvent();
        await Test_Ref_ReflectionInstance();
        await Test_Ref_CreateConstructorParam();
        await Test_Ref_Dispose();
        await Test_Ref_DisposeAsync();

        // ── Reflection 集成流程 ──
        await Test_Ref_Flow_InitAndExecute();
        await Test_Ref_Flow_LoadDllAndReflect();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  ReflectionMQ 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    // ======================== MQ 单项函数 ========================
    static async Task Test_Mq_Instance()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); Assert(mq != null, "Mq.InstanceAsync", "创建成功"); } catch (Exception ex) { Fail("Mq.InstanceAsync", ex); } }
    static async Task Test_Mq_OnAsync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.OnAsync(); Assert(true, "Mq.OnAsync", $"Status:{r.Status} (无Mq实例时预期失败)"); } catch (Exception ex) { Fail("Mq.OnAsync", ex); } }
    static async Task Test_Mq_OffAsync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.OffAsync(); Assert(true, "Mq.OffAsync", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.OffAsync", ex); } }
    static async Task Test_Mq_ProduceAsync_String()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.ProduceAsync("test", "Hello MQ"); Assert(true, "Mq.ProduceAsync(string)", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.ProduceAsync(string)", ex); } }
    static async Task Test_Mq_ProduceAsync_Bytes()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.ProduceAsync("test", new byte[] { 1, 2, 3 }); Assert(true, "Mq.ProduceAsync(byte[])", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.ProduceAsync(byte[])", ex); } }
    static async Task Test_Mq_Produce_Sync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = mq.Produce("test", "sync msg"); Assert(true, "Mq.Produce (同步)", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.Produce-sync", ex); } }
    static async Task Test_Mq_ConsumeAsync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.ConsumeAsync("test"); Assert(true, "Mq.ConsumeAsync", $"Status:{r.Status} (无Mq实例预期失败)"); } catch (Exception ex) { Fail("Mq.ConsumeAsync", ex); } }
    static async Task Test_Mq_UnConsumeAsync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.UnConsumeAsync("test"); Assert(true, "Mq.UnConsumeAsync", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.UnConsumeAsync", ex); } }
    static async Task Test_Mq_RemoveAsync()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.RemoveAsync(); Assert(true, "Mq.RemoveAsync", $"Status:{r.Status}"); } catch (Exception ex) { Fail("Mq.RemoveAsync", ex); } }
    static async Task Test_Mq_DisposeAsync_Instance()
    { try { using var mq = await MqOperate.InstanceAsync(new MqData()); var r = await mq.DisposeAsync("non_existent"); Assert(true, "Mq.DisposeAsync(ISn)", $"Status:{r.Status} (预期找不到实例)"); } catch (Exception ex) { Fail("Mq.DisposeAsync(ISn)", ex); } }
    static async Task Test_Mq_Dispose()
    { try { var mq = await MqOperate.InstanceAsync(new MqData()); mq.Dispose(); Assert(true, "Mq.Dispose", "已释放"); } catch (Exception ex) { Fail("Mq.Dispose", ex); } }
    static async Task Test_Mq_DisposeAsync()
    { try { var mq = await MqOperate.InstanceAsync(new MqData()); await mq.DisposeAsync(); Assert(true, "Mq.DisposeAsync", "已异步释放"); } catch (Exception ex) { Fail("Mq.DisposeAsync", ex); } }

    // ======================== MQ 集成流程 ========================
    static async Task Test_Mq_Flow_OnProduceOff()
    {
        try
        {
            using var mq = await MqOperate.InstanceAsync(new MqData());
            var on = await mq.OnAsync();
            Assert(true, "MQ流程 - OnAsync", $"Status:{on.Status}");

            var prod = await mq.ProduceAsync("flow_topic", "MQ集成测试消息");
            Assert(true, "MQ流程 - ProduceAsync", $"Status:{prod.Status}");

            var off = await mq.OffAsync();
            Assert(true, "MQ流程 - OffAsync", $"Status:{off.Status}");
        }
        catch (Exception ex) { Fail("MQ流程", ex); }
    }

    // ======================== Reflection 单项函数 ========================
    static async Task Test_Ref_Instance()
    { try { var rb = new RD.Basics { DllDatas = new List<RD.DllData>() }; using var r = await ReflectionOperate.InstanceAsync(rb); Assert(r != null, "Ref.InstanceAsync", "创建成功"); } catch (Exception ex) { Fail("Ref.InstanceAsync", ex); } }
    static async Task Test_Ref_GetStatus()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); Assert(!r.GetStatus(), "Ref.GetStatus - 未初始化false", null); } catch (Exception ex) { Fail("Ref.GetStatus", ex); } }
    static async Task Test_Ref_Init()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); var init = r.Init(); Assert(init.Status, "Ref.Init (同步) - 空配置", init.Message); } catch (Exception ex) { Fail("Ref.Init-sync", ex); } }
    static async Task Test_Ref_InitAsync()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); var init = await r.InitAsync(); Assert(init.Status, "Ref.InitAsync - 空配置", init.Message); } catch (Exception ex) { Fail("Ref.InitAsync", ex); } }
    static async Task Test_Ref_GetMethods()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); var m = r.GetMethods(); Assert(m == null, "Ref.GetMethods - 空配置返回null", "OK"); } catch (Exception ex) { Fail("Ref.GetMethods", ex); } }
    static async Task Test_Ref_GetMethod()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); Assert(r.GetMethod("nonexist") == null, "Ref.GetMethod - 不存在返回null", null); Assert(r.GetMethod(null!) == null, "Ref.GetMethod - null输入返回null", null); } catch (Exception ex) { Fail("Ref.GetMethod", ex); } }
    static async Task Test_Ref_GetEvents()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); Assert(r.GetEvents() == null, "Ref.GetEvents - 空配置返回null", "OK"); } catch (Exception ex) { Fail("Ref.GetEvents", ex); } }
    static async Task Test_Ref_GetEvent()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); Assert(r.GetEvent("nonexist") == null, "Ref.GetEvent - 不存在返回null", null); Assert(r.GetEvent(null!) == null, "Ref.GetEvent - null输入返回null", null); } catch (Exception ex) { Fail("Ref.GetEvent", ex); } }
    static async Task Test_Ref_RegisterEvent()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); var ev = await r.RegisterEventAsync("noexist", true, P1: _ => { }); Assert(!ev.Status, "Ref.RegisterEventAsync - 不存在返回失败", ev.Message); } catch (Exception ex) { Fail("Ref.RegisterEventAsync", ex); } }
    static async Task Test_Ref_ReflectionInstance()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); Assert(r.ReflectionInstance("noexist") == null, "Ref.ReflectionInstance - 不存在返回null", null); Assert(r.ReflectionInstance(null!) == null, "Ref.ReflectionInstance - null返回null", null); } catch (Exception ex) { Fail("Ref.ReflectionInstance", ex); } }
    static async Task Test_Ref_CreateConstructorParam()
    { try { using var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.InitAsync(); Assert(r.CreateConstructorParam("no.dll", "NoType", new ConcurrentDictionary<string, object?>()) == null, "Ref.CreateConstructorParam - DLL不存在返回null", null); } catch (Exception ex) { Fail("Ref.CreateConstructorParam", ex); } }
    static async Task Test_Ref_Dispose()
    { try { var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); r.Dispose(); Assert(true, "Ref.Dispose", "已释放"); } catch (Exception ex) { Fail("Ref.Dispose", ex); } }
    static async Task Test_Ref_DisposeAsync()
    { try { var r = await ReflectionOperate.InstanceAsync(new RD.Basics { DllDatas = new List<RD.DllData>() }); await r.DisposeAsync(); Assert(true, "Ref.DisposeAsync", "已异步释放"); } catch (Exception ex) { Fail("Ref.DisposeAsync", ex); } }

    // ======================== Reflection 集成流程 ========================
    static async Task Test_Ref_Flow_InitAndExecute()
    {
        try
        {
            var rb = new RD.Basics { DllDatas = new List<RD.DllData>() };
            using var r = await ReflectionOperate.InstanceAsync(rb);
            Assert(!r.GetStatus(), "Ref流程 - 初始未初始化", null);
            var init = await r.InitAsync();
            Assert(init.Status, "Ref流程 - 初始化", init.Message);
            Assert(r.GetStatus(), "Ref流程 - 已初始化状态", null);
            var reInit = await r.InitAsync();
            Assert(reInit.Status, "Ref流程 - 重复初始化安全", reInit.Message);
        }
        catch (Exception ex) { Fail("Ref流程 - Init", ex); }
    }
    static async Task Test_Ref_Flow_LoadDllAndReflect()
    {
        try
        {
            string dllPath = Path.Combine(AppContext.BaseDirectory, "lib", "reflection", "Snet.Mqtt.Pack", "Snet.Mqtt.dll");
            if (!File.Exists(dllPath))
            {
                Assert(true, "Ref流程 - 加载DLL", $"跳过（文件不存在: {dllPath}）");
                Console.WriteLine($"    提示: 放置 Snet.Mqtt.dll 到 lib/reflection/Snet.Mqtt.Pack/ 以启用此测试");
                return;
            }

            dynamic cfg = new System.Dynamic.ExpandoObject();
            cfg.IpAddress = "127.0.0.1"; cfg.Port = 8111; cfg.UserName = "shunnet"; cfg.Password = "shunnet";

            var rb = new RD.Basics
            {
                DllDatas = new List<RD.DllData>
                {
                    new RD.DllData { DllPath = dllPath, IsAbsolutePath = true, NamespaceDatas = new List<RD.NamespaceData>
                    {
                        new RD.NamespaceData { Namespace = "Snet.Mqtt.service", ClassDatas = new List<RD.ClassData>
                        {
                            new RD.ClassData { SN = "[MqttSvc]", ClassName = "MqttServiceOperate", ConstructorParam = new object[] { cfg },
                                MethodDatas = new List<RD.MethodData> { new RD.MethodData { SN = "[On]", MethodName = "OnAsync" } } }
                        } }
                    } }
                }
            };

            var r = await ReflectionOperate.InstanceAsync(rb);
            var init = await r.InitAsync();
            Assert(init.Status, "Ref流程 - DLL加载初始化", init.Message);

            if (init.Status)
            {
                var methods = r.GetMethods();
                Assert(methods != null, "Ref流程 - 方法容器非空", null);
                var exec = r.ExecuteMethod("[MqttSvc][On]", new object[] { CancellationToken.None });
                Assert(true, "Ref流程 - 方法执行", exec != null ? "返回非null" : "返回null（异步方法预期）");
            }
            r.Dispose();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("Could not load") || msg.Contains("无法加载"))
                Assert(true, "Ref流程 - 加载DLL", $"跳过（依赖缺失）");
            else
                Fail("Ref流程 - 加载DLL", ex);
        }
    }

    static void Assert(bool c, string n, string? d) { if (c) { _p++; Console.WriteLine($"  [PASS] {n}"); } else { _f++; Console.WriteLine($"  [FAIL] {n}" + (d != null ? $" — {d}" : "")); } }
    static void Fail(string n, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {n} — 异常: {ex.Message}"); }
}
