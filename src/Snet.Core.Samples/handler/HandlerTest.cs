using Snet.Core.handler;
using Snet.Model.data;
using Snet.Model.@enum;

namespace Snet.Core.Samples.handler;

/// <summary>
/// 处理器完整测试套件<br/>
/// 测试 AddressHandler.ExecuteDispose（地址值处理）<br/>
/// 测试 BytesHandler.TransformAsync（字节→地址值转换）
/// </summary>
public static class HandlerTest
{
    private static int _p, _f;

    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  Handler 测试套件");
        Console.WriteLine("============================================\n");

        await Test_AddressHandler_Float();
        await Test_AddressHandler_NaN();
        await Test_AddressHandler_Ushort();
        await Test_BytesHandler_Transform();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  Handler 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    static async Task Test_AddressHandler_Float()
    {
        try
        {
            var d = new AddressDetails { AddressName = "test.float", AddressDataType = DataType.Float };
            var r = AddressHandler.ExecuteDispose(d, 1.1f, "成功");
            Assert(r != null && r.ResultValue is float f && Math.Abs(f - 1.1f) < 0.001f,
                "AddressHandler - Float正常", $"值:{r?.ResultValue}");
        }
        catch (Exception ex) { Fail("AddressHandler - Float", ex); }
    }

    static async Task Test_AddressHandler_NaN()
    {
        try
        {
            var d = new AddressDetails { AddressName = "test.nan", AddressDataType = DataType.Float };
            var r = AddressHandler.ExecuteDispose(d, float.NaN, "NaN");
            Assert(r != null, "AddressHandler - NaN处理", $"Quality:{r?.Quality}");
        }
        catch (Exception ex) { Fail("AddressHandler - NaN", ex); }
    }

    static async Task Test_AddressHandler_Ushort()
    {
        try
        {
            var d = new AddressDetails { AddressName = "test.ush", AddressDataType = DataType.Ushort };
            var r = AddressHandler.ExecuteDispose(d, 999L, "正常");
            Assert(r != null, "AddressHandler - Ushort", $"值:{r?.ResultValue}, Quality:{r?.Quality}");
        }
        catch (Exception ex) { Fail("AddressHandler - Ushort", ex); }
    }

    static async Task Test_BytesHandler_Transform()
    {
        try
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(123.456f));
            bytes.AddRange(BitConverter.GetBytes(7890.12f));
            bytes.AddRange(BitConverter.GetBytes(555.123d));
            bytes.AddRange(BitConverter.GetBytes(99.9f));
            byte[] buf = bytes.ToArray();

            List<BytesModel> models = new()
            {
                new BytesModel("F1", "float_0", 0, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("F2", "float_4", 4, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("D1", "double_8", 8, 8, DataType.Double, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("F3", "float_16", 16, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
            };

            var handler = BytesHandler.Instance(Guid.NewGuid().ToString());
            var result = await handler.TransformAsync(buf, DateTime.Now, models);
            Assert(result.Status, "BytesHandler.Transform", $"耗时:{result.RunTime}ms");
            handler.Dispose();
        }
        catch (Exception ex) { Fail("BytesHandler.Transform", ex); }
    }

    static void Assert(bool cond, string name, string? detail)
    {
        if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
        else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
    }
    static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
}
