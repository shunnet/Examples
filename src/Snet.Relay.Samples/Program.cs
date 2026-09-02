
using Snet.Log;
using Snet.Model.data;
using Snet.Mqtt.client;
using Snet.Mqtt.service;
using Snet.Utility;

//正常操作流程

//服务端
MqttServiceOperate mqttServiceOperate = MqttServiceOperate.Instance(new MqttServiceData.Basics
{
    MaxNumber = 1000,
    Password = "shunnet",
    UserName = "shunnet",
    Port = 8111
});
//输出日志
LogHelper.Info(mqttServiceOperate.On().ToJson(true));


//客户端
MqttClientOperate mqttClientOperate = MqttClientOperate.Instance(new MqttClientData.Basics
{
    IpAddress = "127.0.0.1",
    Password = "shunnet",
    UserName = "shunnet",
    Port = 8845,
    //ResponseType = YSAI.Model.@enum.ResponseType.Content, //收到的就是内容
    //ResponseType = YSAI.Model.@enum.ResponseType.Bytes,   //发送字节string,收到到RData则是byte[]
    ResponseType = Snet.Model.@enum.ResponseType.ContentWithTopic   //收到的是一个字符串json 里面有主题与内容
});
//输出日志
LogHelper.Info(mqttClientOperate.On().ToJson(true));

//事件注册
mqttClientOperate.OnDataEvent += OnEvent;

//订阅一个主题
LogHelper.Info(mqttClientOperate.Consume("topic").ToJson(true));


while (true)
{
    string? con = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(con))
    {
        con = new Random().NextDouble().ToString();
    }
    //生产一个数据
    LogHelper.Info(mqttClientOperate.Produce("topic", con).ToJson(true));

    //如果当你要使用字节发送时
    //byte[] bytes = new byte[] { 0x00, 0x01 };
    //LogHelper.Info(mqttClientOperate.Produce("topic", bytes).ToJson(true));
}




//特殊操作




void OnEvent(object? sender, EventDataResult e)
{
    LogHelper.Info(e.ToJson(true));

    //当你收到的内容需要主题时,并且实例化是 RT 使用了 YSAI.Model.@enum.ResponseType.ContentWithTopic
    ResponseModel? model = e.GetSource<string>().ToJsonEntity<ResponseModel>();

    Console.WriteLine();
}





