using Snet.Kafka;
using Snet.Mqtt.client;
using Snet.NetMQ;
using Snet.Netty.client;
using Snet.RabbitMQ;
using Snet.RocketMQ;

namespace Snet.Service.@interface
{
    /// <summary>
    /// 传输注册构造器（全部消息中间件）
    /// </summary>
    public interface IMqBuilder
    {
        /// <summary>注册 MQTT 客户端（连接 Broker）</summary>
        IMqBuilder AddMqttClient(MqttClientData.Basics basics);

        /// <summary>注册 Kafka AdminClient/Producer/Consumer</summary>
        IMqBuilder AddKafka(KafkaData.Basics basics);

        /// <summary>注册 RabbitMQ Publish/Subscribe</summary>
        IMqBuilder AddRabbitMQ(RabbitMQData.Basics basics);

        /// <summary>注册 RocketMQ Publish/Subscribe（Apache RocketMQ 5.x，gRPC 连 Proxy）</summary>
        IMqBuilder AddRocketMQ(RocketMQData.Basics basics);

        /// <summary>注册 NetMQ Publish/Subscribe</summary>
        IMqBuilder AddNetMQ(NetMQData.Basics basics);

        /// <summary>注册 Netty 客户端</summary>
        IMqBuilder AddNettyClient(NettyClientData.Basics basics);
    }
}
