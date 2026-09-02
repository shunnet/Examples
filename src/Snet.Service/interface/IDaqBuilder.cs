using Snet.AllenBradley;
using Snet.Beckhoff;
using Snet.Cimon;
using Snet.DB;
using Snet.Delta;
using Snet.Fanuc;
using Snet.Fatek;
using Snet.Freedom;
using Snet.Fuji;
using Snet.GE;
using Snet.Inovance;
using Snet.Invt;
using Snet.Keyence;
using Snet.Kossi;
using Snet.LSis;
using Snet.MegMeet;
using Snet.Mitsubishi;
using Snet.Modbus;
using Snet.Omron;
using Snet.Opc.da.client;
using Snet.Opc.da.http;
using Snet.Opc.ua.client;
using Snet.OrientalMotor;
using Snet.Panasonic;
using Snet.PQDIF;
using Snet.RKC;
using Snet.Siemens;
using Snet.Sim;
using Snet.TEP.master;
using Snet.Toyota;
using Snet.Turck;
using Snet.Vigor;
using Snet.WeCon;
using Snet.XinJE;
using Snet.Yamatake;
using Snet.Yaskawa;
using Snet.Yokogawa;
using Snet.YuDian;

namespace Snet.Service.@interface
{
    /// <summary>
    /// 采集注册构建器（全部采集协议）
    /// </summary>
    public interface IDaqBuilder
    {
        /// <summary>注册 西门子 S7/PPI/S7Plus/FetchWrite</summary>
        IDaqBuilder AddSiemens(SiemensData.Basics basics);

        /// <summary>注册 Modbus TCP/UDP/RTU/ASCII/RTUoTCP/ASCIIoTCP</summary>
        IDaqBuilder AddModbus(ModbusData.Basics basics);

        /// <summary>注册 三菱 MC/FX/A1E/A3C/CIP/Links</summary>
        IDaqBuilder AddMitsubishi(MitsubishiData.Basics basics);

        /// <summary>注册 欧姆龙 Fins/CIP/HostLink/CMode</summary>
        IDaqBuilder AddOmron(OmronData.Basics basics);

        /// <summary>注册 东方马达 EIP 步进驱动器</summary>
        IDaqBuilder AddOrientalMotor(OrientalMotorData.Basics basics);

        /// <summary>注册 汇川 TCP/Serial/CIP/Easy/ComputerLink</summary>
        IDaqBuilder AddInovance(InovanceData.Basics basics);

        /// <summary>注册 OPC UA 客户端（Client，订阅 Tag）</summary>
        IDaqBuilder AddOpcUaClient(OpcUaClientData.Basics basics);

        /// <summary>注册 OPC DA 客户端</summary>
        IDaqBuilder AddOpcDaClient(OpcDaClientData.Basics basics);

        /// <summary>注册 OPC DA HTTP</summary>
        IDaqBuilder AddOpcDaHttp(OpcDaHttpData.Basics basics);

        /// <summary>注册 罗克韦尔 CIP/PCCC/SLC/DF1</summary>
        IDaqBuilder AddAllenBradley(AllenBradleyData.Basics basics);

        /// <summary>注册 台达 TCP/Serial/ASCII</summary>
        IDaqBuilder AddDelta(DeltaData.Basics basics);

        /// <summary>注册 基恩士 MC/Nano/KvOld</summary>
        IDaqBuilder AddKeyence(KeyenceData.Basics basics);

        /// <summary>注册 科伺 PLC（Omron CIP）</summary>
        IDaqBuilder AddKossi(KossiData.Basics basics);

        /// <summary>注册 松下 MC/Mewtocol</summary>
        IDaqBuilder AddPanasonic(PanasonicData.Basics basics);

        /// <summary>注册 英威腾（Modbus）</summary>
        IDaqBuilder AddInvt(InvtData.Basics basics);

        /// <summary>注册 麦格米特 TCP/Serial</summary>
        IDaqBuilder AddMegMeet(MegMeetData.Basics basics);

        /// <summary>注册 倍福 ADS</summary>
        IDaqBuilder AddBeckhoff(BeckhoffData.Basics basics);

        /// <summary>注册 通用电气 SRTP</summary>
        IDaqBuilder AddGE(GEData.Basics basics);

        /// <summary>注册 安川 Memobus TCP/UDP</summary>
        IDaqBuilder AddYaskawa(YaskawaData.Basics basics);

        /// <summary>注册 西蒙 PLC</summary>
        IDaqBuilder AddCimon(CimonData.Basics basics);

        /// <summary>注册 发那科 CNC/机器人</summary>
        IDaqBuilder AddFanuc(FanucData.Basics basics);

        /// <summary>注册 永宏 PLC</summary>
        IDaqBuilder AddFatek(FatekData.Basics basics);

        /// <summary>注册 富士 SPH/SPB</summary>
        IDaqBuilder AddFuji(FujiData.Basics basics);

        /// <summary>注册 LS产电 PLC</summary>
        IDaqBuilder AddLSis(LSisData.Basics basics);

        /// <summary>注册 理化温控器</summary>
        IDaqBuilder AddRKC(RKCData.Basics basics);

        /// <summary>注册 丰田机器人</summary>
        IDaqBuilder AddToyota(ToyotaData.Basics basics);

        /// <summary>注册 图尔克 IO-Link</summary>
        IDaqBuilder AddTurck(TurckData.Basics basics);

        /// <summary>注册 丰炜 PLC</summary>
        IDaqBuilder AddVigor(VigorData.Basics basics);

        /// <summary>注册 维控 PLC</summary>
        IDaqBuilder AddWeCon(WeConData.Basics basics);

        /// <summary>注册 信捷 PLC</summary>
        IDaqBuilder AddXinJE(XinJEData.Basics basics);

        /// <summary>注册 山武（AZBIL）</summary>
        IDaqBuilder AddYamatake(YamatakeData.Basics basics);

        /// <summary>注册 横河 PLC</summary>
        IDaqBuilder AddYokogawa(YokogawaData.Basics basics);

        /// <summary>注册 宇电 AIBus 温控器</summary>
        IDaqBuilder AddYuDian(YuDianData.Basics basics);

        /// <summary>注册 电力通讯规约（DLT645/DLT698/CJT188/DTSU6606）</summary>
        IDaqBuilder AddPQDIF(PQDIFData.Basics basics);

        /// <summary>注册 数据库采集（SqlServer/MySQL/Oracle/SQLite）</summary>
        IDaqBuilder AddDB(DBData.Basics basics);

        /// <summary>注册 TEP TCP 扩展主站（非标设备采集）</summary>
        IDaqBuilder AddTepMaster(TepMasterData.Basics basics);

        /// <summary>注册 自由协议（自定义报文）</summary>
        IDaqBuilder AddFreedom(FreedomData.Basics basics);

        /// <summary>注册 模拟库（无硬件测试）</summary>
        IDaqBuilder AddSim(SimData.Basics basics);
    }
}
