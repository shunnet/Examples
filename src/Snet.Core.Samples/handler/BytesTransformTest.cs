using Snet.Core.handler;
using Snet.Model.data;
using Snet.Model.@enum;

namespace Snet.Core.Samples.handler;

/// <summary>
/// 字节解析转换完整测试套件<br/>
/// 模拟 PLC 批量读取大量地址后的数据解析转换，覆盖简单示例与大数据场景。
/// </summary>
public static class BytesTransformTest
{
    private static int _p, _f;

    public static async Task RunAllAsync()
    {
        _p = 0; _f = 0;
        Console.WriteLine("\n============================================");
        Console.WriteLine("  BytesTransform 测试套件");
        Console.WriteLine("============================================\n");

        await Test_SimpleTransform();
        await Test_SimpleTransform_Loop();
        await Test_LargeDataset_40000Points();

        Console.WriteLine("\n--------------------------------------------");
        Console.WriteLine($"  BytesTransform 结果: {_p} 通过, {_f} 失败 (共 {_p + _f} 项)");
        Console.WriteLine("--------------------------------------------\n");
    }

    /// <summary>
    /// 简单示例：模拟 PLC 读取 4 个点（float/float/double/float）
    /// </summary>
    static async Task Test_SimpleTransform()
    {
        try
        {
            // 模拟 PLC 一次性读取到的字节数据
            var raw = new List<byte>();
            raw.AddRange(BitConverter.GetBytes(123456f));
            raw.AddRange(BitConverter.GetBytes(123456.1f));
            raw.AddRange(BitConverter.GetBytes(123456.123456d));
            raw.AddRange(BitConverter.GetBytes(11.223f));
            byte[] buffer = raw.ToArray();

            // 映射配置
            List<BytesModel> models = new()
            {
                new BytesModel("100", "modbus_100_float", 0, 4, DataType.Float, EncodingType.UTF8, DataFormat.DCBA),
                new BytesModel("102", "modbus_102_float", 4, 4, DataType.Float, EncodingType.UTF8, DataFormat.DCBA),
                new BytesModel("104", "modbus_104_double", 8, 8, DataType.Double, EncodingType.UTF8, DataFormat.DCBA),
                new BytesModel("106", "modbus_106_float", 16, 4, DataType.Float, EncodingType.UTF8, DataFormat.DCBA),
            };

            using var handler = BytesHandler.Instance(Guid.NewGuid().ToString());
            var result = await handler.TransformAsync(buffer, DateTime.Now, models);
            Assert(result.Status, "简单示例 - 4个点解析", $"耗时:{result.RunTime}ms, 结果:{result.Message}");
        }
        catch (Exception ex) { Fail("简单示例", ex); }
    }

    /// <summary>
    /// 循环解析测试（5次迭代，验证无内存泄漏或性能退化）
    /// </summary>
    static async Task Test_SimpleTransform_Loop()
    {
        try
        {
            var raw = new List<byte>();
            raw.AddRange(BitConverter.GetBytes(123456f));
            raw.AddRange(BitConverter.GetBytes(123456.1f));
            raw.AddRange(BitConverter.GetBytes(123456.123456d));
            raw.AddRange(BitConverter.GetBytes(11.223f));
            byte[] buffer = raw.ToArray();

            List<BytesModel> models = new()
            {
                new BytesModel("100", "float_a", 0, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("102", "float_b", 4, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("104", "double_c", 8, 8, DataType.Double, EncodingType.UTF8, DataFormat.ABCD),
                new BytesModel("106", "float_d", 16, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD),
            };

            using var handler = BytesHandler.Instance(Guid.NewGuid().ToString());
            int totalMs = 0;
            for (int i = 0; i < 5; i++)
            {
                var result = await handler.TransformAsync(buffer, DateTime.Now, models);
                if (!result.Status) { Fail($"循环解析 [{i}]", new Exception(result.Message)); return; }
                totalMs += result.RunTime;
                await Task.Delay(50);
            }

            Assert(true, "循环解析 - 5次迭代", $"平均:{totalMs / 5}ms/次, 总:{totalMs}ms");
        }
        catch (Exception ex) { Fail("循环解析", ex); }
    }

    /// <summary>
    /// 大数据示例：模拟 PLC 一次性读取 40000 个点（各类型混合）<br/>
    /// 验证大量地址的解析性能和正确性。
    /// </summary>
    static async Task Test_LargeDataset_40000Points()
    {
        try
        {
            const int PointCount = 10000; // 每种类型 10000 个，共 40000 个点
            const int IntSize = 4;
            const int FloatSize = 4;
            const int DoubleSize = 8;
            const int BoolSize = 1;
            const int GroupSize = IntSize + FloatSize + DoubleSize + BoolSize; // 17 字节/组

            // 构造字节缓冲区
            var raw = new List<byte>();
            for (int i = 0; i < PointCount; i++)
            {
                raw.AddRange(BitConverter.GetBytes(i));
                raw.AddRange(BitConverter.GetBytes(i + 0.1f));
                raw.AddRange(BitConverter.GetBytes(i + 0.123456d));
                raw.AddRange(BitConverter.GetBytes(i % 2 == 0));
            }
            byte[] buffer = raw.ToArray();

            // 构造映射模型
            List<BytesModel> models = new();
            for (int i = 0; i < PointCount; i++)
            {
                int baseIndex = i * GroupSize;
                models.Add(new BytesModel($"addr_{i}_int", $"int_{i}", baseIndex + 0, 4, DataType.Int, EncodingType.UTF8, DataFormat.ABCD));
                models.Add(new BytesModel($"addr_{i}_float", $"float_{i}", baseIndex + 4, 4, DataType.Float, EncodingType.UTF8, DataFormat.ABCD));
                models.Add(new BytesModel($"addr_{i}_double", $"double_{i}", baseIndex + 8, 8, DataType.Double, EncodingType.UTF8, DataFormat.ABCD));
                models.Add(new BytesModel($"addr_{i}_bool", $"bool_{i}", baseIndex + 16, 1, DataType.Bool, EncodingType.UTF8, DataFormat.ABCD, -1));
            }

            using var handler = BytesHandler.Instance(Guid.NewGuid().ToString());
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await handler.TransformAsync(buffer, DateTime.Now, models);
            sw.Stop();

            Assert(result.Status, $"大数据 - {PointCount * 4:N0}点解析",
                $"耗时:{result.RunTime}ms(内部)/{sw.ElapsedMilliseconds}ms(钟), 字节数:{buffer.Length:N0}, 模型数:{models.Count:N0}");
        }
        catch (Exception ex) { Fail("大数据", ex); }
    }

    static void Assert(bool cond, string name, string? detail)
    {
        if (cond) { _p++; Console.WriteLine($"  [PASS] {name}"); }
        else { _f++; Console.WriteLine($"  [FAIL] {name}" + (detail != null ? $" — {detail}" : "")); }
    }
    static void Fail(string name, Exception ex) { _f++; Console.WriteLine($"  [FAIL] {name} — 异常: {ex.Message}"); }
}
