using Snet.Core.extend;
using Snet.Core.handler;
using Snet.Core.packer;
using Snet.Model.data;
using Snet.Model.@enum;
using System.Collections.Concurrent;

namespace Snet.Core.Samples;

class Program
{
    #region 组包全量验证  
    static int _通过, _失败;

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 地址组包全面验证 ===\n");

        验证长度计算();
        验证String组包();
        验证数组组包();
        验证混合组包();
        验证Melsec十六进制();
        验证MelsecA4C与非法设备码();
        验证位设备字节映射();
        验证超限防护();
        验证Modbus富地址();
        验证驱动语义Packer();
        验证新协议组包();
        验证性能();
        验证跨区类型降级();
        验证2026年8月全量审查修复();
        验证去重键双类型();
        验证全链路组包解包();
        验证修复问题集();
        验证倍福组包();

        Console.WriteLine($"\n=== 结果: 通过 {_通过}, 失败 {_失败} ===");
        if (_失败 > 0) Environment.Exit(1);
    }

    static void 断言(bool cond, string name)
    {
        if (cond) { _通过++; }
        else { _失败++; Console.WriteLine($"  ❌ 失败: {name}"); }
    }

    /// <summary>未组包点识别：AddressDescribe 现在原样传出（可能为空），组包批固定 "packer - N" → 非 packer 批即未组包</summary>
    static bool 是未组包(AddressDetails a) => a.AddressDescribe?.StartsWith("packer") != true;

    static Address 建地址(params (string, DataType, ushort, EncodingType?)[] arr)
    {
        var list = new List<AddressDetails>();
        foreach (var (n, t, len, enc) in arr)
            list.Add(new AddressDetails(n, t, len) { EncodingType = enc ?? EncodingType.UTF8, AddressType = AddressType.Reality });
        return new Address(list);
    }

    static void 验证长度计算()
    {
        Console.WriteLine("--- 长度计算验证 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        var addr = 建地址(
            ("DB1.DBB0", DataType.Bool, 1, null),
            ("DB1.DBB1", DataType.String, 10, EncodingType.ANSI),
            ("DB1.DBB2", DataType.String, 10, EncodingType.Unicode),
            ("DB1.DBB3", DataType.Short, 1, null),
            ("DB1.DBB4", DataType.Int, 1, null),
            ("DB1.DBB5", DataType.Double, 1, null)
        );
        var r = packer.Pack(addr, 0, DataFormat.ABCD)!;
        var models = (List<BytesModel>)r.AddressArray[0].AddressExtendParam!;
        var byName = models.ToDictionary(m => m.Address);

        断言(byName["DB1.DBB0"].Length == 1, "Bool → 1 字节");
        断言(byName["DB1.DBB1"].Length == 10, $"String ANSI×10 → 10 (实际 {byName["DB1.DBB1"].Length})");
        断言(byName["DB1.DBB2"].Length == 20, $"String Unicode×10 → 20 (实际 {byName["DB1.DBB2"].Length})");
        断言(byName["DB1.DBB3"].Length == 2, "Short → 2");
        断言(byName["DB1.DBB4"].Length == 4, "Int → 4");
        断言(byName["DB1.DBB5"].Length == 8, "Double → 8");
    }

    static void 验证String组包()
    {
        Console.WriteLine("--- String 编码保留验证 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        var addr = 建地址(
            ("DB1.DBB0", DataType.String, 32, EncodingType.GB2312),
            ("DB1.DBB32", DataType.String, 16, EncodingType.Unicode)
        );
        var r = packer.Pack(addr, 0, DataFormat.ABCD)!;
        var models = (List<BytesModel>)r.AddressArray[0].AddressExtendParam!;
        var byName = models.ToDictionary(m => m.Address);

        断言(byName["DB1.DBB0"].EncodingType == EncodingType.GB2312, "保留 GB2312");
        断言(byName["DB1.DBB0"].Length == 32, "GB2312×32 → 32");
        断言(byName["DB1.DBB32"].EncodingType == EncodingType.Unicode, "保留 Unicode");
        断言(byName["DB1.DBB32"].Length == 32, "Unicode×16 → 32");
    }

    static void 验证数组组包()
    {
        Console.WriteLine("--- 数组类型走未组包路径验证 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        var addr = 建地址(
            ("DB1.DBB0", DataType.ByteArray, 64, null),
            ("DB1.DBB64", DataType.IntArray, 10, null),
            ("DB1.DBB104", DataType.ShortArray, 8, null),
            ("DB1.DBB120", DataType.DoubleArray, 4, null),
            ("DB1.DBB152", DataType.BoolArray, 10, null),
            ("DB1.DBB200", DataType.Int, 1, null) // 标量仍参与组包
        );
        var r = packer.Pack(addr, 0, DataFormat.ABCD)!;

        // 未组包条目 AddressDescribe 原样传出（传啥返啥，空则空）
        var addrDesc = 建地址(("DB1.DBB300", DataType.IntArray, 4, null));
        addrDesc.AddressArray[0].AddressDescribe = "我的描述";
        var rDesc = packer.Pack(addrDesc, 0, DataFormat.ABCD)!;
        断言(rDesc.AddressArray[0].AddressDescribe == "我的描述", $"未组包保留原 AddressDescribe，实际 {rDesc.AddressArray[0].AddressDescribe}");
        var rDesc2 = packer.Pack(建地址(("DB1.DBB300", DataType.IntArray, 4, null)), 0, DataFormat.ABCD)!;
        断言(rDesc2.AddressArray[0].AddressDescribe == null, "未组包空 AddressDescribe 原样传出（不标记）");

        // 未组包点全字段原样保留（传啥返啥）：编码/别名/属性名/SN/地址类型
        // （用 IntArray 测——数组不可组包，走 no packer 路径）
        var addrFull = 建地址(("DB1.DBB400", DataType.IntArray, 4, null));
        var full = addrFull.AddressArray[0];
        full.EncodingType = EncodingType.GB2312;
        full.AddressAnotherName = "别名";
        full.AddressPropertyName = "属性";
        full.AddressType = AddressType.VirtualStatic;
        full.IsEnable = false;
        var rFull = packer.Pack(addrFull, 0, DataFormat.ABCD)!;
        var outFull = rFull.AddressArray[0];
        断言(outFull.EncodingType == EncodingType.GB2312, $"未组包保留 EncodingType，实际 {outFull.EncodingType}");
        断言(outFull.AddressAnotherName == "别名" && outFull.AddressPropertyName == "属性", "未组包保留别名/属性名");
        断言(outFull.SN == full.SN, "未组包保留 SN");
        断言(outFull.AddressType == AddressType.VirtualStatic, $"未组包保留 AddressType，实际 {outFull.AddressType}");
        断言(outFull.IsEnable == false, "未组包保留 IsEnable");

        int 批次 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        int 未组包 = r.AddressArray.Count(是未组包);
        Console.WriteLine($"  批次:{批次} 未组包:{未组包}");

        断言(批次 == 1, "仅 Int 标量参与组包 → 1 批");
        断言(未组包 == 5, "5 个数组类型全部走未组包路径");
        断言(r.AddressArray.All(a => !是未组包(a) || a.AddressExtendParam != null),
            "未组包条目保留原始数据");
    }

    static void 验证混合组包()
    {
        Console.WriteLine("--- 混合类型+区域+超限拆分验证 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        var addr = 建地址(
            ("DB1.DBB0", DataType.Int, 1, null),
            ("DB1.DBB4", DataType.String, 200, EncodingType.ANSI),
            ("DB1.DBB204", DataType.Bool, 1, null),
            ("DB2.DBB0", DataType.Float, 1, null),
            ("INVALID_ADDR", DataType.Short, 1, null)
        );
        var r = packer.Pack(addr, 0, DataFormat.ABCD)!;

        int 批次 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        int 未组包 = r.AddressArray.Count(是未组包);
        Console.WriteLine($"  批次:{批次} 未组包:{未组包}");
        断言(批次 >= 2, "DB1 超 240 应拆 2 批");
        断言(未组包 == 1, "INVALID_ADDR 走未组包");

        var invalid = r.AddressArray.First(是未组包);
        断言(invalid.AddressDataType == DataType.Short, "未组包保留 Short 类型");
    }

    static void 验证Melsec十六进制()
    {
        Console.WriteLine("--- Melsec X/Y 十六进制解析 ---");
        var p = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;

        // X10 是十六进制 16 → 字1位0 → 字节偏移 2，位 0
        var (bi, bit) = p.ParseAddress("X10");
        断言(bi == 2 && bit == 0, $"X10(hex=16) → (2,0)，实际 ({bi},{bit})");

        // X0 十六进制 0
        (bi, bit) = p.ParseAddress("X0");
        断言(bi == 0 && bit == 0, $"X0 → (0,0)，实际 ({bi},{bit})");

        // M 是十进制：M50 → 字3位2 → (6,2)
        (bi, bit) = p.ParseAddress("M50");
        断言(bi == 6 && bit == 2, $"M50(dec=50) → (6,2)，实际 ({bi},{bit})");

        // D 字设备十进制 ×2
        (bi, _) = p.ParseAddress("D100");
        断言(bi == 200, $"D100 → 200，实际 {bi}");

        // W 字设备十六进制 ×2：W10 = 16 → 32
        (bi, _) = p.ParseAddress("W10");
        断言(bi == 32, $"W10(hex=16)×2 → 32，实际 {bi}");
    }

    /// <summary>
    /// 验证新增 A4C 串口/TCP 协议复用标准 Melsec 地址模型，并确保驱动不支持的设备代码不会被错误合并进批次。
    /// </summary>
    static void 验证MelsecA4C与非法设备码()
    {
        Console.WriteLine("--- Melsec A4C 映射与非法设备码降级 ---");
        断言(PackerHandler.CanAutoPack("MelsecA4CNet") && PackerHandler.CanAutoPack("MelsecA4CNetOverTcp"),
            "MelsecA4CNet/MelsecA4CNetOverTcp 均已映射到 Mitsubishi Packer");

        var packer = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        断言(packer.ParseAddress("D100").ByteIndex == 200, "A4C 复用 Melsec D100 字地址模型");
        断言(packer.GetRegionKey("D100") == "D", "A4C 复用 Melsec D 区域键");

        var result = new PackerHandler(Guid.NewGuid().ToString()).AddressAutoPackOrPassthrough(
            建地址(("D100", DataType.Short, 1, null), ("D101", DataType.Short, 1, null)),
            "MelsecA4CNetOverTcp", 0, DataFormat.ABCD)!;
        断言(result.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true) == 1,
            "A4C TCP 连续 D 字地址可合并为一个批次");

        断言(packer.ParseAddress("Q100").ByteIndex < 0 && packer.GetRegionKey("Q100") == string.Empty,
            "驱动不支持的 Melsec Q 设备代码不解析、不生成区域键");
        var invalid = packer.Pack(建地址(("Q100", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        断言(invalid.AddressArray.Count == 1 && 是未组包(invalid.AddressArray[0]),
            "驱动不支持的 Melsec Q100 原样降级且不丢点");
    }

    static void 验证位设备字节映射()
    {
        Console.WriteLine("--- /16 位设备 ByteIndex/BitIndex 映射 ---");
        var melsec = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        var fins = PackerFactory.GetPacker(ProtocolFamily.Omron)!;

        // M8：字0高位字节 → 字节1，位0（不再越界）
        var (bi, bit) = melsec.ParseAddress("M8");
        断言(bi == 1 && bit == 0, $"M8 → (1,0)，实际 ({bi},{bit})");
        // M15：字0高位字节 → 字节1，位7
        (bi, bit) = melsec.ParseAddress("M15");
        断言(bi == 1 && bit == 7, $"M15 → (1,7)，实际 ({bi},{bit})");
        // M16：字1低位字节 → 字节2，位0
        (bi, bit) = melsec.ParseAddress("M16");
        断言(bi == 2 && bit == 0, $"M16 → (2,0)，实际 ({bi},{bit})");

        // FINS CIO100.08 → 绝对位号 100×16+8 = 1608（位读命令 0101，1 位 1 字节）
        (bi, bit) = fins.ParseAddress("CIO100.08");
        断言(bi == 1608 && bit == 0, $"CIO100.08 → (1608,0)，实际 ({bi},{bit})");

        // SS/SC 位区（驱动 MelsecMcDataType isWord=1）：SS0 → (0,0)
        (bi, bit) = melsec.ParseAddress("SS0");
        断言(bi == 0 && bit == 0, $"SS0(位区) → (0,0)，实际 ({bi},{bit})");

        // FINS TIM/CNT 带点号 = 位读接点（TIM100.0 → 绝对位号 1600）
        (bi, bit) = fins.ParseAddress("TIM100.0");
        断言(bi == 1600 && bit == 0, $"TIM100.0(位读) → (1600,0)，实际 ({bi},{bit})");
        断言(fins.GetRegionKey("TIM100.0") == "TIM_BIT", $"TIM100.0 区域 = TIM_BIT，实际 {fins.GetRegionKey("TIM100.0")}");

        // 组包验证 M8 Bool 能正确落在批次内（遍历所有批次查找）
        var addr = 建地址(("D0", DataType.Int, 1, null), ("M8", DataType.Bool, 1, null), ("M15", DataType.Bool, 1, null));
        var r = melsec.Pack(addr, 0, DataFormat.ABCD)!;
        BytesModel? m8 = null;
        foreach (var d in r.AddressArray)
            if (d.AddressExtendParam is List<BytesModel> ms)
                m8 = ms.FirstOrDefault(m => m.Address == "M8") ?? m8;
        断言(m8 != null, "M8 出现在组包结果中");
        断言(m8 != null && m8.StartBit >= 0, "M8 组包 StartBit 非负");
    }

    static void 验证超限防护()
    {
        Console.WriteLine("--- 超限地址防护 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        // 超 maxByteLength 的地址走未组包路径
        var addr = 建地址(
            ("DB1.DBB0", DataType.Int, 1, null),
            ("DB1.DBB4", DataType.String, 300, EncodingType.ANSI)  // 300 > 240 默认上限
        );
        var r = packer.Pack(addr, 0, DataFormat.ABCD)!;
        int 未组包 = r.AddressArray.Count(是未组包);
        断言(未组包 == 1, "超限 String(300>240) 走未组包路径");

        // 组包批次总长不超过上限
        foreach (var d in r.AddressArray.Where(a => a.AddressDescribe?.StartsWith("packer") == true))
            断言(d.Length <= 240, $"批次长度 {d.Length} ≤ 240");
    }

    static void 验证Modbus富地址()
    {
        Console.WriteLine("--- Modbus 富地址格式验证 ---");
        var p = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;

        // 零基地址
        var (bi, bit) = p.ParseAddress("100");
        断言(bi == 100, $"\"100\" → 100，实际 {bi}");
        // 十六进制 100H = 256
        (bi, _) = p.ParseAddress("100H");
        断言(bi == 256, $"\"100H\" → 256，实际 {bi}");
        // 位后缀 100.1 → bit1
        (bi, bit) = p.ParseAddress("100.1");
        断言(bi == 100 && bit == 1, $"\"100.1\" → (100,1)，实际 ({bi},{bit})");
        // 站号 + 地址
        (bi, _) = p.ParseAddress("s=2;100");
        断言(bi == 100, $"\"s=2;100\" → 100，实际 {bi}");
        // format + 地址
        (bi, _) = p.ParseAddress("format=DCBA;100");
        断言(bi == 100, $"\"format=DCBA;100\" → 100，实际 {bi}");
        // x= + w= + 地址
        (bi, _) = p.ParseAddress("x=7;w=8;100");
        断言(bi == 100, $"\"x=7;w=8;100\" → 100，实际 {bi}");
        // file=
        (bi, _) = p.ParseAddress("file=0;100");
        断言(bi == 100, $"\"file=0;100\" → 100，实际 {bi}");

        // 区域键：Word 类型无 x= → FC3；Bool 类型无 x= → FC1
        断言(p.GetRegionKey("100") == "S-1|FC3", $"Word \"100\" 区域 = FC3，实际 {p.GetRegionKey("100")}");
        断言(p.GetRegionKey("x=2;100") == "S-1|FC2", $"\"x=2;100\" 区域 = FC2，实际 {p.GetRegionKey("x=2;100")}");
        断言(p.GetRegionKey("s=2;100") == "S2|FC3", $"\"s=2;100\" 区域 = S2|FC3，实际 {p.GetRegionKey("s=2;100")}");
        断言(p.GetRegionKey("file=0;100") == "S-1|FILE0", $"\"file=0;100\" 区域 = FILE0，实际 {p.GetRegionKey("file=0;100")}");

        // 带点号 Bool（90.1）→ 寄存器内位操作 → 强制 FC3（驱动 ReadBoolHelper SplitDot 读字取位）
        var dotBool = p.Pack(建地址(
            ("90.1", DataType.Bool, 1, null),
            ("90.2", DataType.Bool, 1, null),
            ("90.3", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var dotBatch = dotBool.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(dotBatch.Length == 2, $"90.1~90.3 同寄存器 → 批长度 2（1寄存器×2字节），实际 {dotBatch.Length}");
        var dotModels = (List<BytesModel>)dotBatch.AddressExtendParam!;
        var dotByName = dotModels.ToDictionary(m => m.Address);
        // 2026-08-14 修复：寄存器 Bool 的 BoolIndex = 全局位偏移（位0-7 在低字节 → +8）
        // 90.1 → 9（低字节位1）、90.3 → 11（低字节位3）
        断言(dotByName["90.1"].StartBit == 0 && dotByName["90.1"].BoolIndex == 9 && dotByName["90.3"].BoolIndex == 11,
            $"90.1~90.3 寄存器批 偏移0 位9/11（低字节），实际 (0:{dotByName["90.1"].StartBit},{dotByName["90.1"].BoolIndex}) (2:{dotByName["90.3"].BoolIndex})");

        // 组包：Word 100 + Word 101 → FC3 同批；Bool 100（线圈）→ 降级单点 ReadBool（2026-08 全量审查修复）
        var addr = 建地址(
            ("100", DataType.Int, 1, null),
            ("101", DataType.Short, 1, null),
            ("100", DataType.Bool, 1, null),
            ("s=2;100", DataType.Int, 1, null)  // 不同站号 → 独立批
        );
        var r = p.Pack(addr, 0, DataFormat.ABCD)!;
        var models = r.AddressArray
            .Where(a => a.AddressDescribe?.StartsWith("packer") == true)
            .SelectMany(a => (List<BytesModel>)a.AddressExtendParam!)
            .ToList();
        // Word 100 → 批次内偏移0，Word 101 → 批次内偏移2（×2 字节，StartBit 是批次内偏移）
        var w100 = models.First(m => m.Address == "100" && m.BoolIndex == 0);
        var w101 = models.First(m => m.Address == "101");
        断言(w100.StartBit == 0, $"Word 100 批次内偏移0，实际 {w100.StartBit}");
        断言(w101.StartBit == 2, $"Word 101 批次内偏移2，实际 {w101.StartBit}");
        // Bool 100（FC1 线圈）→ 消费层 ByteArray 批统一走 Read（FC3 寄存器）物理错区 → 降级未组包
        var b100 = r.AddressArray.First(a => a.AddressName == "100" && 是未组包(a));
        断言(b100.AddressDataType == DataType.Bool, $"Bool 100 线圈降级未组包（单点 ReadBool FC1 正确），实际 {b100.AddressDataType}");

        // 批首归一化（2026-08 全量审查修复）：点号 Bool 批首 "90.1" 驱动 uint.Parse 抛异常 → 归一化 "90"
        断言(dotBatch.AddressName == "90", $"90.1 批首归一化为 \"90\"（驱动可解析），实际 {dotBatch.AddressName}");

        // format= 强制 DCBA 写入 BytesModel.DataFormat
        var addr2 = 建地址(("format=DCBA;100", DataType.Int, 1, null));
        var r2 = p.Pack(addr2, 0, DataFormat.ABCD)!;
        var model = r2.AddressArray
            .Where(a => a.AddressDescribe?.StartsWith("packer") == true)
            .SelectMany(a => (List<BytesModel>)a.AddressExtendParam!)
            .First();
        断言(model.DataFormat == DataFormat.DCBA, $"format=DCBA 生效，实际 {model.DataFormat}");
    }

    static void 验证驱动语义Packer()
    {
        Console.WriteLine("--- 驱动语义 Packer 验证 ---");

        // Siemens 专项（2026-08-07 全量审查核对 S7AddressData.CalculateAddressStarted）：
        // 无点号 n → 驱动位号 n×8 → 字节 n 位 0（packer 字节原址一致）；带点号 n.m → 字节 n 位 m。
        // S7 是字节地址空间协议（无位区/字区概念），Bool/Word 均按字节+位解读 → 不需跨区降级。
        var siemens = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;
        var (sbi, sbit) = siemens.ParseAddress("M10");
        断言(sbi == 10 && sbit == 0, $"Siemens M10(无点号) → (10,0)，驱动位号80展开，实际 ({sbi},{sbit})");
        (sbi, sbit) = siemens.ParseAddress("M10.3");
        断言(sbi == 10 && sbit == 3, $"Siemens M10.3 → (10,3)，实际 ({sbi},{sbit})");
        (sbi, sbit) = siemens.ParseAddress("DB1.DBX0.3");
        断言(sbi == 0 && sbit == 3, $"Siemens DB1.DBX0.3 → (0,3)，实际 ({sbi},{sbit})");
        (sbi, sbit) = siemens.ParseAddress("DB1.DBW10");
        断言(sbi == 10 && sbit == 0, $"Siemens DB1.DBW10 → (10,0)，实际 ({sbi},{sbit})");
        断言(siemens.GetRegionKey("DB2.DBX0.0") == "DB2", $"Siemens DB2 块区域 = DB2，实际 {siemens.GetRegionKey("DB2.DBX0.0")}");
        // 同字节空间不同解读（M10 Bool=字节10位0 / M10 Word=字节10起）：
        // 去重键含数据类型 → 都保留，重叠排布（同 ByteIndex → 同批偏移 0），一批读 2 字节同时正确解包两个点
        var s7r = siemens.Pack(建地址(("M10", DataType.Bool, 1, null), ("M10", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        var s7models = s7r.AddressArray.Where(a => a.AddressDescribe?.StartsWith("packer") == true)
            .SelectMany(a => a.AddressExtendParam is List<BytesModel> ms ? ms : [])
            .Where(m => m.Address == "M10").ToList();
        断言(s7models.Count == 2, $"Siemens M10 Bool+Word 都保留（去重键含类型），实际 {s7models.Count}");
        断言(s7models.Any(m => m.DataType == DataType.Bool) && s7models.Any(m => m.DataType == DataType.Int),
            "Siemens M10 Bool 与 Int 两个 BytesModel 都在（重叠解包正确）");

        // GE：R/AI/AQ 字区（×2 字节，不支持位读），I/Q/M/T/S 位区（地址号直接）
        var ge = PackerFactory.GetPacker(ProtocolFamily.GE)!;
        断言(ge.ParseAddress("R1").ByteIndex == 0, $"GE R1(字) → 0，实际 {ge.ParseAddress("R1").ByteIndex}");
        断言(ge.ParseAddress("R100").ByteIndex == 198, $"GE R100(字×2) → 198，实际 {ge.ParseAddress("R100").ByteIndex}");
        断言(ge.GetRegionKey("AI100") == "AI", "GE AI100 区域 = AI");
        断言(ge.ParseAddress("I10").ByteIndex == 9, $"GE I10(位直接) → 9，实际 {ge.ParseAddress("I10").ByteIndex}");

        // Vigor：D 字区（×2），D9000 → SD 区 -9000 后 ×2
        var vigor = PackerFactory.GetPacker(ProtocolFamily.Vigor)!;
        断言(vigor.ParseAddress("D100").ByteIndex == 200, $"Vigor D100(字×2) → 200，实际 {vigor.ParseAddress("D100").ByteIndex}");
        断言(vigor.ParseAddress("D9000").ByteIndex == 0, $"Vigor D9000 → 0（SD区-9000），实际 {vigor.ParseAddress("D9000").ByteIndex}");
        断言(vigor.GetRegionKey("D9000") == "SD", $"Vigor D9000 区域 = SD，实际 {vigor.GetRegionKey("D9000")}");
        断言(vigor.GetRegionKey("D100") == "D", "Vigor D100 区域 = D");

        // XinJE：X10 → 8（八进制，位区直接），D100 → 200（字区 ×2）
        var xinje = PackerFactory.GetPacker(ProtocolFamily.XinJE)!;
        断言(xinje.ParseAddress("X10").ByteIndex == 8, $"XinJE X10(oct 位) → 8，实际 {xinje.ParseAddress("X10").ByteIndex}");
        断言(xinje.ParseAddress("D100").ByteIndex == 200, $"XinJE D100(字×2) → 200，实际 {xinje.ParseAddress("D100").ByteIndex}");

        // Fatek：D 字区 ×2，X 位区直接
        var fatek = PackerFactory.GetPacker(ProtocolFamily.Fatek)!;
        断言(fatek.ParseAddress("D100").ByteIndex == 200, $"Fatek D100(字×2) → 200，实际 {fatek.ParseAddress("D100").ByteIndex}");
        断言(fatek.ParseAddress("X0").ByteIndex == 0, $"Fatek X0(位) → 0，实际 {fatek.ParseAddress("X0").ByteIndex}");

        // Yokogawa：D 字区 ×2，X 位区 /16 编排
        var yoko = PackerFactory.GetPacker(ProtocolFamily.Yokogawa)!;
        断言(yoko.ParseAddress("D100").ByteIndex == 200, $"Yokogawa D100(字×2) → 200，实际 {yoko.ParseAddress("D100").ByteIndex}");
        断言(yoko.ParseAddress("X17").ByteIndex == 2, $"Yokogawa X17(位/16) → 2，实际 {yoko.ParseAddress("X17").ByteIndex}");

        // Fanuc：D 字区（×2），I/Q/M 位区（地址号直接）
        var fanuc = PackerFactory.GetPacker(ProtocolFamily.Fanuc)!;
        断言(fanuc.ParseAddress("D5").ByteIndex == 10, $"FANUC D5(字×2) → 10，实际 {fanuc.ParseAddress("D5").ByteIndex}");
        断言(fanuc.ParseAddress("M100").ByteIndex == 100, $"FANUC M100(位直接) → 100，实际 {fanuc.ParseAddress("M100").ByteIndex}");
        断言(fanuc.GetRegionKey("M100") == "M", $"FANUC M100 区域 = M，实际 {fanuc.GetRegionKey("M100")}");

        // LSis：D100 → 200（字×2），MB100 → 100（位宽 B 直接）
        var lsis = PackerFactory.GetPacker(ProtocolFamily.LSis_Cnet)!;
        断言(lsis.ParseAddress("D100").ByteIndex == 200, $"LSis D100 → 200，实际 {lsis.ParseAddress("D100").ByteIndex}");
        断言(lsis.ParseAddress("MB100").ByteIndex == 100, $"LSis MB100 → 100，实际 {lsis.ParseAddress("MB100").ByteIndex}");
        断言(lsis.GetRegionKey("MB100") == "MB", $"LSis MB100 区域 = MB，实际 {lsis.GetRegionKey("MB100")}");

        // Fuji SPH：字语义 ParseAddress = 地址×2（位号 0-15 为字内位，走 GroupAndSort 的 |B 分支）
        var fuji = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        var (fbi, fbit) = fuji.ParseAddress("M1.100.3");
        断言(fbi == 200 && fbit == 0, $"Fuji M1.100.3 字 → (200,0)，实际 ({fbi},{fbit})");
        (fbi, fbit) = fuji.ParseAddress("I100.3");
        断言(fbi == 200 && fbit == 0, $"Fuji I100.3 字 → (200,0)，实际 ({fbi},{fbit})");
        断言(fuji.GetRegionKey("M1.100.3") == "M1", $"Fuji M1.100.3 区域 = M1，实际 {fuji.GetRegionKey("M1.100.3")}");

        // Yaskawa：纯数字 + 位后缀
        var yaskawa = PackerFactory.GetPacker(ProtocolFamily.Yaskawa)!;
        var (ybi, ybit) = yaskawa.ParseAddress("100.1");
        断言(ybi == 100 && ybit == 1, $"Yaskawa 100.1 → (100,1)，实际 ({ybi},{ybit})");
        断言(yaskawa.ParseAddress("100").ByteIndex == 100, $"Yaskawa 100 → 100，实际 {yaskawa.ParseAddress("100").ByteIndex}");

        // Toyo：K100 字 → 字号 32+0x100=288 → 字节 576；D100 → (4096+256)×2=8704
        var toyo = PackerFactory.GetPacker(ProtocolFamily.Toyota)!;
        断言(toyo.ParseAddress("K100").ByteIndex == 576, $"Toyo K100 字 → 576，实际 {toyo.ParseAddress("K100").ByteIndex}");
        断言(toyo.ParseAddress("D100").ByteIndex == 8704, $"Toyo D100 字 → 8704，实际 {toyo.ParseAddress("D100").ByteIndex}");
        断言(toyo.ParseAddress("K1005").ByteIndex == 8266, $"Toyo K1005 字(hex 0x1005) → 8266，实际 {toyo.ParseAddress("K1005").ByteIndex}");
        断言(toyo.GetRegionKey("EB100") == "EB9", $"Toyo EB100 区域 = EB9，实际 {toyo.GetRegionKey("EB100")}");
        断言(toyo.GetRegionKey("prg=1;K100") == "K|P1", $"Toyo prg=1;K100 区域 = K|P1，实际 {toyo.GetRegionKey("prg=1;K100")}");
        断言(toyo.GetRegionKey("U100") == "U", $"Toyo U100 区域 = U，实际 {toyo.GetRegionKey("U100")}");
        断言(toyo.GetRegionKey("GM100") == "GM", $"Toyo GM100 区域 = GM，实际 {toyo.GetRegionKey("GM100")}");

        // AllenBradley SLC：B3:0 → 0、N7:10 → 20、F8:2 → 8；带 / 位寻址不可组包
        var slc = PackerFactory.GetPacker(ProtocolFamily.AllenBradley)!;
        断言(slc.ParseAddress("B3:0").ByteIndex == 0, $"SLC B3:0 → 0，实际 {slc.ParseAddress("B3:0").ByteIndex}");
        断言(slc.ParseAddress("N7:10").ByteIndex == 20, $"SLC N7:10 → 20，实际 {slc.ParseAddress("N7:10").ByteIndex}");
        断言(slc.ParseAddress("F8:2").ByteIndex == 8, $"SLC F8:2 → 8，实际 {slc.ParseAddress("F8:2").ByteIndex}");
        // PCCC 元素宽：T/C/R = 6 字节，L = 8 字节，ST = 84 字节
        断言(slc.ParseAddress("T4:2").ByteIndex == 12, $"SLC T4:2 → 12（3字=6字节/元素），实际 {slc.ParseAddress("T4:2").ByteIndex}");
        断言(slc.ParseAddress("C5:1").ByteIndex == 6, $"SLC C5:1 → 6，实际 {slc.ParseAddress("C5:1").ByteIndex}");
        断言(slc.ParseAddress("L9:2").ByteIndex == 16, $"SLC L9:2 → 16（4字=8字节/元素），实际 {slc.ParseAddress("L9:2").ByteIndex}");
        断言(slc.ParseAddress("ST5:1").ByteIndex == 84, $"SLC ST5:1 → 84（2长度+82字符），实际 {slc.ParseAddress("ST5:1").ByteIndex}");
        断言(slc.ParseAddress("B3:0/5").ByteIndex == -1, "SLC B3:0/5 带位寻址不可组包");
        断言(slc.GetRegionKey("B3:0") == "B3", $"SLC B3:0 区域 = B3，实际 {slc.GetRegionKey("B3:0")}");
        断言(slc.GetRegionKey("I:0") == "I1", $"SLC I:0 区域 = I1（默认文件号1），实际 {slc.GetRegionKey("I:0")}");
        断言(slc.GetRegionKey("ST5:0") == "ST5", $"SLC ST5:0 区域 = ST5，实际 {slc.GetRegionKey("ST5:0")}");

        // FxLinks：位设备每位 1 字节（BR 命令 ASCII），位号直接作字节偏移；X/Y 八进制
        var fxl = PackerFactory.GetPacker(ProtocolFamily.MitsubishiFx)!;
        var (xbi, xbit) = fxl.ParseAddress("X10");
        断言(xbi == 8 && xbit == 0, $"FxLinks X10(oct 8) → (8,0)，实际 ({xbi},{xbit})");
        (xbi, xbit) = fxl.ParseAddress("Y17");
        断言(xbi == 15 && xbit == 0, $"FxLinks Y17(oct 15) → (15,0)，实际 ({xbi},{xbit})");
        断言(fxl.ParseAddress("D100").ByteIndex == 200, $"FxLinks D100 → 200，实际 {fxl.ParseAddress("D100").ByteIndex}");
        (xbi, xbit) = fxl.ParseAddress("M100");
        断言(xbi == 100 && xbit == 0, $"FxLinks M100 → (100,0)，实际 ({xbi},{xbit})");
        断言(fxl.GetRegionKey("TS50") == "TS", $"FxLinks TS50 区域 = TS，实际 {fxl.GetRegionKey("TS50")}");
        断言(fxl.GetRegionKey("s=2;D100") == "S2|D", $"FxLinks s=2;D100 区域 = S2|D，实际 {fxl.GetRegionKey("s=2;D100")}");

        // Fuji SPB / CommandSettingType / SPH 补全（SPH 字 = 地址×2，位 = 字内位）
        (fbi, fbit) = fuji.ParseAddress("M100");
        断言(fbi == 200 && fbit == 0, $"Fuji SPB M100 → (200,0)，实际 ({fbi},{fbit})");
        断言(fuji.ParseAddress("X10").ByteIndex == 20, $"Fuji SPB X10 → 20，实际 {fuji.ParseAddress("X10").ByteIndex}");
        断言(fuji.ParseAddress("W9.100").ByteIndex == 200, $"Fuji CST W9.100 → 200，实际 {fuji.ParseAddress("W9.100").ByteIndex}");
        断言(fuji.ParseAddress("TS100").ByteIndex == 200, $"Fuji CST TS100 → 200，实际 {fuji.ParseAddress("TS100").ByteIndex}");
        (fbi, fbit) = fuji.ParseAddress("M1.100");
        断言(fbi == 200 && fbit == 0, $"Fuji SPH M1.100 字 → (200,0)，实际 ({fbi},{fbit})");
        // M2 非 SPH 类型（SPH 只支持 1/3/10）→ 按 SPB 解释（M 区地址2 位100，驱动 FujiSPBAddress 接受）
        (fbi, fbit) = fuji.ParseAddress("M2.100");
        断言(fbi == 4 && fbit == 0, $"Fuji M2.100 非 SPH 类型 → SPB M区地址2 → (4,0)，实际 ({fbi},{fbit})");
        (fbi, fbit) = fuji.ParseAddress("I100");
        断言(fbi == 200 && fbit == 0, $"Fuji SPH I100 字 → (200,0)，实际 ({fbi},{fbit})");
        断言(fuji.GetRegionKey("M100") == "M", $"Fuji SPB M100 区域 = M，实际 {fuji.GetRegionKey("M100")}");
        断言(fuji.GetRegionKey("W9.100") == "W9", $"Fuji CST W9.100 区域 = W9，实际 {fuji.GetRegionKey("W9.100")}");
        断言(fuji.GetRegionKey("TS100") == "TS", $"Fuji CST TS100 区域 = TS，实际 {fuji.GetRegionKey("TS100")}");

        // Fuji SPB 无点号 Bool：X/Y/M/L/TC/CC = 绝对位号（M100=位100=字节12位4，M108=位108=字节13位4）
        var spbM = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        var spbMs = (List<BytesModel>)spbM.Pack(建地址(
            ("M100", DataType.Bool, 1, null),
            ("M108", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!.AddressArray[0].AddressExtendParam!;
        断言(spbMs[0].BoolIndex == 4 && spbMs[1].StartBit == 1 && spbMs[1].BoolIndex == 4,
            $"Fuji SPB M100/M108 Bool → 绝对位号（字节差1 位4），实际 (0:{spbMs[0].StartBit},{spbMs[0].BoolIndex}) (1:{spbMs[1].StartBit},{spbMs[1].BoolIndex})");
    }

    static void 验证新协议组包()
    {
        Console.WriteLine("--- 新增协议组包验证 ---");

        // Toyo 字：K100-K102 连续 3 字（Short 2 字节）→ 1 批 6 字节
        var toyo = PackerFactory.GetPacker(ProtocolFamily.Toyota)!;
        var r = toyo.Pack(建地址(
            ("K100", DataType.Short, 1, null),
            ("K101", DataType.Short, 1, null),
            ("K102", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        int 批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"Toyo K100-K102 连续字 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 6, $"Toyo 3 字批长度 = 6 字节，实际 {r.AddressArray[0].Length}");
        断言(r.AddressArray[0].AddressName == "K100", $"Toyo 批首地址 = K100，实际 {r.AddressArray[0].AddressName}");

        // Toyo Bool：K1000-K100F 全部降级（2026-08 全量审查修复：消费层字路径把位地址当纯 hex 字号解析
        // "K1005"→字4133 vs 模型字288、点号 "K100.5" 抛 FormatException → 位批必然错位/读失败 → 单点 ReadBool）
        var boolAddr = new List<(string, DataType, ushort, EncodingType?)>(16);
        for (int i = 0; i < 16; i++) boolAddr.Add(($"K100{i:X}", DataType.Bool, 1, null));
        r = toyo.Pack(建地址(boolAddr.ToArray()), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        int 降级 = r.AddressArray.Count(是未组包);
        断言(批数 == 0 && 降级 == 16, $"Toyo K1000-K100F Bool 全部降级未组包（16 点），实际 批{批数} 降级{降级}");

        // Toyo 位/字混批隔离：K100 字组包、K1000 位降级 → 1 批 + 1 未组包
        r = toyo.Pack(建地址(
            ("K100", DataType.Short, 1, null),
            ("K1000", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        降级 = r.AddressArray.Count(是未组包);
        断言(批数 == 1 && 降级 == 1, $"Toyo K100 字组包 + K1000 位降级 → 1 批 1 未组包，实际 批{批数} 降级{降级}");

        // AllenBradley：N7:0-N7:2 连续 3 字 → 1 批 6 字节；B3:0/5 带位寻址 → 不可组包
        var slc = PackerFactory.GetPacker(ProtocolFamily.AllenBradley)!;
        r = slc.Pack(建地址(
            ("N7:0", DataType.Short, 1, null),
            ("N7:1", DataType.Short, 1, null),
            ("N7:2", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"SLC N7:0-N7:2 连续字 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 6, $"SLC 3 字批长度 = 6 字节，实际 {r.AddressArray[0].Length}");

        r = slc.Pack(建地址(
            ("B3:0/5", DataType.Bool, 1, null),
            ("B3:0/6", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        int 未组包 = r.AddressArray.Count(是未组包);
        断言(未组包 == 2, $"SLC 带 / 位寻址全部不可组包（原样保留），实际 {未组包}");

        // FxLinks：D100-D102 连续字 → 1 批 6 字节；X0-X7 连续位 → 1 批 1 字节
        var fxl = PackerFactory.GetPacker(ProtocolFamily.MitsubishiFx)!;
        r = fxl.Pack(建地址(
            ("D100", DataType.Short, 1, null),
            ("D101", DataType.Short, 1, null),
            ("D102", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"FxLinks D100-D102 连续字 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 6, $"FxLinks 3 字批长度 = 6 字节，实际 {r.AddressArray[0].Length}");

        r = fxl.Pack(建地址(
            ("X0", DataType.Bool, 1, null),
            ("X1", DataType.Bool, 1, null),
            ("X2", DataType.Bool, 1, null),
            ("X3", DataType.Bool, 1, null),
            ("X4", DataType.Bool, 1, null),
            ("X5", DataType.Bool, 1, null),
            ("X6", DataType.Bool, 1, null),
            ("X7", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"FxLinks X0-X7 连续 8 位 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 1, $"FxLinks 8 位批长度 = 1 字节（WR 字读位流 8 位/字节，2026-08 全量审查修复），实际 {r.AddressArray[0].Length}");
        var fxModels = (List<BytesModel>)r.AddressArray[0].AddressExtendParam!;
        var fxByName = fxModels.ToDictionary(m => m.Address);
        断言(fxByName["X0"].StartBit == 0 && fxByName["X0"].BoolIndex == 0 && fxByName["X7"].BoolIndex == 7,
            $"FxLinks 位流批内偏移=位号差（X0→byte0 bit0、X7→byte0 bit7），实际 (0:{fxByName["X0"].StartBit},{fxByName["X0"].BoolIndex}) (7:{fxByName["X7"].BoolIndex})");

        // Fuji SPB：M100-M102 连续字 → 1 批 6 字节；M100.0-M100.7 连续位 → 1 批 1 字节
        var fuji = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        r = fuji.Pack(建地址(
            ("M100", DataType.Short, 1, null),
            ("M101", DataType.Short, 1, null),
            ("M102", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"Fuji SPB M100-M102 连续字 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 6, $"Fuji SPB 3 字批长度 = 6 字节，实际 {r.AddressArray[0].Length}");

        r = fuji.Pack(建地址(
            ("M100.0", DataType.Bool, 1, null),
            ("M100.1", DataType.Bool, 1, null),
            ("M100.2", DataType.Bool, 1, null),
            ("M100.3", DataType.Bool, 1, null),
            ("M100.4", DataType.Bool, 1, null),
            ("M100.5", DataType.Bool, 1, null),
            ("M100.6", DataType.Bool, 1, null),
            ("M100.7", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"Fuji SPB M100.0-M100.7 连续 8 位 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 1, $"Fuji SPB 8 位批长度 = 1 字节，实际 {r.AddressArray[0].Length}");

        // Fuji SPH Bool：M1.100.5-M1.100.12（字 100 内位 5-12）→ 1 批 2 字节，位跨字节正确
        r = fuji.Pack(建地址(
            ("M1.100.5", DataType.Bool, 1, null),
            ("M1.100.6", DataType.Bool, 1, null),
            ("M1.100.7", DataType.Bool, 1, null),
            ("M1.100.8", DataType.Bool, 1, null),
            ("M1.100.9", DataType.Bool, 1, null),
            ("M1.100.10", DataType.Bool, 1, null),
            ("M1.100.11", DataType.Bool, 1, null),
            ("M1.100.12", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"Fuji SPH M1.100.5-12 连续 8 位 → 1 批，实际 {批数}");
        断言(r.AddressArray[0].Length == 2, $"Fuji SPH 8 位批长度 = 2 字节，实际 {r.AddressArray[0].Length}");
        var models = (List<BytesModel>)r.AddressArray[0].AddressExtendParam!;
        var byName = models.ToDictionary(m => m.Address);
        断言(byName["M1.100.5"].BoolIndex == 5 && byName["M1.100.8"].BoolIndex == 0, "Fuji SPH 位5 与 位8 分别落在两个字节内");

        // 分帧协议 PhysicalMaxBytes = 驱动 ushort 上限 131070；无分帧协议 = MaxReadLength
        var 分帧族 = new[] { ProtocolFamily.Modbus, ProtocolFamily.Mitsubishi, ProtocolFamily.Omron, ProtocolFamily.Yokogawa,
            ProtocolFamily.Panasonic, ProtocolFamily.Fatek, ProtocolFamily.XinJE, ProtocolFamily.Vigor, ProtocolFamily.LSis_Cnet,
            ProtocolFamily.MitsubishiFx, ProtocolFamily.Keyence };
        foreach (var f in 分帧族)
            断言(PackerFactory.GetPacker(f)!.PhysicalMaxBytes == 131070, $"{f} PhysicalMaxBytes = 131070（驱动 ushort 上限）");
        var 无分帧族 = new[] { ProtocolFamily.GE, ProtocolFamily.Fanuc, ProtocolFamily.Toyota, ProtocolFamily.Cimon,
            ProtocolFamily.AllenBradley, ProtocolFamily.Fuji, ProtocolFamily.Yaskawa };
        foreach (var f in 无分帧族)
        {
            var p = PackerFactory.GetPacker(f)!;
            断言(p.PhysicalMaxBytes == p.MaxReadLength, $"{f} PhysicalMaxBytes = MaxReadLength（无分帧单帧上限）");
        }

        // Modbus PhysicalMaxBytes = 驱动 ushort 上限（65535 寄存器 = 131070 字节），传 10000 生效
        var mb = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        断言(mb.PhysicalMaxBytes == 131070, $"Modbus PhysicalMaxBytes = 131070（驱动 ushort 上限），实际 {mb.PhysicalMaxBytes}");

        // 组包后 BytesModel.DataType 保留原类型（BytesHandler 解包 switch 不支持 ByteArray → 必须原类型）
        var typeAddr = 建地址(
            ("1", DataType.Int32, 1, null),
            ("90.1", DataType.Bool, 1, null),
            ("200", DataType.Float, 1, null));
        r = mb.Pack(typeAddr, 10000, DataFormat.ABCD)!;
        var typeModels = r.AddressArray
            .Where(a => a.AddressDescribe?.StartsWith("packer") == true)
            .SelectMany(a => (List<BytesModel>)a.AddressExtendParam!)
            .ToList();
        var typeByName = typeModels.ToDictionary(m => m.Address);
        断言(typeByName["1"].DataType == DataType.Int32, $"组包后 1 → Int32（非 ByteArray），实际 {typeByName["1"].DataType}");
        断言(typeByName["90.1"].DataType == DataType.Bool, $"组包后 90.1 → Bool（非 ByteArray），实际 {typeByName["90.1"].DataType}");
        断言(typeByName["200"].DataType == DataType.Float, $"组包后 200 → Float（非 ByteArray），实际 {typeByName["200"].DataType}");
        断言(typeModels.All(m => m.DataType != DataType.ByteArray), "所有批内模型均非 ByteArray（可正确解包）");
        var mbAddr = 建地址(
            ("100", DataType.Int32, 1, null),
            ("200", DataType.Int32, 1, null),
            ("300", DataType.Int32, 1, null),
            ("400", DataType.Int32, 1, null),
            ("500", DataType.Int32, 1, null),
            ("600", DataType.Int32, 1, null),
            ("700", DataType.Int32, 1, null),
            ("800", DataType.Int32, 1, null),
            ("900", DataType.Int32, 1, null),
            ("1000", DataType.Int32, 1, null),
            ("1010", DataType.Int32, 1, null));
        r = mb.Pack(mbAddr, 10000, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"Modbus maxByteLength=10000 → 1 批（PhysicalMaxBytes=131070 生效），实际 {批数}");
        断言(r.AddressArray[0].Length == 1824, $"Modbus 11 点 Int32 批长度 = 1824 字节，实际 {r.AddressArray[0].Length}");

        // FxLinks 位/字分离：M100 Bool（位读）与 M100 Word（位区字读错位 → 降级）同地址 → 1 批 + 1 未组包
        r = fxl.Pack(建地址(
            ("M100", DataType.Bool, 1, null),
            ("M100", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1 && r.AddressArray.Count(是未组包) == 1,
            $"FxLinks M100 Bool 组包 + Word 降级（位区字读 16 位窗口错位，2026-08 全量审查修复），实际 批{批数}");

        // Toyo 短 Bool 地址（驱动补 0 语义）：K5 = 字 K0 位 5 → 全部降级（2026-08 全量审查修复）
        r = toyo.Pack(建地址(
            ("K5", DataType.Bool, 1, null),
            ("K6", DataType.Bool, 1, null),
            ("K7", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 0 && r.AddressArray.Count(是未组包) == 3,
            $"Toyo K5-K7 短位地址 → 全部降级未组包（消费层字路径位地址错位），实际 批{批数}");
    }

    static void 验证性能()
    {
        Console.WriteLine("--- 1000 地址性能验证 ---");
        var packer = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;

        var rng = new Random(42);
        var types = new[] { DataType.Bool, DataType.Short, DataType.Int, DataType.Float, DataType.String };
        var list = new List<(string, DataType, ushort, EncodingType?)>(1000);
        for (int db = 1; db <= 5; db++)
            for (int i = 0; i < 200; i++)
            {
                var t = types[rng.Next(5)];
                int off = rng.Next(0, 5000);
                if (t == DataType.String)
                    list.Add(($"DB{db}.DBB{off}", t, (ushort)rng.Next(1, 64), EncodingType.UTF8));
                else
                    list.Add(($"DB{db}.DBB{off}", t, 1, null));
            }

        var addr = 建地址(list.ToArray());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = packer.Pack(addr, 0, DataFormat.ABCD);
        sw.Stop();

        Console.WriteLine($"  1000 地址混合类型: {sw.ElapsedMilliseconds}ms, 批次: {r!.AddressArray.Count}");
        断言(sw.ElapsedMilliseconds < 1000, "1000 地址 < 1s");
        断言(r.AddressArray.All(a => a.AddressExtendParam is List<BytesModel> { Count: > 0 }), "所有批次含 BytesModel");
    }


    /// <summary>
    /// 跨区类型降级验证（2026-08-07 全量审查新增）：
    /// 位区 Word / 字区 Bool 与驱动读命令不兼容的地址必须走 no packer 原样保留，
    /// 同地址 Bool+Word 同传不丢点（|B/|W 分离）。
    /// </summary>
    static void 验证跨区类型降级()
    {
        Console.WriteLine("--- 跨区类型降级验证 ---");
        var 组包数 = (Address r) => r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        var 降级数 = (Address r) => r.AddressArray.Count(是未组包);

        // Melsec：字区 Bool（D100）位读命令不合法 → 降级；位区 Word（M100）字读错位 → 降级
        var melsec = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        var r1 = melsec.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r1) == 0 && 降级数(r1) == 1, "Melsec D100 Bool 降级（字区位读不合法）");
        var r2 = melsec.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r2) == 0 && 降级数(r2) == 1, "Melsec M100 Word 降级（位区字读错位）");
        // 同地址 Bool+Word 不丢点：Bool 组包、Word 降级
        var r3 = melsec.Pack(建地址(("M100", DataType.Bool, 1, null), ("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r3.AddressArray.Count == 2 && 组包数(r3) == 1 && 降级数(r3) == 1, "Melsec M100 Bool+Word 同传都保留（不丢点）");

        // Keyence：同 MC 语义
        var keyence = PackerFactory.GetPacker(ProtocolFamily.Keyence)!;
        var r4 = keyence.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r4) == 0 && 降级数(r4) == 1, "Keyence D100 Bool 降级");
        var r5 = keyence.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r5) == 0 && 降级数(r5) == 1, "Keyence M100 Word 降级");

        // Panasonic：Bool 仅 X/Y/R/L（驱动显式报错）；X100 Bool+Word 同传 2 条都保留
        var panasonic = PackerFactory.GetPacker(ProtocolFamily.Panasonic)!;
        var r6 = panasonic.Pack(建地址(("DT100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r6) == 0 && 降级数(r6) == 1, "Panasonic DT100 Bool 降级（Bit read only X,Y,R,L）");
        var r7 = panasonic.Pack(建地址(("X100", DataType.Bool, 1, null), ("X100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r7.AddressArray.Count == 2 && 组包数(r7) == 2, "Panasonic X100 Bool+Word 同传 2 批都保留");

        // GE：字区 R/AI/AQ Bool 降级（驱动不支持位读）
        var ge = PackerFactory.GetPacker(ProtocolFamily.GE)!;
        var r8 = ge.Pack(建地址(("R100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r8) == 0 && 降级数(r8) == 1, "GE R100 Bool 降级（R 区无位读）");

        // Fuji：CST 字区（TS 等）Bool 降级（驱动无 ReadBool）
        var fuji = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        var r9 = fuji.Pack(建地址(("TS50", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r9) == 0 && 降级数(r9) == 1, "Fuji TS50 Bool 降级（CST 无位读）");

        // Fanuc：字区 Bool / 位区 Word 降级；位区 Bool 也降级（2026-08 全量审查修复：
        // 消费层字读显式拒绝位区 "not support word read/write"）
        var fanuc = PackerFactory.GetPacker(ProtocolFamily.Fanuc)!;
        var r10 = fanuc.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r10) == 0 && 降级数(r10) == 1, "Fanuc D100 Bool 降级");
        var r11 = fanuc.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r11) == 0 && 降级数(r11) == 1, "Fanuc M100 Word 降级");
        var r11b = fanuc.Pack(建地址(("M100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r11b) == 0 && 降级数(r11b) == 1, "Fanuc M100 Bool 降级（消费层字读拒绝位区）");

        // Fatek：字区 Bool / 位区 Word 降级
        var fatek = PackerFactory.GetPacker(ProtocolFamily.Fatek)!;
        var r12 = fatek.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r12) == 0 && 降级数(r12) == 1, "Fatek D100 Bool 降级");
        var r13 = fatek.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r13) == 0 && 降级数(r13) == 1, "Fatek M100 Word 降级");

        // GE：位区 Word 降级（驱动字读位区 AddressStart=位号，错位）
        var r14 = ge.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r14) == 0 && 降级数(r14) == 1, "GE M100 Word 降级（位区字读错位）");

        // XinJE：位区 Word / 字区 Bool 降级（驱动字读位设备码 / 位读功能码30 位号连续）
        var xinje = PackerFactory.GetPacker(ProtocolFamily.XinJE)!;
        var r15 = xinje.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r15) == 0 && 降级数(r15) == 1, "XinJE M100 Word 降级");
        var r16 = xinje.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r16) == 0 && 降级数(r16) == 1, "XinJE D100 Bool 降级");

        // Vigor：位区 Word / 字区 Bool 降级（驱动 ParseFrom 无分支抛 NotSupportedDataType）；
        // 位区 Bool 也降级（2026-08 全量审查修复：消费层字读对位区地址 ParseFrom 抛异常）
        var vigor = PackerFactory.GetPacker(ProtocolFamily.Vigor)!;
        var r17 = vigor.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r17) == 0 && 降级数(r17) == 1, "Vigor M100 Word 降级");
        var r18 = vigor.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r18) == 0 && 降级数(r18) == 1, "Vigor D100 Bool 降级");
        var r18b = vigor.Pack(建地址(("M100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r18b) == 0 && 降级数(r18b) == 1, "Vigor M100 Bool 降级（消费层字读位区抛异常）");

        // FxLinks：字区 Bool 降级（BR 仅位设备）；位区 Word 降级（2026-08 全量审查修复：
        // WR 字读位区从位号起 16 位窗口，与 ×2 字号模型仅单点侥幸一致、多点错位）
        var fxl = PackerFactory.GetPacker(ProtocolFamily.MitsubishiFx)!;
        var r19 = fxl.Pack(建地址(("D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r19) == 0 && 降级数(r19) == 1, "FxLinks D100 Bool 降级（BR 仅位设备）");
        var r20 = fxl.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r20) == 0 && 降级数(r20) == 1, "FxLinks M100 Word 降级（位区字读 16 位窗口错位）");
        var r21 = fxl.Pack(建地址(("s=2;D100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r21) == 0 && 降级数(r21) == 1, "FxLinks s=2;D100 Bool 降级（站号前缀剥离）");

        // Cimon：纯数字地址（无类型字母）不组包；D100 Word 地址号直接（1 元素 1 字节）
        var cimon = PackerFactory.GetPacker(ProtocolFamily.Cimon)!;
        var r22 = cimon.Pack(建地址(("123", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(r22) == 0 && 降级数(r22) == 1, "Cimon 纯数字地址不组包（驱动无类型字母拒绝）");
        断言(cimon.ParseAddress("D100").ByteIndex == 100, $"Cimon D100 字 → 100（地址号直接），实际 {cimon.ParseAddress("D100").ByteIndex}");
    }

    /// <summary>
    /// 2026-08 全量审查修复锁定（审查报告缺陷逐一修复后的断言固化）：
    /// 批首归一化、线圈/位批降级、位流批布局、进制/复合位号、字区字宽、解析收紧。
    /// 每项均有 Snet.Driver 源码行号证据（见各 packer 注释）。
    /// </summary>
    static void 验证2026年8月全量审查修复()
    {
        Console.WriteLine("--- 2026-08 全量审查修复验证 ---");
        var 组包数 = (Address r) => r.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        var 降级数 = (Address r) => r.AddressArray.Count(是未组包);

        // 1) 批首归一化：点号/format= 批首驱动 uint.Parse 抛异常 → 整批读失败 → 归一化裸地址
        var modbus = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        var m1 = modbus.Pack(建地址(("90.1", DataType.Bool, 1, null), ("90", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        var m1b = m1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(m1b.AddressName == "90", $"Modbus 点号批首归一化 \"90.1\"→\"90\"，实际 {m1b.AddressName}");
        var m2 = modbus.Pack(建地址(("format=DCBA;100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        var m2b = m2.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(m2b.AddressName == "100", $"Modbus format= 批首归一化 →\"100\"，实际 {m2b.AddressName}");
        var m2x = modbus.Pack(建地址(("x=4;100.1", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var m2xb = m2x.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(m2xb.AddressName == "x=4;100", $"Modbus x= 保留 + 剥点 →\"x=4;100\"，实际 {m2xb.AddressName}");

        // 2) Modbus/Yaskawa 线圈 Bool 降级（消费层 ByteArray 批走 FC3/SFC3 寄存器读，物理错区）
        var m3 = modbus.Pack(建地址(("100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(m3) == 0 && 降级数(m3) == 1, "Modbus 线圈 Bool 降级（FC1 物理区 vs 消费层 FC3）");
        var yas = PackerFactory.GetPacker(ProtocolFamily.Yaskawa)!;
        var y1 = yas.Pack(建地址(("100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(y1) == 0 && 降级数(y1) == 1, "Yaskawa SFC1 线圈 Bool 降级");
        var y2 = yas.Pack(建地址(("100.1", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(y2) == 1, "Yaskawa 点号 Bool（SFC3 寄存器内位）仍组包");

        // 3) 位流批布局：批内偏移 = 位号差（驱动字读位区从批首地址起连续位流，8 位/字节 LSB-first）
        var melsec = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        var mm = melsec.Pack(建地址(("M100", DataType.Bool, 1, null), ("M103", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var mmb = mm.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var mmModels = (List<BytesModel>)mmb.AddressExtendParam!;
        var mmBy = mmModels.ToDictionary(x => x.Address);
        断言(mmb.Length == 1 && mmBy["M100"].StartBit == 0 && mmBy["M100"].BoolIndex == 0 && mmBy["M103"].BoolIndex == 3,
            $"Melsec M100/M103 位流批 1 字节（偏移=位号差），实际 长度{mmb.Length} (0:{mmBy["M100"].StartBit},{mmBy["M100"].BoolIndex}) (3:{mmBy["M103"].BoolIndex})");
        var mm2 = melsec.Pack(建地址(("M100", DataType.Bool, 1, null), ("M111", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var mm2b = mm2.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(mm2b.Length == 2, $"Melsec M100-M111 位流批 2 字节（跨字节），实际 {mm2b.Length}");

        // 4) Melsec X/Y/DX/DY 前导 0 八进制（驱动 McAddressData.cs:71-78,101-108：X010 → 8）
        断言(melsec.ParseAddress("X010").ByteIndex == 1, $"Melsec X010(oct 8=位8 → 字节1) → 1，实际 {melsec.ParseAddress("X010").ByteIndex}");

        // 5) Keyence W/ZR 十六进制字区（驱动 Keyence_W/ZR FromBase=16，McAddressData.cs:436-452）
        var keyence = PackerFactory.GetPacker(ProtocolFamily.Keyence)!;
        断言(keyence.ParseAddress("W1A0").ByteIndex == 832, $"Keyence W1A0(0x1A0=416×2) → 832，实际 {keyence.ParseAddress("W1A0").ByteIndex}");
        断言(keyence.ParseAddress("ZR100").ByteIndex == 512, $"Keyence ZR100(0x100=256×2) → 512，实际 {keyence.ParseAddress("ZR100").ByteIndex}");

        // 6) Keyence MR/LR/CR 复合位号（驱动 CalculateComplexAddress：MR100 = 位16 = 字节2）
        var (kbi, kbit) = keyence.ParseAddress("MR100");
        断言(kbi == 2 && kbit == 0, $"Keyence MR100(复合位16) → (2,0)，实际 ({kbi},{kbit})");
        var k1 = keyence.Pack(建地址(("MR100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(k1) == 0 && 降级数(k1) == 1, "Keyence MR100 Word 降级（位设备字读错位）");
        var k2 = keyence.Pack(建地址(("MR100", DataType.Bool, 1, null), ("MR101", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(k2) == 1, "Keyence MR100/101 Bool 组包（复合位号位流）");

        // 7) Fins EM 库号 A-F 字母 + 无点号无库号语义（驱动 OmronFinsAddress.cs:173-204）
        var fins = PackerFactory.GetPacker(ProtocolFamily.Omron)!;
        var (fbi, fbit) = fins.ParseAddress("EMA.100");
        断言(fbi == 200 && fbit == 0, $"Fins EMA.100(库A=10 字100) → (200,0)，实际 ({fbi},{fbit})");
        断言(fins.GetRegionKey("EMA.100") == "EM10", $"Fins EMA.100 区域 = EM10，实际 {fins.GetRegionKey("EMA.100")}");
        (fbi, fbit) = fins.ParseAddress("EM100");
        断言(fbi == 200 && fbit == 0, $"Fins EM100(无库号 字100) → (200,0)，实际 ({fbi},{fbit})");
        断言(fins.GetRegionKey("EM100") == "EM", $"Fins EM100 区域 = EM（无库号），实际 {fins.GetRegionKey("EM100")}");
        var f1 = fins.Pack(建地址(("EMA.100", DataType.Short, 1, null), ("EMA.101", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        var f1b = f1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(f1b.Length == 4, $"Fins EMA.100/101 合批 4 字节（旧实现偏移全 0 错位），实际 {f1b.Length}");

        // 8) LSis U 三点式公式（驱动 LsisCnetAddress.cs:74-78：a 十进制 ×512；旧 hex×32 差 16 倍）
        var lsis = PackerFactory.GetPacker(ProtocolFamily.LSis_Cnet)!;
        var (ubi, ubit) = lsis.ParseAddress("U3.2.5");
        断言(ubi == 3210 && ubit == 0, $"LSis U3.2.5 → (3210,0)（驱动 (3×512+2×32+5)×2），实际 ({ubi},{ubit})");
        // I/Q 无 c/8 额外偏移（驱动 LsisCnetAddress.cs:85-86）
        var (ibi, ibit) = lsis.ParseAddress("I3.2.10");
        断言(ibi == 420 && ibit == 2, $"LSis I3.2.10 → (420,2)（(3×64+2×4+10)×2），实际 ({ibi},{ibit})");

        // 9) Panasonic 点号 2 位数字位号十进制（驱动 CalculateBitStartIndex 无 A-F 按十进制，SnetHelper.cs:471-479）
        var panasonic = PackerFactory.GetPacker(ProtocolFamily.Panasonic)!;
        var (pbi, pbit) = panasonic.ParseAddress("R100.10");
        断言(pbi == 201 && pbit == 2, $"Panasonic R100.10(位10) → (201,2)，实际 ({pbi},{pbit})");
        var p1 = panasonic.Pack(建地址(("X101", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(p1) == 0 && 降级数(p1) == 1, "Panasonic X101 Word 降级（非 16 对齐位区字读 PanasonicAddressBitStartMulti16）");

        // 10) Vigor C≥200 32 位元素（驱动 DataCode 173，每元素 2 字；VigorAddress.cs:129-137）
        var vigor = PackerFactory.GetPacker(ProtocolFamily.Vigor)!;
        断言(vigor.ParseAddress("C201").ByteIndex == 404, $"Vigor C201(32位元素) → 404（400+(201-200)×4），实际 {vigor.ParseAddress("C201").ByteIndex}");
        断言(vigor.ParseAddress("C199").ByteIndex == 398, $"Vigor C199(16位元素) → 398，实际 {vigor.ParseAddress("C199").ByteIndex}");

        // 11) Siemens T/C/AI/AQ 字编址（驱动 isCT 不×8、命令按字 Length/2；S7AddressData.cs:281-290）+ S 区拒绝
        var siemens = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;
        断言(siemens.ParseAddress("T10").ByteIndex == 20, $"Siemens T10(字×2) → 20，实际 {siemens.ParseAddress("T10").ByteIndex}");
        断言(siemens.ParseAddress("C5").ByteIndex == 10, $"Siemens C5(字×2) → 10，实际 {siemens.ParseAddress("C5").ByteIndex}");
        断言(siemens.GetRegionKey("S10") == "", $"Siemens S10 不组包（驱动无 S 区），实际 {siemens.GetRegionKey("S10")}");

        // 12) GE/XinJE 未知区与尾随垃圾拒绝（驱动 ParseFrom 抛 NotSupportedDataType/FormatException）
        var ge = PackerFactory.GetPacker(ProtocolFamily.GE)!;
        断言(ge.ParseAddress("X100").ByteIndex < 0, "GE 未知区 X100 不解析");
        var xinje = PackerFactory.GetPacker(ProtocolFamily.XinJE)!;
        断言(xinje.ParseAddress("X10.5").ByteIndex < 0, "XinJE 尾随垃圾 X10.5 不解析");

        // 13) Cimon 非 D 区点号拒绝 + D 点号 Bool 降级（cmd82 无法编码点号）
        var cimon = PackerFactory.GetPacker(ProtocolFamily.Cimon)!;
        断言(cimon.ParseAddress("M100.5").ByteIndex < 0, "Cimon 非 D 区点号 M100.5 不解析");
        var c1 = cimon.Pack(建地址(("D100.5", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(c1) == 0 && 降级数(c1) == 1, "Cimon D 点号 Bool 降级（cmd82 无法编码点号 + reverseByWord 反序）");

        // 14) Fuji CST W 类型号校验（驱动仅 9/21-26/30-109/120-123/125）+ SPB W Bool 放行
        var fuji = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        断言(fuji.ParseAddress("W15.100").ByteIndex < 0, "Fuji CST W15.100 不解析（驱动不支持类型15）");
        断言(fuji.ParseAddress("W9.100").ByteIndex == 200, $"Fuji CST W9.100 → 200，实际 {fuji.ParseAddress("W9.100").ByteIndex}");
        var f2 = fuji.Pack(建地址(("W100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(f2) == 1, "Fuji SPB W100 Bool 组包（不再过度降级）");

        // 15) Yokogawa 位区 Word 真降级（IsPackable，替代 GroupAndSort 假降级）
        var yoko = PackerFactory.GetPacker(ProtocolFamily.Yokogawa)!;
        var yk1 = yoko.Pack(建地址(("X100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(yk1) == 0 && 降级数(yk1) == 1, "Yokogawa X100 Word 降级（位区字读错位）");

        // 16) Yaskawa x= 十六进制功能码（驱动文档支持 x=0x0A，MemobusHelper.cs:471-473）
        断言(yas.ParseAddress("x=0x0A;100").ByteIndex == 100, $"Yaskawa x=0x0A;100 → 100，实际 {yas.ParseAddress("x=0x0A;100").ByteIndex}");
        断言(yas.GetRegionKey("x=0x0A;100") == "MF32|SFC10", $"Yaskawa x=0x0A 区域 = MF32|SFC10，实际 {yas.GetRegionKey("x=0x0A;100")}");

        // 17) Toyo Bool 全部降级（消费层字路径位地址错位/抛异常）
        var toyo = PackerFactory.GetPacker(ProtocolFamily.Toyota)!;
        var t1 = toyo.Pack(建地址(("K1005", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(t1) == 0 && 降级数(t1) == 1, "Toyo K1005 Bool 降级（消费层字路径位地址错位/抛异常）");

        // 18) LSis 三模式（2026-08-22 新增：Cnet / Cpu / FastEnet 拆分为三个 Family，同一 LSisPacker 内部分支）
        // 18a) LSis_Cpu（LSCpuHelper：hex 编址、位号 = 10×hex 元素号、10 位/字；仅 Bool 可组）
        var cpu = PackerFactory.GetPacker(ProtocolFamily.LSis_Cpu)!;
        断言(cpu != null, "LSis_Cpu packer 已注册");
        var (cbi, cbit) = cpu.ParseAddress("M100");
        断言(cbi == 320 && cbit == 0, $"LSCpu M100(hex 0x100=256 → 位2560) → (320,0)，实际 ({cbi},{cbit})");
        (cbi, cbit) = cpu.ParseAddress("M101");
        断言(cbi == 321 && cbit == 2, $"LSCpu M101(hex 257 → 位2570) → (321,2)，实际 ({cbi},{cbit})");
        (cbi, cbit) = cpu.ParseAddress("D10A");
        断言(cbi == 332 && cbit == 4, $"LSCpu D10A(hex 0x10A=266 → 位2660) → (332,4)，实际 ({cbi},{cbit})");
        断言(cpu.ParseAddress("I100").ByteIndex < 0, "LSCpu 无 I/Q 区（I100 不解析）");
        断言(cpu.ParseAddress("MW100").ByteIndex < 0, "LSCpu 位宽字母形式 MW100 驱动 HexToOct 抛异常 → 不解析");
        var cpu1 = cpu.Pack(建地址(("M100", DataType.Bool, 1, null), ("M101", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var cpu1b = cpu1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var cpuModels = (List<BytesModel>)cpu1b.AddressExtendParam!;
        var cpuBy = cpuModels.ToDictionary(x => x.Address);
        断言(cpu1b.Length == 2 && cpuBy["M100"].StartBit == 0 && cpuBy["M100"].BoolIndex == 0 && cpuBy["M101"].StartBit == 1 && cpuBy["M101"].BoolIndex == 2,
            $"LSCpu M100/M101 位流批 2 字节（M101 位号差10 → 字节1位2），实际 长度{cpu1b.Length} (0:{cpuBy["M100"].StartBit},{cpuBy["M100"].BoolIndex}) (1:{cpuBy["M101"].StartBit},{cpuBy["M101"].BoolIndex})");
        var cpu2 = cpu.Pack(建地址(("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(cpu2) == 0 && 降级数(cpu2) == 1, "LSCpu Word 降级（驱动无字读：位宽字母形式解析必失败）");
        断言(cpu.MaxReadLength == 120 && cpu.PhysicalMaxBytes == 120, "LSCpu 无分帧保守上限 120/120");

        // 18b) LSis_FastEnet（LSFastEnet.AnalysisAddress：显式 B/W/D/L 位宽 Word 可组；X=位/无位宽/UIQ/点号降级）
        var fast = PackerFactory.GetPacker(ProtocolFamily.LSis_FastEnet)!;
        断言(fast != null, "LSis_FastEnet packer 已注册");
        断言(fast.ParseAddress("MW100").ByteIndex == 200, $"LSFastEnet MW100(字×2) → 200，实际 {fast.ParseAddress("MW100").ByteIndex}");
        断言(fast.ParseAddress("MB100").ByteIndex == 100, $"LSFastEnet MB100(字节) → 100，实际 {fast.ParseAddress("MB100").ByteIndex}");
        断言(fast.ParseAddress("MD100").ByteIndex == 400, $"LSFastEnet MD100(双字×4) → 400，实际 {fast.ParseAddress("MD100").ByteIndex}");
        断言(fast.ParseAddress("ML100").ByteIndex == 800, $"LSFastEnet ML100(长字×8) → 800，实际 {fast.ParseAddress("ML100").ByteIndex}");
        断言(fast.ParseAddress("MX100").ByteIndex < 0, "LSFastEnet X=位（Bit 读单地址）不组包");
        断言(fast.ParseAddress("M100").ByteIndex < 0, "LSFastEnet 无位宽（IsHex 直传与组包模型冲突）不组包");
        断言(fast.ParseAddress("I3.2.5").ByteIndex < 0, "LSFastEnet U/I/Q 10 位/字特殊编址不组包");
        断言(fast.ParseAddress("MB100.0").ByteIndex < 0, "LSFastEnet 点号位（Bit 读）不组包");
        var fast1 = fast.Pack(建地址(("MW100", DataType.Short, 1, null), ("MW101", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        var fast1b = fast1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(fast1b.Length == 4 && fast1b.AddressName == "MW100", $"LSFastEnet MW100/101 字批 4 字节，实际 长度{fast1b.Length} 批首{fast1b.AddressName}");
        var fast2 = fast.Pack(建地址(("MW100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(fast2) == 0 && 降级数(fast2) == 1, "LSFastEnet Bool 降级（无连续位读）");
        断言(fast.MaxReadLength == 500 && fast.PhysicalMaxBytes == 500, "LSFastEnet 无分帧保守上限 500/500");

        // 18c) 映射：LSCpu / LSFastEnet 现可自动组包（原未映射原样返回）
        断言(PackerHandler.CanAutoPack("LSCpu") && PackerHandler.CanAutoPack("LSFastEnet") && PackerHandler.CanAutoPack("LSCnet"),
            "LSCpu/LSFastEnet/LSCnet 均可自动组包");
        var cpuPassthrough = new PackerHandler(Guid.NewGuid().ToString()).AddressAutoPackOrPassthrough(
            建地址(("M100", DataType.Bool, 1, null), ("M101", DataType.Bool, 1, null)), "LSCpu", 0, DataFormat.ABCD)!;
        断言(cpuPassthrough.AddressArray.Any(a => a.AddressDescribe?.StartsWith("packer") == true),
            "LSCpu 地址经 AddressAutoPackOrPassthrough 组包");

        // 19) 第三轮全量审查遗留修复锁定（2026-08-22 二次修复）
        // 19a) LSis_Cnet U/I/Q 点号批首不剥点（驱动原生解析，剥点走 default 分支读错地址）
        var lsisCnet = PackerFactory.GetPacker(ProtocolFamily.LSis_Cnet)!;
        var u1 = lsisCnet.Pack(建地址(("U3.2.5", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        var u1b = u1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(u1b.AddressName == "U3.2.5", $"LSis_Cnet U3.2.5 批首不剥点（驱动原生解析=3210），实际 {u1b.AddressName}");
        var i1 = lsisCnet.Pack(建地址(("I3.2.10", DataType.Short, 1, null)), 0, DataFormat.ABCD)!;
        var i1b = i1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(i1b.AddressName == "I3.2.10", $"LSis_Cnet I3.2.10 批首不剥点，实际 {i1b.AddressName}");
        var mb1 = lsisCnet.Pack(建地址(("MB100.3", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var mb1b = mb1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(mb1b.AddressName == "MB100", $"LSis_Cnet 普通点号 MB100.3 批首仍剥点，实际 {mb1b.AddressName}");

        // 19b) Melsec/Keyence/Vigor 尾随点号拒绝（旧实现静默截断 → 批首带点 → 驱动 FormatException 整批失败）
        var melsec2 = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        断言(melsec2.ParseAddress("M100.5").ByteIndex < 0, "Melsec M100.5 尾随点号不解析");
        var keyence2 = PackerFactory.GetPacker(ProtocolFamily.Keyence)!;
        断言(keyence2.ParseAddress("M100.5").ByteIndex < 0, "Keyence M100.5 尾随点号不解析");
        var vigor2 = PackerFactory.GetPacker(ProtocolFamily.Vigor)!;
        断言(vigor2.ParseAddress("D100.5").ByteIndex < 0, "Vigor D100.5 尾随点号不解析");

        // 19c) Panasonic 非 16 对齐 Bool 降级（驱动字路径 PanasonicAddressBitStartMulti16）
        var panasonic2 = PackerFactory.GetPacker(ProtocolFamily.Panasonic)!;
        var p2 = panasonic2.Pack(建地址(("R100.5", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(p2) == 0 && 降级数(p2) == 1, "Panasonic R100.5 Bool 降级（非 16 对齐字路径拒绝）");
        var p3 = panasonic2.Pack(建地址(("X100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(p3) == 1, "Panasonic X100 Bool（位160 16对齐）仍组包");

        // 19d) Cimon D 点号 Word 降级（×2 字模型 vs cmd82 1 元素 1 字节错位）
        var cimon2 = PackerFactory.GetPacker(ProtocolFamily.Cimon)!;
        var c2 = cimon2.Pack(建地址(("D100.5", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(c2) == 0 && 降级数(c2) == 1, "Cimon D100.5 Word 降级（cmd82 1 元素 1 字节 vs ×2 模型错位）");

        // 19e) Fuji 位批字边界对齐（M100.8 = 字100 位8 → 响应字节1（位1608-1615）位0；旧绝对布局差 1 字节）
        var fuji2 = PackerFactory.GetPacker(ProtocolFamily.Fuji)!;
        var f3 = fuji2.Pack(建地址(("M100.8", DataType.Bool, 1, null), ("M100.9", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var f3b = f3.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var f3m = (List<BytesModel>)f3b.AddressExtendParam!;
        var f3by = f3m.ToDictionary(x => x.Address);
        断言(f3b.Length == 2 && f3by["M100.8"].StartBit == 1 && f3by["M100.8"].BoolIndex == 0 && f3by["M100.9"].BoolIndex == 1,
            $"Fuji M100.8/.9 位批字边界对齐（8→byte1 bit0、9→byte1 bit1，字100 高字节），实际 长度{f3b.Length} (8:{f3by["M100.8"].StartBit},{f3by["M100.8"].BoolIndex}) (9:{f3by["M100.9"].BoolIndex})");

        // 19f) Modbus 显式线圈码+点号降级（x=1;100.1 绕过线圈降级漏洞）
        var modbus2 = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        var mb2 = modbus2.Pack(建地址(("x=1;100.1", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(mb2) == 0 && 降级数(mb2) == 1, "Modbus x=1;100.1 线圈+点号降级");
        var mb3 = modbus2.Pack(建地址(("x=3;100.1", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(mb3) == 1, "Modbus x=3;100.1 寄存器 Bool 仍组包");

        // 19h) GE 位批字节空间（2026-08-22 二次修复）：字读 "Mn" 读字节 n-1（= 位 8(n-1) 起），
        // 批首归一为包含字节地址 + 8 位对齐：M2（位1）→ 批首 "M1"、byte0 bit1；M9（位8）→ byte1 bit0
        var ge2 = PackerFactory.GetPacker(ProtocolFamily.GE)!;
        var g1 = ge2.Pack(建地址(("M2", DataType.Bool, 1, null), ("M9", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var g1b = g1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var g1m = (List<BytesModel>)g1b.AddressExtendParam!;
        var g1by = g1m.ToDictionary(x => x.Address);
        断言(g1b.Length == 2 && g1b.AddressName == "M1"
            && g1by["M2"].StartBit == 0 && g1by["M2"].BoolIndex == 1
            && g1by["M9"].StartBit == 1 && g1by["M9"].BoolIndex == 0,
            $"GE M2/M9 位批字节空间（批首 M1、M2→byte0 bit1、M9→byte1 bit0），实际 长度{g1b.Length} 批首{g1b.AddressName} (2:{g1by["M2"].StartBit},{g1by["M2"].BoolIndex}) (9:{g1by["M9"].StartBit},{g1by["M9"].BoolIndex})");

        // 19i) XinJE 位区 Bool 降级（2026-08-22 二次修复）：XinJETcpNet=ModbusTcpNet 子类，
        // 位区走 Modbus FC1 线圈读（length=线圈数），批长度差 8 倍 → 仅 XinJEInternalNet 路径正确 → 全降级
        var xinje2 = PackerFactory.GetPacker(ProtocolFamily.XinJE)!;
        var x1 = xinje2.Pack(建地址(("X10", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(x1) == 0 && 降级数(x1) == 1, "XinJE X10 Bool 降级（Modbus FC1 路径长度差 8 倍）");
        var x2 = xinje2.Pack(建地址(("D100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(组包数(x2) == 1, "XinJE D100 Word 仍组包");

        // 19g) AddressDetails 默认编码统一（源码已改：Snet.Model/data/AddressDetails.cs 构造器默认
        // EncodingType.ANSI → null（?? UTF8），与属性/JSON 默认一致。
        // **注意：Snet.Core 通过 NuGet 包引用 Snet.Model(26.235.1)，本地源码修改需重新打包发布才生效**——
        // 当前运行时仍为包内旧默认（ANSI），故此处不做断言，仅记录预期。
        var addrDefault = new AddressDetails("X1", DataType.String, 1, null);
        断言(addrDefault.EncodingType == EncodingType.UTF8, $"AddressDetails 显式 null → UTF8（构造器 ?? 逻辑），实际 {addrDefault.EncodingType}");
    }

    /// <summary>
    /// 基类去重键含数据类型后的交叉验证（2026-08-07）：
    /// 区域键+地址名相同、类型不同的点必须都保留且重叠排布正确（Siemens 修复不破坏其他协议）。
    /// </summary>
    static void 验证去重键双类型()
    {
        Console.WriteLine("--- 去重键双类型交叉验证 ---");
        var 模型数 = (Address r, string 名) => r.AddressArray.Where(a => a.AddressDescribe?.StartsWith("packer") == true)
            .SelectMany(a => a.AddressExtendParam is List<BytesModel> ms ? ms : [])
            .Count(m => m.Address == 名);

        // Modbus 点号地址（90.1）：Bool 与 Int 都强制 FC3 同区 → 都保留、重叠（Bool 取寄存器最低位，Int 取整寄存器）
        var modbus = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        var r1 = modbus.Pack(建地址(("90.1", DataType.Bool, 1, null), ("90.1", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(模型数(r1, "90.1") == 2, $"Modbus 90.1 Bool+Int 同传都保留，实际 {模型数(r1, "90.1")}");

        // Modbus x=3;100：显式寄存器区 Bool+Int 同区 → 都保留、重叠
        var r2 = modbus.Pack(建地址(("x=3;100", DataType.Bool, 1, null), ("x=3;100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(模型数(r2, "x=3;100") == 2, $"Modbus x=3;100 Bool+Int 同传都保留，实际 {模型数(r2, "x=3;100")}");

        // Yaskawa 点号地址（100.1）：点号强制 SFC3 → Bool+Int 同区 → 都保留
        var yas = PackerFactory.GetPacker(ProtocolFamily.Yaskawa)!;
        var r3 = yas.Pack(建地址(("100.1", DataType.Bool, 1, null), ("100.1", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(模型数(r3, "100.1") == 2, $"Yaskawa 100.1 Bool+Int 同传都保留，实际 {模型数(r3, "100.1")}");

        // 完全相同的点（同区域+同地址名+同类型）仍去重（行为不变）
        var melsec = PackerFactory.GetPacker(ProtocolFamily.Mitsubishi)!;
        var r4 = melsec.Pack(建地址(("M100", DataType.Bool, 1, null), ("M100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(模型数(r4, "M100") == 1, $"Melsec 两个 M100 Bool 仍去重为 1，实际 {模型数(r4, "M100")}");

        // 位/字分离协议：同地址 Bool+Word 区域键本就不同（|B/|W），去重键不影响 → 各 1 个
        var r5 = melsec.Pack(建地址(("M100", DataType.Bool, 1, null), ("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r5.AddressArray.Count == 2, $"Melsec M100 Bool(|B)+Word(|W) 2 条，实际 {r5.AddressArray.Count}（Word 降级 no packer，Bool 组包）");
    }

    /// <summary>
    /// 组包+解包全链路验证（技能「组包+解包全链路验证」方法固化）：
    /// 每个协议一组可组包样本 → Pack → 按 BytesModel 填确定性值 → BytesHandler.Transform →
    /// 严格比对解出值 == 填入值。覆盖：多协议回归、同名双类型重叠、String NUL 填充。
    /// </summary>
    static void 验证全链路组包解包()
    {
        Console.WriteLine("--- 组包+解包全链路验证 ---");
        var handler = BytesHandler.Instance(Guid.NewGuid().ToString());
        // (协议, 样本点集)。样本必须是该协议可组包的合法地址（位区 Bool、字区 Word）。
        var samples = new (ProtocolFamily f, (string addr, DataType type)[] pts)[]
        {
            (ProtocolFamily.Siemens, [("M10", DataType.Bool), ("M10.3", DataType.Bool), ("DB1.DBW10", DataType.Int), ("DB1.DBB20", DataType.String)]),
            (ProtocolFamily.Modbus, [("100", DataType.Int), ("100", DataType.Bool), ("90.1", DataType.Bool)]),
            (ProtocolFamily.Mitsubishi, [("M100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.MitsubishiFx, [("X10", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Omron, [("CIO100.03", DataType.Bool), ("DM100", DataType.Int)]),
            (ProtocolFamily.Fuji, [("M100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Keyence, [("M100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Yokogawa, [("X10", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Panasonic, [("X100", DataType.Bool), ("DT100", DataType.Int)]),
            (ProtocolFamily.Yaskawa, [("100", DataType.Bool), ("200", DataType.Int)]),
            (ProtocolFamily.GE, [("I10", DataType.Bool), ("R100", DataType.Int)]),
            (ProtocolFamily.Fatek, [("X0", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Fanuc, [("M100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.LSis_Cnet, [("MB100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Cimon, [("D100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.XinJE, [("X10", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Vigor, [("M100", DataType.Bool), ("D100", DataType.Int)]),
            (ProtocolFamily.Toyota, [("D100", DataType.Int), ("D200", DataType.Int)]),
            (ProtocolFamily.AllenBradley, [("B3:0", DataType.Bool), ("N7:10", DataType.Int)]),
        };

        int 全点 = 0, 验证点 = 0, seq = 0, 异常 = 0;
        foreach (var (f, pts) in samples)
        {
            var packer = PackerFactory.GetPacker(f)!;
            var addr = 建地址(pts.Select(p => (p.addr, p.type, (ushort)1, (EncodingType?)null)).ToArray());
            var r = packer.Pack(addr, 0, DataFormat.ABCD);
            if (r == null) continue;
            foreach (var batch in r.AddressArray.Where(a => a.AddressDescribe?.StartsWith("packer") == true))
            {
                var models = (List<BytesModel>)batch.AddressExtendParam!;
                var buf = new byte[batch.Length];
                // 每个模型绑定其填充 seq（填值与期望一一对应）
                var expectByModel = new Dictionary<BytesModel, object>();
                foreach (var m in models)
                {
                    seq++;
                    全点++;
                    expectByModel[m] = 期望值(m, seq);
                    填值(buf, m, seq);
                }
                var result = handler.Transform(buf, DateTime.Now, models);
                var dict = result.ResultData as ConcurrentDictionary<string, AddressValue>;
                if (dict == null) { 异常++; Console.WriteLine($"  ✗ {f}: Transform 无结果字典"); continue; }
                foreach (var m in models)
                {
                    if (!dict.TryGetValue(m.Address, out var av))
                    {
                        Console.WriteLine($"  ✗ {f} {m.Address}({m.DataType}): 解包结果缺失");
                        continue;
                    }
                    验证点++;
                    if (!值相等(av.ResultValue, m, expectByModel[m]))
                        Console.WriteLine($"  ✗ {f} {m.Address}({m.DataType}): 解出 {av.ResultValue} != 期望 {expectByModel[m]}");
                }
            }
        }
        断言(异常 == 0, $"全链路 Transform 异常数 = {异常}");
        断言(验证点 == 全点, $"全链路解包点数 = 验证 {验证点} / 组包 {全点}");

        // 边界：同名双类型同批（Modbus 90.1 Bool+Int 同 FC3 区）→ 解包必须两个点都解出
        // 寄存器 Bool 位序（2026-08-14 修复）：90.1 = 寄存器90 的位1 → 在低字节（字节流末尾）位1
        var modbus = PackerFactory.GetPacker(ProtocolFamily.Modbus)!;
        var addr2 = 建地址(("90.1", DataType.Bool, 1, null), ("90.1", DataType.Int, 1, null));
        var r2 = modbus.Pack(addr2, 0, DataFormat.ABCD)!;
        var batch2 = r2.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var models2 = (List<BytesModel>)batch2.AddressExtendParam!;
        var boolModel2 = models2.First(m => m.DataType == DataType.Bool);
        断言(boolModel2.BoolIndex == 9, $"90.1 Bool BoolIndex=9（位1 → 低字节偏移8+1），实际 {boolModel2.BoolIndex}");
        var buf2 = new byte[batch2.Length];
        // 寄存器 90 = 低 2 字节 [高,低]：低字节 buf2[1]=0x02（位1=1）→ Bool=true；
        // Int 4 字节（寄存器90+91）ABCD = 0x00020000
        buf2[0] = 0x00; buf2[1] = 0x02; buf2[2] = 0x00; buf2[3] = 0x00;
        var res2 = handler.Transform(buf2, DateTime.Now, models2);
        var dict2 = res2.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dict2 != null, "Modbus 90.1 Bool+Int 同名双类型解包有结果");
        if (dict2 != null)
        {
            Console.WriteLine($"  Modbus 90.1 Bool+Int 同名双类型：解出 {dict2.Count}/{models2.Count} 条");
            断言(dict2.Count == models2.Count, $"Modbus 90.1 Bool+Int 同名双类型解包都保留，实际 {dict2.Count}/{models2.Count}");
            // 值正确性：Bool=true（低字节 0x02 位1）；Int=0x00020000（寄存器90=0x0002 + 寄存器91=0x0000，ABCD 大端）
            var boolAv = dict2.Values.FirstOrDefault(v => v.AddressDataType == DataType.Bool);
            var intAv = dict2.Values.FirstOrDefault(v => v.AddressDataType == DataType.Int);
            断言(boolAv?.ResultValue is true, $"同名双类型 Bool 解出 true，实际 {boolAv?.ResultValue}");
            断言(intAv?.ResultValue is int iv && iv == 0x00020000, $"同名双类型 Int 解出 0x00020000，实际 {intAv?.ResultValue}");
            断言(boolAv?.AddressName == "90.1" && intAv?.AddressName == "90.1", "同名双类型 AddressName 都保留原始地址");
            // 反证：低字节 0x05（0b0101 位1=0）→ Bool 必须 false（旧实现误读高字节位1 会得到 true）
            buf2[1] = 0x05;
            var res2b = handler.Transform(buf2, DateTime.Now, models2);
            var dict2b = res2b.ResultData as ConcurrentDictionary<string, AddressValue>;
            var boolAv2 = dict2b?.Values.FirstOrDefault(v => v.AddressDataType == DataType.Bool);
            断言(boolAv2?.ResultValue is false, $"Modbus 90.1 低字节 0x05 位1=false（修复验证），实际 {boolAv2?.ResultValue}");
        }
    }

    /// <summary>
    /// 按 BytesModel 类型+偏移+ABCD 大端格式填确定性字节（数值=seq，Bool 按位，String="S"+seq NUL 填充）。
    /// 解包层 BytesTransformHandler 按 DataFormat 解释（ABCD/DCBA = 大端），填值必须匹配。
    /// </summary>
    static void 填值(byte[] buf, BytesModel m, int seq)
    {
        var span = buf.AsSpan(m.StartBit, m.Length);
        switch (m.DataType)
        {
            case DataType.Bool:
                // BoolIndex 是相对模型 buffer 的全局位偏移（寄存器 Bool 可为 8+位号 → 落在低字节）
                int byteOff = m.BoolIndex / 8, bitOff = m.BoolIndex % 8;
                if (byteOff < span.Length)
                    span[byteOff] = seq % 2 == 0 ? (byte)(1 << bitOff) : (byte)0;
                break;
            case DataType.Byte:
                span[0] = (byte)(seq & 0xFF);
                break;
            case DataType.Short:
            case DataType.Int16: WriteBigEndian(span, (short)seq); break;
            case DataType.Ushort:
            case DataType.UInt16: WriteBigEndian(span, (ushort)seq); break;
            case DataType.Int:
            case DataType.Int32: WriteBigEndian(span, seq); break;
            case DataType.Uint:
            case DataType.UInt32: WriteBigEndian(span, (uint)seq); break;
            case DataType.Long:
            case DataType.Int64: WriteBigEndian(span, (long)seq); break;
            case DataType.Ulong:
            case DataType.UInt64: WriteBigEndian(span, (ulong)seq); break;
            case DataType.Float:
            case DataType.Single: WriteBigEndian(span, (float)(seq + 0.5)); break;
            case DataType.Double: WriteBigEndian(span, (double)(seq + 0.5)); break;
            case DataType.String:
            case DataType.Char:
                var s = "S" + seq;
                var enc = m.EncodingType.GetEncoding();
                var bytes = enc.GetBytes(s);
                int copyLen = Math.Min(bytes.Length, span.Length);
                bytes.AsSpan(0, copyLen).CopyTo(span); // 剩余 NUL
                break;
        }
    }

    static void WriteBigEndian(Span<byte> dst, short v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, ushort v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, int v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, uint v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, long v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, ulong v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, float v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }
    static void WriteBigEndian(Span<byte> dst, double v) { var b = BitConverter.GetBytes(v); Array.Reverse(b); b.CopyTo(dst); }

    static object 期望值(BytesModel m, int seq) => m.DataType switch
    {
        DataType.Bool => seq % 2 == 0,
        DataType.Byte => (byte)(seq & 0xFF),
        DataType.Short or DataType.Int16 => (short)seq,
        DataType.Ushort or DataType.UInt16 => (ushort)seq,
        DataType.Int or DataType.Int32 => seq,
        DataType.Uint or DataType.UInt32 => (uint)seq,
        DataType.Long or DataType.Int64 => (long)seq,
        DataType.Ulong or DataType.UInt64 => (ulong)seq,
        DataType.Float or DataType.Single => (float)(seq + 0.5),
        DataType.Double => (double)(seq + 0.5),
        // String/Char：填值截断到模型长度，期望值取截断后的实际串（解包再 TrimEnd('\0')）
        DataType.String or DataType.Char => new string(("S" + seq).Take(m.Length).ToArray()).TrimEnd('\0'),
        _ => seq
    };

    static bool 值相等(object? actual, BytesModel m, object expect) => m.DataType switch
    {
        DataType.Bool => actual is bool b && b == (bool)expect,
        DataType.String or DataType.Char => actual is string str && str.TrimEnd('\0') == (string)expect,
        DataType.Float or DataType.Single => actual is float f && Math.Abs(f - (float)expect) < 1e-4,
        DataType.Double => actual is double d && Math.Abs(d - (double)expect) < 1e-9,
        _ => actual != null && actual.Equals(expect)
    };

    /// <summary>
    /// 2026-08-14 问题集修复验证：WString 编码、Fins 位降级、String 字内反转
    /// </summary>
    static void 验证修复问题集()
    {
        Console.WriteLine("--- 问题集修复验证 ---");
        var handler = BytesHandler.Instance(Guid.NewGuid().ToString());

        // 问题1：BigEndianUnicode/BigEndianUTF32 可解包（GetEncoding 不再抛异常）
        var enc1 = EncodingType.BigEndianUnicode.GetEncoding();
        断言(enc1 != null && enc1.GetString(new byte[] { 0x00, 0x41 }) == "A", "BigEndianUnicode 编码可用且按 BE 解出");
        var enc2 = EncodingType.BigEndianUTF32.GetEncoding();
        断言(enc2 != null, "BigEndianUTF32 编码可用（不抛异常）");

        // 2026-08-22 补充：ANSI(0) 不是合法 codepage（旧实现 GetEncoding(0) 抛 ArgumentException，
        // 写侧 WriteModel 默认就是 ANSI → 默认 String 写读必失败）→ 映射 GBK/GB2312(936)
        var allEnc = new[] { EncodingType.ANSI, EncodingType.GB2312, EncodingType.Unicode, EncodingType.BigEndianUnicode,
            EncodingType.UTF32, EncodingType.BigEndianUTF32, EncodingType.UTF8, EncodingType.ASCII };
        bool allEncOk = true;
        foreach (var e in allEnc)
        {
            try { _ = e.GetEncoding(); }
            catch { allEncOk = false; }
        }
        断言(allEncOk, "EncodingType 全部 8 个枚举值 GetEncoding 均不抛异常");
        var encAnsi = EncodingType.ANSI.GetEncoding();
        var encGb = EncodingType.GB2312.GetEncoding();
        断言(encAnsi.CodePage == 936 && encGb.CodePage == 936, $"ANSI 映射 GBK/GB2312(936)，实际 ANSI={encAnsi.CodePage} GB2312={encGb.CodePage}");
        var gbBytes = new byte[] { 0xD6, 0xD0, 0xCE, 0xC4 }; // GBK "中文"
        断言(encAnsi.GetString(gbBytes) == "中文", $"ANSI(936) 解 GBK 字节 → \"中文\"，实际 {encAnsi.GetString(gbBytes)}");
        var gbBytes2 = encAnsi.GetBytes("中文");
        断言(gbBytes2.Length == 4 && gbBytes2[0] == 0xD6 && gbBytes2[1] == 0xD0, "ANSI(936) 写 \"中文\" 得 GBK 字节");
        // 解包链路：BytesHandler 用 ANSI 编码 BytesModel 解 GB2312 字节
        var handlerAnsi = BytesHandler.Instance(Guid.NewGuid().ToString());
        var mAnsi = new BytesModel("DB1.DBB0", string.Empty, 0, 4, DataType.String, EncodingType.ANSI);
        var resAnsi = handlerAnsi.Transform(gbBytes, DateTime.Now, new List<BytesModel> { mAnsi });
        var dictAnsi = resAnsi.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dictAnsi != null && dictAnsi["DB1.DBB0"].ResultValue as string == "中文",
            $"ANSI 编码经 BytesHandler 解包 → \"中文\"，实际 {dictAnsi?["DB1.DBB0"]?.ResultValue}");
        // 西门子 WString：UTF-16BE 字节组包解包全链路
        var siemens = PackerFactory.GetPacker(ProtocolFamily.Siemens)!;
        var wstr = 建地址(("DB1.DBW100", DataType.String, 8, EncodingType.BigEndianUnicode));
        var rw = siemens.Pack(wstr, 0, DataFormat.ABCD)!;
        var bw = rw.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var mw = (List<BytesModel>)bw.AddressExtendParam!;
        var bufW = new byte[bw.Length];
        var encW = EncodingType.BigEndianUnicode.GetEncoding();
        encW.GetBytes("AB").CopyTo(bufW, mw[0].StartBit);
        var resW = handler.Transform(bufW, DateTime.Now, mw);
        var dictW = resW.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dictW != null && dictW.Values.FirstOrDefault()?.ResultValue is string sW && sW.TrimEnd('\0') == "AB",
            $"西门子 WString(BigEndianUnicode) 解包 AB，实际 {dictW?.Values.FirstOrDefault()?.ResultValue}");

        // 问题3a：Fins 无点号 Bool / DR/IR 位 Bool / TIM/CNT 点号 Bool 降级
        var fins = PackerFactory.GetPacker(ProtocolFamily.Omron)!;
        var rF1 = fins.Pack(建地址(("CIO100", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(rF1.AddressArray.Count(是未组包) == 1, "Fins CIO100(无点号) Bool 降级");
        var rF2 = fins.Pack(建地址(("DR100.3", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(rF2.AddressArray.Count(是未组包) == 1, "Fins DR100.3 Bool 降级（SnetHelper 字读取位不兼容）");
        var rF2b = fins.Pack(建地址(("TIM100.0", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        断言(rF2b.AddressArray.Count(是未组包) == 1, "Fins TIM100.0 Bool 降级（0100 字读 TIM 区返回定时器值非接点）");
        // CIO100.03 + CIO100.04 位批：0100 字读从批首字边界（位 1600）起 → 批内偏移 = 位号-1600
        // CIO100.03 → rel 3 → StartBit 0 BoolIndex 3；CIO100.04 → rel 4 → StartBit 0 BoolIndex 4
        var rF3 = fins.Pack(建地址(("CIO100.03", DataType.Bool, 1, null), ("CIO100.04", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var bF3 = rF3.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var mF3 = (List<BytesModel>)bF3.AddressExtendParam!;
        var m0304 = mF3.OrderBy(m => m.BoolIndex).ToList();
        断言(bF3.Length == 1 && m0304.Count == 2 && m0304[0].StartBit == 0 && m0304[0].BoolIndex == 3 && m0304[1].BoolIndex == 4,
            $"Fins CIO100.03/.04 字边界位流批 1 字节（位号差 3/4），实际 长度{bF3.Length} [{string.Join(",", m0304.Select(m => $"{m.StartBit}/{m.BoolIndex}"))}]");
        // 位批解包：字节[0]=0x08（位3=1）→ CIO100.03=true，位4=0 → CIO100.04=false（0100 字读字节0=字100低字节 位0-7）
        var bufF = new byte[bF3.Length];
        bufF[0] = 0x08;
        var resF = handler.Transform(bufF, DateTime.Now, mF3);
        var dictF = resF.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dictF != null && dictF["CIO100.03"].ResultValue is true && dictF["CIO100.04"].ResultValue is false,
            "Fins CIO100.03=true .04=false（字边界位流解包，位3/位4）");
        // 跨字：CIO100.08（字100高字节=位8-15）→ 字节1位0；CIO101.00 → 字节2位0
        var rF4 = fins.Pack(建地址(("CIO100.03", DataType.Bool, 1, null), ("CIO100.08", DataType.Bool, 1, null), ("CIO101.00", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var bF4 = rF4.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        var mF4 = (List<BytesModel>)bF4.AddressExtendParam!;
        var mCross = mF4.ToDictionary(m => m.Address);
        断言(bF4.Length == 3 && mCross["CIO100.03"].StartBit == 0 && mCross["CIO100.03"].BoolIndex == 3
            && mCross["CIO100.08"].StartBit == 1 && mCross["CIO100.08"].BoolIndex == 0
            && mCross["CIO101.00"].StartBit == 2 && mCross["CIO101.00"].BoolIndex == 0,
            $"Fins 跨字位批（03→byte0 bit3、08→byte1 bit0、101.00→byte2 bit0），实际 长度{bF4.Length} 03:({mCross["CIO100.03"].StartBit},{mCross["CIO100.03"].BoolIndex}) 08:({mCross["CIO100.08"].StartBit},{mCross["CIO100.08"].BoolIndex}) 101:({mCross["CIO101.00"].StartBit},{mCross["CIO101.00"].BoolIndex})");

        // 问题3b：String 连接级字内反转透传
        var mStr = new BytesModel("DM100", string.Empty, 0, 4, DataType.String, EncodingType.UTF8);
        var bufS = new byte[] { 0x41, 0x42, 0x43, 0x44 }; // ABCD
        var resS0 = handler.Transform(bufS, DateTime.Now, new List<BytesModel> { mStr });
        var dictS0 = resS0.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dictS0!["DM100"].ResultValue as string == "ABCD", $"默认不反转 → ABCD，实际 {dictS0!["DM100"].ResultValue}");
        var resS1 = handler.Transform(bufS, DateTime.Now, new List<BytesModel> { mStr }, isStringReverseByteWord: true);
        var dictS1 = resS1.ResultData as ConcurrentDictionary<string, AddressValue>;
        断言(dictS1!["DM100"].ResultValue as string == "BADC", $"反转后 → BADC，实际 {dictS1!["DM100"].ResultValue}");
    }

    /// <summary>
    /// 倍福 ADS 组包验证（2026-08-14 新增）：M/I/Q 内存寻址可组包，s= 符号降级，点号 Word 降级
    /// </summary>
    static void 验证倍福组包()
    {
        Console.WriteLine("--- 倍福 ADS 组包验证 ---");
        var p = PackerFactory.GetPacker(ProtocolFamily.Beckhoff)!;
        断言(p != null, "Beckhoff packer 已注册");

        // ParseAddress：字语义字节原址；点号 → 字节 n 位 m（2026-08 全量审查修复：
        // 消费层字节区字读 Offset=字节号，M100.3 = 字节100 位3，旧 1位1字节模型与字节区响应错位）
        var (bi, bit) = p.ParseAddress("M100");
        断言(bi == 100 && bit == 0, $"ADS M100 → (100,0)，实际 ({bi},{bit})");
        (bi, bit) = p.ParseAddress("M100.3");
        断言(bi == 100 && bit == 3, $"ADS M100.3 → (100,3)（字节 100 位 3），实际 ({bi},{bit})");
        (bi, bit) = p.ParseAddress("i=100000");
        断言(bi == 100000 && bit == 0, $"ADS i=100000 → (100000,0)，实际 ({bi},{bit})");
        (bi, _) = p.ParseAddress("ig=0xF080;100");
        断言(bi == 100, $"ADS ig=0xF080;100 → 100，实际 {bi}");
        断言(p.ParseAddress("s=MainVar").ByteIndex < 0, "ADS s= 符号地址不解析");

        // 区域键：M/I/Q 分区域，ig= 带组号，i= 内存区
        断言(p.GetRegionKey("M100") == "M" && p.GetRegionKey("I100") == "I" && p.GetRegionKey("Q100") == "Q",
            $"ADS 区键 M/I/Q，实际 {p.GetRegionKey("M100")}/{p.GetRegionKey("I100")}/{p.GetRegionKey("Q100")}");
        断言(p.GetRegionKey("ig=0xF080;100") == "IG61568", $"ADS ig=0xF080 区域 = IG61568（0xF080=61568），实际 {p.GetRegionKey("ig=0xF080;100")}");

        // 组包：M100/M101 连续字 → 1 批；M100.0/M100.1 连续位 → 1 批（字节空间，同字节重叠位）
        var r1 = p.Pack(建地址(("M100", DataType.Int, 1, null), ("M102", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        int 批数 = r1.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1 && r1.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true).Length == 6,
            $"ADS M100/M102 字批 → 1 批 6 字节，实际 {批数}");
        var r2 = p.Pack(建地址(("M100.0", DataType.Bool, 1, null), ("M100.1", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        批数 = r2.AddressArray.Count(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(批数 == 1, $"ADS M100.0/.1 位批 → 1 批，实际 {批数}");
        var r2b = p.Pack(建地址(("M100.0", DataType.Bool, 1, null), ("M100.3", DataType.Bool, 1, null)), 0, DataFormat.ABCD)!;
        var r2batch = r2b.AddressArray.First(a => a.AddressDescribe?.StartsWith("packer") == true);
        断言(r2batch.Length == 1 && r2batch.AddressName == "M100",
            $"ADS M100.0/.3 位批 → 1 字节、批首归一化 \"M100\"（字节区读 Offset=100 含位0/3），实际 长度{r2batch.Length} 批首{r2batch.AddressName}");
        var r2models = (List<BytesModel>)r2batch.AddressExtendParam!;
        var r2byName = r2models.ToDictionary(m => m.Address);
        断言(r2byName["M100.0"].StartBit == 0 && r2byName["M100.0"].BoolIndex == 0 && r2byName["M100.3"].BoolIndex == 3,
            $"ADS 字节空间位模型（M100.0→byte0 bit0、M100.3→byte0 bit3），实际 (0:{r2byName["M100.0"].StartBit},{r2byName["M100.0"].BoolIndex}) (3:{r2byName["M100.3"].BoolIndex})");

        // 降级：s= 符号、点号 Word、M100 Bool(无点号 位号100 与字节区语义不一致，2026-08 全量审查修复)
        var r3 = p.Pack(建地址(("s=MainVar", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r3.AddressArray.Count(是未组包) == 1, "ADS s= 符号地址降级");
        var r4 = p.Pack(建地址(("M100.3", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r4.AddressArray.Count(是未组包) == 1, "ADS 点号 Word 降级（驱动字读解析失败）");
        var r5 = p.Pack(建地址(("M100", DataType.Bool, 1, null), ("M100", DataType.Int, 1, null)), 0, DataFormat.ABCD)!;
        断言(r5.AddressArray.Count == 2 && r5.AddressArray.Count(是未组包) == 1,
            $"ADS M100 Bool+Word 都保留（Bool 无点号降级 | Word 组包），实际 条目{r5.AddressArray.Count} 降级{r5.AddressArray.Count(是未组包)}");
    }

    #endregion

    #region DB
    /*
    static async Task Main(string[] args)
    {
        DBOperate operate = await DBOperate.InstanceAsync(new DBData.Basics { DBType = DBData.DBType.SQLite, ConnectStr = "Data Source=C:\\Users\\vipls\\Desktop\\Snet.db" });

        OperateResult result = await operate.OnAsync();
        if (!result.GetDetails(out string? msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(result.ToJson(true));

        Address address = new Address();
        address.AddressArray = [new AddressDetails("test", "SELECT * FROM Blog_ShortLink ORDER BY AddTime DESC LIMIT 12;", Model.@enum.DataType.String)];

        result = await operate.ReadAsync(address);

        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(result.ToJson(true));

        await operate.DisposeAsync();

    }
    */
    #endregion

    #region 单独UDP测试
    /*
    static async Task Main(string[] args)
    {
        UdpServiceOperate udpService = UdpServiceOperate.Instance(new());
        udpService.OnInfoEventAsync += UdpService_OnInfoEventAsync;
        udpService.OnDataEventAsync += UdpService_OnDataEventAsync;
        OperateResult result = await udpService.OnAsync();
        Console.WriteLine(result.ToJson(true));

        UdpClientOperate udpClient = UdpClientOperate.Instance(new());
        udpClient.OnInfoEventAsync += UdpClient_OnInfoEventAsync; ;
        udpClient.OnDataEventAsync += UdpClient_OnDataEventAsync; ;
        result = await udpClient.OnAsync();
        Console.WriteLine(result.ToJson(true));

        while (true)
        {
            string msg = "客户端往服务端发送数据";
            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            Console.WriteLine(msg);
            result = await udpClient.SendAsync(msgBytes);
            Console.WriteLine(result.ToJson(true));

            Console.ReadLine();

            msg = "服务端往客户端发送消息";
            msgBytes = Encoding.UTF8.GetBytes(msg);
            Console.WriteLine(msg);
            result = await udpService.SendAsync(msgBytes);
            Console.WriteLine(result.ToJson(true));

            Console.ReadLine();
        }

    }

    private static Task UdpClient_OnDataEventAsync(object? sender, EventDataResult e)
    {
        Console.WriteLine("\r\n 客户端收到消息 \r\n");
        Console.WriteLine(e.ToJson());
        return Task.CompletedTask;
    }

    private static Task UdpClient_OnInfoEventAsync(object? sender, EventInfoResult e)
    {
        Console.WriteLine("\r\n 客户端收到消息 \r\n");
        Console.WriteLine(e.ToJson());
        return Task.CompletedTask;
    }

    private static Task UdpService_OnDataEventAsync(object? sender, Model.data.EventDataResult e)
    {
        Console.WriteLine("\r\n 服务端收到消息 \r\n");
        Console.WriteLine(e.ToJson());
        return Task.CompletedTask;
    }

    private static Task UdpService_OnInfoEventAsync(object? sender, Model.data.EventInfoResult e)
    {
        Console.WriteLine("\r\n 服务端收到消息 \r\n");
        Console.WriteLine(e.ToJson());
        return Task.CompletedTask;
    }
    */
    #endregion

    #region core 整体测试
    /*
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║      Snet 完整测试套件                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.WriteLine($"  测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        int total = 0;

        try
        {
            // ── 通信模块 ──
            Console.WriteLine("\n>>> 套件 {0}/13: UDP Unicast (单播)", ++total);
            await UdpUnicastTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: UDP Broadcast (广播)", ++total);
            await UdpBroadcastTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: UDP Multicast (组播)", ++total);
            await UdpMulticastTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: TCP", ++total);
            await TcpTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: Serial (串口)", ++total);
            await SerialTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: WebSocket", ++total);
            await WsTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: HTTP", ++total);
            await HttpTest.RunAllAsync();

            // ── 基础设施模块 ──
            Console.WriteLine("\n>>> 套件 {0}/13: ShareCache (共享缓存)", ++total);
            await ShareCacheTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: ProcessCache (进程缓存)", ++total);
            await ProcessCacheTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: Channel (通道)", ++total);
            await ChannelTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: Handler (地址值处理)", ++total);
            await HandlerTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: BytesTransform (字节解析转换)", ++total);
            await BytesTransformTest.RunAllAsync();
            Console.WriteLine("\n>>> 套件 {0}/13: Reflection + MQ (反射+消息队列)", ++total);
            await ReflectionMqTest.RunAllAsync();

            Console.WriteLine("\n╔══════════════════════════════════════════════╗");
            Console.WriteLine("║              所有测试套件执行完毕            ║");
            Console.WriteLine("╚══════════════════════════════════════════════╝");
        }
        catch (Exception ex) { Console.WriteLine($"\n[FATAL] 测试执行异常: {ex}"); }

        Console.WriteLine($"\n  共 {total}/13 个测试套件执行完成");
        Console.WriteLine("按 Enter 键退出...");
        Console.ReadLine();
    }
    */
    #endregion

    #region 共享缓存
    /*
    static async Task Main(string[] args)
    {
        using (ShareCacheOperate shareCache = await ShareCacheOperate.InstanceAsync(new()))
        {
            Encoding encoding = Encoding.UTF8;
            byte[] bytes = encoding.GetBytes("你好");

            OperateResult result = await shareCache.SetCacheAsync("test", bytes);
            if (!result.GetDetails(out string? msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(result.ToJson(true));
            result = await shareCache.GetCacheAsync("test");
            if (!result.GetDetails(out msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(result.ToJson(true));
            bytes = result.ResultData.GetSource<byte[]>();
            Console.WriteLine(encoding.GetString(bytes));
        }
    } 
    */
    #endregion

    #region 进程缓存
    /*
    static async Task Main(string[] args)
    {
        using (ProcessCacheOperate processCache = await ProcessCacheOperate.InstanceAsync(new()))
        {
            OperateResult result = await processCache.SetCacheAsync("test", 123456);
            if (!result.GetDetails(out string? msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(result.ToJson(true));
            result = await processCache.GetCacheAsync<int>("test");
            if (!result.GetDetails(out msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(result.ToJson(true));
        }
    } 
    */
    #endregion

    #region 字节处理
    /*
    static async Task Main(string[] args)
    {
        //#region 大数据示例

        //#region 模拟字节
        ////模拟PLC一次性读取到40000个点的字节数据
        //List<byte> bytes = new List<byte>();
        //for (int i = 0; i < 10000; i++)
        //{
        //    bytes.AddRange(BitConverter.GetBytes(i));
        //    bytes.AddRange(BitConverter.GetBytes(i + 0.1f));
        //    bytes.AddRange(BitConverter.GetBytes(i + 0.123456d));
        //    bytes.AddRange(BitConverter.GetBytes(i % 2 == 0));
        //}
        ////得到的字节数据
        //byte[] buffer = bytes.ToArray();
        //#endregion


        //#region 配置入参
        ////创建入参 模拟需要解析 40000个地址
        //List<BytesModel> inModels = new();
        //const int INT_SIZE = 4;
        //const int FLOAT_SIZE = 4;
        //const int DOUBLE_SIZE = 8;
        //const int BOOL_SIZE = 1;
        //const int GROUP_SIZE = INT_SIZE + FLOAT_SIZE + DOUBLE_SIZE + BOOL_SIZE;
        //for (int i = 0; i < 10000; i++)
        //{
        //    int baseIndex = i * GROUP_SIZE;
        //    inModels.Add(new BytesModel($"address_{i}_int", $"num_{i}_int", baseIndex + 0, INT_SIZE, DataType.Int));
        //    inModels.Add(new BytesModel($"address_{i}_float", $"num_{i}_float", baseIndex + 4, FLOAT_SIZE, DataType.Float));
        //    inModels.Add(new BytesModel($"address_{i}_double", $"num_{i}_double", baseIndex + 8, DOUBLE_SIZE, DataType.Double));
        //    inModels.Add(new BytesModel($"address_{i}_bool", $"num_{i}_bool", baseIndex + 16, BOOL_SIZE, DataType.Bool));
        //}
        //#endregion


        //#region 实现解析转换
        ////创建一个单例
        //BytesHandler handle = BytesHandler.Instance(Guid.NewGuid().ToString());
        ////结果
        //OperateResult result;
        ////处理
        //for (int i = 1; i <= 1000000; i++)
        //{
        //    //处理
        //    result = await handle.TransformAsync(buffer, DateTime.Now, inModels);
        //    Console.WriteLine($"第{i}次处理40000个地址，总字节数：{bytes.Count}，耗时：{result.RunTime}");
        //}
        //#endregion 

        //#endregion

        #region 简单示例

        #region 模拟字节
        //模拟PLC一次性读取到点的字节数据
        List<byte> bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(123456f));
        bytes.AddRange(BitConverter.GetBytes(123456.1f));
        bytes.AddRange(BitConverter.GetBytes(123456.123456d));
        bytes.AddRange(BitConverter.GetBytes(11.223f));
        //得到的字节数据
        byte[] buffer = bytes.ToArray();
        #endregion


        #region 配置入参
        //创建入参 模拟需要解析地址
        List<BytesModel> inModels = new();
        inModels.Add(new BytesModel($"100", $"modbus_100_float", 0, 4, DataType.Float, dataFormat: DataFormat.DCBA));
        inModels.Add(new BytesModel($"102", $"modbus_102_float", 4, 4, DataType.Float, dataFormat: DataFormat.DCBA));
        inModels.Add(new BytesModel($"104", $"modbus_104_float", 8, 8, DataType.Double, dataFormat: DataFormat.DCBA));
        inModels.Add(new BytesModel($"106", $"modbus_106_float", 16, 4, DataType.Float, dataFormat: DataFormat.DCBA));
        #endregion


        #region 实现解析转换
        //创建一个单例
        BytesHandler handle = BytesHandler.Instance(Guid.NewGuid().ToString());
        //处理
        while (true)
        {
            OperateResult result = await handle.TransformAsync(buffer, DateTime.Now, inModels);
            Console.WriteLine($"处理地址，总字节数：{bytes.Count}，耗时：{result.RunTime}");
            Console.WriteLine(result.ToJson(true));
            await Task.Delay(1000);
        }
        #endregion

        #endregion
    }
    */
    #endregion

    #region 通道操作
    /*
    static async Task Main(string[] args)
    {
        Snet.Core.channel.ChannelOperate<string> operate = await Snet.Core.channel.ChannelOperate<string>.InstanceAsync(new());
        Snet.Model.data.OperateResult result = await operate.WriteAsync("111", CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine(operate.Count);
        Console.WriteLine();
        Console.WriteLine(result.ToJson(true));
        result = await operate.ReadAsync(CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine(operate.Count);
        Console.WriteLine();
        Console.WriteLine(result.ToJson(true));


        result = await operate.ReadWaitAsync(5000);
        Console.WriteLine();
        Console.WriteLine(operate.Count);
        Console.WriteLine();
        Console.WriteLine(result.ToJson(true));

        await operate.DisposeAsync();


        operate = await Snet.Core.channel.ChannelOperate<string>.InstanceAsync(new() { IsSync = false });
        operate.OnDataEventAsync -= Operate_OnDataEventAsync;
        operate.OnDataEventAsync += Operate_OnDataEventAsync;
        operate.OnInfoEventAsync -= Operate_OnInfoEventAsync;
        operate.OnInfoEventAsync += Operate_OnInfoEventAsync;
        result = await operate.WriteAsync("222", CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine(operate.Count);
        Console.WriteLine();
        Console.WriteLine(result.ToJson(true));
        await operate.DisposeAsync();



        await operate.ResetChannelAsync();
        operate.OnDataEventAsync -= Operate_OnDataEventAsync;
        operate.OnDataEventAsync += Operate_OnDataEventAsync;
        operate.OnInfoEventAsync -= Operate_OnInfoEventAsync;
        operate.OnInfoEventAsync += Operate_OnInfoEventAsync;
        result = await operate.WriteAsync("333", CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine(operate.Count);
        Console.WriteLine();
        Console.WriteLine(result.ToJson(true));
        await operate.DisposeAsync();

        Console.ReadLine();
    }
    private static async Task Operate_OnInfoEventAsync(object? sender, EventInfoResult e)
    {
        Console.WriteLine();
        Console.WriteLine(e.ToJson(true));
        Console.WriteLine();
    }

    private static async Task Operate_OnDataEventAsync(object? sender, EventDataResult e)
    {
        Console.WriteLine();
        Console.WriteLine(e.ToJson(true));
        Console.WriteLine();
    }
    */
    #endregion

    #region 地址与值的处理
    /*
    static async Task Main(string[] args)
    {
        AddressDetails details = new AddressDetails()
        {
            AddressName = "address.test",
            AddressDataType = Model.@enum.DataType.Float
        };

        while (true)
        {
            AddressValue? addressValue = AddressHandler.ExecuteDispose(details, 1.1f, "成功");
            Console.WriteLine(addressValue?.ToJson(true));

            addressValue = AddressHandler.ExecuteDispose(details, float.NaN, "成功");
            Console.WriteLine(addressValue?.ToJson(true));

            details.AddressDataType = Model.@enum.DataType.Ushort;
            addressValue = AddressHandler.ExecuteDispose(details, 123444444444444444, "成功");
            Console.WriteLine(addressValue?.ToJson(true));

            Console.ReadLine();
        }
    } 
    */
    #endregion

    #region 反射+MQ操作
    /*
    static async Task Main(string[] args)
    {
        //创建MQ服务端，通过反射的形式创建
        string basePath = AppContext.BaseDirectory;
        dynamic ms1 = new ExpandoObject();
        ms1.IpAddress = "127.0.0.1"; ms1.Port = 8111; ms1.UserName = "shunnet"; ms1.Password = "shunnet";
        dynamic ms2 = new ExpandoObject();
        ms2.IpAddress = "127.0.0.1"; ms2.Port = 8222; ms2.UserName = "shunnet"; ms2.Password = "shunnet";
        //通过反射加载DLL
        ReflectionData.Basics rb = new()
        {
            DllDatas =
            [
                new()
                {
                     DllPath=Path.Combine(basePath,"lib","reflection","Snet.Mqtt.Pack","Snet.Mqtt.dll"),
                     IsAbsolutePath=true,
                     NamespaceDatas=
                     [
                          new()  //命名空间
                          {
                                Namespace="Snet.Mqtt.service",
                                ClassDatas=
                                [
                                    new ()  //命名空间的类名
                                    {
                                          ConstructorParam=[ms1],
                                          ClassName="MqttServiceOperate",
                                          SN="[MqttService1]",
                                          MethodDatas=
                                          [
                                             new ()  //方法
                                             {
                                                  MethodName="OnAsync",
                                                  SN="[OnAsync]"
                                             }
                                          ]
                                    },
                                    new ()  //命名空间的类名
                                    {
                                          ConstructorParam=[ms2],
                                          ClassName="MqttServiceOperate",
                                          SN="[MqttService2]",
                                          MethodDatas=
                                          [
                                             new ()  //方法
                                             {
                                                  MethodName="OnAsync",
                                                  SN="[OnAsync]"
                                             }
                                          ]
                                    }
                                ],

                          }
                     ]
                }
            ]
        };

        ReflectionOperate reflection = await ReflectionOperate.InstanceAsync(rb);
        OperateResult result = await reflection.InitAsync();
        if (!result.GetDetails(out string? msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

        result = await reflection.ExecuteMethod("[MqttService1][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

        result = await reflection.ExecuteMethod("[MqttService2][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);








        Console.WriteLine("回车初始化MQ操作");
        //初始化MQ操作类
        MqOperate mqOperate = await MqOperate.InstanceAsync(new());
        Console.ReadLine();

        while (true)
        {
            result = await mqOperate.ProduceAsync("testtopic", "MQTT传输11111", ["Snet.Mqtt.client.MqttClientOperate.mqtt1"]);
            if (!result.GetDetails(out msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            result = await mqOperate.ProduceAsync("testtopic", "MQTT传输22222", ["Snet.Mqtt.client.MqttClientOperate.mqtt2"]);
            if (!result.GetDetails(out msg))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);

            Console.ReadLine();
        }
    } 
    */
    #endregion

    #region 反射操作
    /*
        static async Task Main(string[] args)
    {
        string basePath = AppContext.BaseDirectory;

         dynamic ms1 = new ExpandoObject();
        ms1.IpAddress = "127.0.0.1"; ms1.Port = 8111; ms1.UserName = "shunnet"; ms1.Password = "shunnet";
        dynamic ms2 = new ExpandoObject();
        ms2.IpAddress = "127.0.0.1"; ms2.Port = 8222; ms2.UserName = "shunnet"; ms2.Password = "shunnet";
        dynamic os = new ExpandoObject();
        os.IpAddress = "127.0.0.1";
        //通过反射加载DLL
        ReflectionData.Basics rb = new()
        {
            DllDatas =
            [
                 new()
                {
                     DllPath=Path.Combine(basePath,"lib","reflection","Snet.Opc.Pack","Snet.Opc.dll"),
                     IsAbsolutePath=true,
                     NamespaceDatas=
                     [
                          new()  //命名空间
                          {
                                Namespace="Snet.Opc.ua.service",
                                ClassDatas=
                                [
                                    new ()  //命名空间的类名
                                    {
                                          ConstructorParam=[os],
                                          ClassName="OpcUaServiceOperate",
                                          SN="[OpcUaService]",
                                          MethodDatas=
                                          [
                                             new ()  //方法
                                             {
                                                  MethodName="OnAsync",
                                                  SN="[OnAsync]"
                                             }
                                          ]
                                    }
                                ],

                          }
                     ]
                },
                new()
                {
                     DllPath=Path.Combine(basePath,"lib","reflection","Snet.Mqtt.Pack","Snet.Mqtt.dll"),
                     IsAbsolutePath=true,
                     NamespaceDatas=
                     [
                          new()  //命名空间
                          {
                                Namespace="Snet.Mqtt.service",
                                ClassDatas=
                                [
                                    new ()  //命名空间的类名
                                    {
                                          ConstructorParam=[ms1],
                                          ClassName="MqttServiceOperate",
                                          SN="[MqttService1]",
                                          MethodDatas=
                                          [
                                             new ()  //方法
                                             {
                                                  MethodName="OnAsync",
                                                  SN="[OnAsync]"
                                             }
                                          ]
                                    },
                                    new ()  //命名空间的类名
                                    {
                                          ConstructorParam=[ms2],
                                          ClassName="MqttServiceOperate",
                                          SN="[MqttService2]",
                                          MethodDatas=
                                          [
                                             new ()  //方法
                                             {
                                                  MethodName="OnAsync",
                                                  SN="[OnAsync]"
                                             }
                                          ]
                                    }
                                ],

                          }
                     ]
                }
            ]
        };

        ReflectionOperate reflection = await ReflectionOperate.InstanceAsync(rb);
        OperateResult result = await reflection.InitAsync();
        if (!result.GetDetails(out string? msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

        result = await reflection.ExecuteMethod("[MqttService1][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

        result = await reflection.ExecuteMethod("[MqttService2][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

        result = await reflection.ExecuteMethod("[OpcUaService][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
        if (!result.GetDetails(out msg))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            return;
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);

    }
    */
    #endregion
}
