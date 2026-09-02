using Microsoft.Extensions.DependencyInjection;
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
using Snet.Model.@interface;
using Snet.Omron;
using Snet.Opc.da.client;
using Snet.Opc.da.http;
using Snet.Opc.ua.client;
using Snet.OrientalMotor;
using Snet.Panasonic;
using Snet.PQDIF;
using Snet.RKC;
using Snet.Service.@interface;
using Snet.Service.registry;
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

namespace Snet.Service.builder
{
    public class DaqBuilder : IDaqBuilder
    {
        private readonly IServiceCollection _services;
        private readonly List<Func<Task>> _registrationTasks = new();
        private readonly DaqRegistry _registry = new();
        internal DaqBuilder(IServiceCollection services)
        {
            _services = services;
        }

        internal async Task ExecuteAsync()
        {
            await Task.WhenAll(_registrationTasks.Select(t => t()));
            _services.AddSingleton<IDaqRegistry>(_registry);
        }

        private IDaqBuilder Register<T>(string? sn, Func<Task<T>> factory) where T : class, IDaq
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sn);
            _registrationTasks.Add(async () =>
            {
                var operate = await factory();
                _services.AddKeyedSingleton<IDaq>(sn, operate);
                _registry.Add(sn, operate);
            });
            return this;
        }

        /// <inheritdoc/>
        public IDaqBuilder AddSiemens(SiemensData.Basics basics) => Register(basics.SN, () => SiemensOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddModbus(ModbusData.Basics basics) => Register(basics.SN, () => ModbusOperate.InstanceAsync(basics));

        public IDaqBuilder AddMitsubishi(MitsubishiData.Basics basics) => Register(basics.SN, () => MitsubishiOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddOmron(OmronData.Basics basics) => Register(basics.SN, () => OmronOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddOrientalMotor(OrientalMotorData.Basics basics) => Register(basics.SN, () => OrientalMotorOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddInovance(InovanceData.Basics basics) => Register(basics.SN, () => InovanceOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddOpcUaClient(OpcUaClientData.Basics basics) => Register(basics.SN, () => OpcUaClientOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddOpcDaClient(OpcDaClientData.Basics basics) => Register(basics.SN, () => OpcDaClientOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddOpcDaHttp(OpcDaHttpData.Basics basics) => Register(basics.SN, () => OpcDaHttpOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddAllenBradley(AllenBradleyData.Basics basics) => Register(basics.SN, () => AllenBradleyOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddDelta(DeltaData.Basics basics) => Register(basics.SN, () => DeltaOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddKeyence(KeyenceData.Basics basics) => Register(basics.SN, () => KeyenceOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddKossi(KossiData.Basics basics) => Register(basics.SN, () => KossiOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddPanasonic(PanasonicData.Basics basics) => Register(basics.SN, () => PanasonicOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddInvt(InvtData.Basics basics) => Register(basics.SN, () => InvtOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddMegMeet(MegMeetData.Basics basics) => Register(basics.SN, () => MegMeetOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddBeckhoff(BeckhoffData.Basics basics) => Register(basics.SN, () => BeckhoffOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddGE(GEData.Basics basics) => Register(basics.SN, () => GEOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddYaskawa(YaskawaData.Basics basics) => Register(basics.SN, () => YaskawaOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddCimon(CimonData.Basics basics) => Register(basics.SN, () => CimonOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddFanuc(FanucData.Basics basics) => Register(basics.SN, () => FanucOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddFatek(FatekData.Basics basics) => Register(basics.SN, () => FatekOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddFuji(FujiData.Basics basics) => Register(basics.SN, () => FujiOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddLSis(LSisData.Basics basics) => Register(basics.SN, () => LSisOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddRKC(RKCData.Basics basics) => Register(basics.SN, () => RKCOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddToyota(ToyotaData.Basics basics) => Register(basics.SN, () => ToyotaOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddTurck(TurckData.Basics basics) => Register(basics.SN, () => TurckOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddVigor(VigorData.Basics basics) => Register(basics.SN, () => VigorOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddWeCon(WeConData.Basics basics) => Register(basics.SN, () => WeConOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddXinJE(XinJEData.Basics basics) => Register(basics.SN, () => XinJEOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddYamatake(YamatakeData.Basics basics) => Register(basics.SN, () => YamatakeOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddYokogawa(YokogawaData.Basics basics) => Register(basics.SN, () => YokogawaOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddYuDian(YuDianData.Basics basics) => Register(basics.SN, () => YuDianOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddPQDIF(PQDIFData.Basics basics) => Register(basics.SN, () => PQDIFOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddDB(DBData.Basics basics) => Register(basics.SN, () => DBOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddTepMaster(TepMasterData.Basics basics) => Register(basics.SN, () => TepMasterOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddFreedom(FreedomData.Basics basics) => Register(basics.SN, () => FreedomOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IDaqBuilder AddSim(SimData.Basics basics) => Register(basics.SN, () => SimOperate.InstanceAsync(basics));
    }
}
