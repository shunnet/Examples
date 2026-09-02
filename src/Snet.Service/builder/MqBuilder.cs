using Microsoft.Extensions.DependencyInjection;
using Snet.Kafka;
using Snet.Model.@interface;
using Snet.Mqtt.client;
using Snet.NetMQ;
using Snet.Netty.client;
using Snet.RabbitMQ;
using Snet.RocketMQ;
using Snet.Service.@interface;
using Snet.Service.registry;

namespace Snet.Service.builder
{
    public class MqBuilder : IMqBuilder
    {
        private readonly IServiceCollection _services;
        private readonly List<Func<Task>> _registrationTasks = new();
        private readonly MqRegistry _registry = new();
        internal MqBuilder(IServiceCollection services)
        {
            _services = services;
        }

        internal async Task ExecuteAsync()
        {
            await Task.WhenAll(_registrationTasks.Select(t => t()));
            _services.AddSingleton<IMqRegistry>(_registry);
        }

        private IMqBuilder Register<T>(string? sn, Func<Task<T>> factory) where T : class, IMq
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sn);
            _registrationTasks.Add(async () =>
            {
                var operate = await factory();
                _services.AddKeyedSingleton<IMq>(sn, operate);
                _registry.Add(sn, operate);
            });
            return this;
        }

        /// <inheritdoc/>
        public IMqBuilder AddMqttClient(MqttClientData.Basics basics) => Register(basics.SN, () => MqttClientOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IMqBuilder AddKafka(KafkaData.Basics basics) => Register(basics.SN, () => KafkaOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IMqBuilder AddRabbitMQ(RabbitMQData.Basics basics) => Register(basics.SN, () => RabbitMQOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IMqBuilder AddRocketMQ(RocketMQData.Basics basics) => Register(basics.SN, () => RocketMQOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IMqBuilder AddNetMQ(NetMQData.Basics basics) => Register(basics.SN, () => NetMQOperate.InstanceAsync(basics));

        /// <inheritdoc/>
        public IMqBuilder AddNettyClient(NettyClientData.Basics basics) => Register(basics.SN, () => NettyClientOperate.InstanceAsync(basics));
    }
}
