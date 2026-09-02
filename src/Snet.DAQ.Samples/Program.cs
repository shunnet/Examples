using Snet.Log;
using Snet.Model.data;
using Snet.Mqtt.client;
using Snet.Mqtt.service;
using Snet.Opc.ua.client;
using Snet.Opc.ua.service;
using Snet.Utility;

//string basePath = AppContext.BaseDirectory;

//dynamic ms1 = new ExpandoObject();
//ms1.IpAddress = "127.0.0.1"; ms1.Port = 8111;
//dynamic ms2 = new ExpandoObject();
//ms2.IpAddress = "127.0.0.1"; ms1.Port = 8222;
////通过反射加载DLL
//ReflectionData.Basics rb = new()
//{
//    DllDatas =
//    [
//        new()
//        {
//             DllPath=Path.Combine(basePath,"lib","mq","Snet.Mqtt.Pack","Snet.Mqtt.dll"),
//             IsAbsolutePath=true,
//             NamespaceDatas=
//             [
//                  new()  //命名空间
//                  {
//                        Namespace="Snet.Mqtt.service",
//                        ClassDatas=
//                        [
//                            new ()  //命名空间的类名
//                            {
//                                  ConstructorParam=[ms1],
//                                  ClassName="MqttServiceOperate",
//                                  SN="[MqttService1]",
//                                  MethodDatas=
//                                  [
//                                     new ()  //方法
//                                     {
//                                          MethodName="OnAsync",
//                                          SN="[OnAsync]"
//                                     }
//                                  ]
//                            },
//                            new ()  //命名空间的类名
//                            {
//                                  ConstructorParam=[ms2],
//                                  ClassName="MqttServiceOperate",
//                                  SN="[MqttService2]",
//                                  MethodDatas=
//                                  [
//                                     new ()  //方法
//                                     {
//                                          MethodName="OnAsync",
//                                          SN="[OnAsync]"
//                                     }
//                                  ]
//                            }
//                        ],

//                  }
//             ]
//        }
//    ]
//};
//ReflectionOperate reflection = await ReflectionOperate.InstanceAsync(rb);
//OperateResult result = reflection.Init();
//if (!result.GetDetails(out string? msg))
//{
//    Console.ForegroundColor = ConsoleColor.Red;
//    Console.WriteLine(msg);
//    return;
//}
//Console.ForegroundColor = ConsoleColor.Green;
//Console.WriteLine(msg);
//result = await reflection.ExecuteMethod("[MqttService1][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
//if (!result.GetDetails(out msg))
//{
//    Console.ForegroundColor = ConsoleColor.Red;
//    Console.WriteLine(msg);
//    return;
//}
//Console.ForegroundColor = ConsoleColor.Green;
//Console.WriteLine(msg);
//result = await reflection.ExecuteMethod("[MqttService2][OnAsync]", [CancellationToken.None]).GetSource<Task<OperateResult>>();
//if (!result.GetDetails(out msg))
//{
//    Console.ForegroundColor = ConsoleColor.Red;
//    Console.WriteLine(msg);
//    return;
//}
//Console.ForegroundColor = ConsoleColor.Green;
//Console.WriteLine(msg);





Console.WriteLine(new MqttClientData.Basics() { IpAddress = "127.0.0.1", Port = 8111, Password = "samples", UserName = "samples", SN = "mqtt1", MessageExpirationTime = 10000 }.ToJson(true));

Console.WriteLine(new MqttClientData.Basics() { IpAddress = "127.0.0.1", Port = 8222, Password = "samples", UserName = "samples", SN = "mqtt2", MessageExpirationTime = 10000 }.ToJson(true));

//启动 OPCUA 服务端
OpcUaServiceOperate opcUaServiceOperate = OpcUaServiceOperate.Instance(new OpcUaServiceData.Basics
{
    AType = Snet.Opc.core.Data.AuType.UserName,
    UserName = "shunnet",
    Password = "shunnet"
});
//输出日志
LogHelper.Info(opcUaServiceOperate.On().ToJson(true));

//启动MQTT服务端 1
MqttServiceOperate mqttServiceOperate1 = MqttServiceOperate.Instance(new MqttServiceData.Basics
{
    MaxNumber = 1000,
    Password = "shunnet",
    UserName = "shunnet",
    Port = 8111
});
//输出日志
LogHelper.Info(mqttServiceOperate1.On().ToJson(true));
//启动MQTT服务端 2
MqttServiceOperate mqttServiceOperate2 = MqttServiceOperate.Instance(new MqttServiceData.Basics
{
    MaxNumber = 1000,
    Password = "shunnet",
    UserName = "shunnet",
    Port = 8222
});
//输出日志
LogHelper.Info(mqttServiceOperate2.On().ToJson(true));











//获取地址
string data = FileHandler.FileToString("config\\addressList.json");
Address? address = data.ToJsonEntity<Address>();

//注意,不管是脚本或反射解析,函数入参必须是两个参数,地址名称与地址值,处理完后返回值

#region 动态地址 + 动态单主题转发
//foreach (var item in address.AddressArray)
//{
//    //转发参数 一个主题多个数据
//    item.AddressMqParam = new AddressMq
//    {
//        ISns = new List<string> { "Snet.Mqtt.client.MqttClientOperate.mqtt1", "Snet.Mqtt.client.MqttClientOperate.mqtt2" },
//        Topic = $"TEST/AddressValue",
//        ContentFormat = item.AddressName + " ---- {0}"
//    };
//}
#endregion

#region 动态地址 + 动态多主题转发 
//foreach (var item in address.AddressArray)
//{
//    //转发参数 多个主题
//    item.AddressMqParam = new AddressMq
//    {
//        ISns = new List<string> { "Snet.Mqtt.client.MqttClientOperate.mqtt1", "Snet.Mqtt.client.MqttClientOperate.mqtt2" },
//        Topic = $"TEST/AddressValue/{item.AddressName}",
//    };
//}
#endregion

#region 动态地址 + 动态单主题转发 + 反射解析

foreach (var item in address.AddressArray)
{
    //解析参数
    item.AddressParseParam = new AddressParse
    {
        ReflectionParam = new object[]
                {
                    new Snet.Core.reflection.ReflectionData.Basics
                            {
                                DllDatas = new List<Snet.Core.reflection.ReflectionData.DllData>
                                {
                                    new Snet.Core.reflection.ReflectionData.DllData
                                    {
                                        DllPath="Snet.DAQ.Samples.DLL.dll",
                                        IsAbsolutePath=false,
                                        NamespaceDatas=new List<Snet.Core.reflection.ReflectionData.NamespaceData>
                                        {
                                            new Snet.Core.reflection.ReflectionData.NamespaceData
                                            {
                                                Namespace="Snet.DAQ.Samples.DLL",
                                                ClassDatas=new List<Snet.Core.reflection.ReflectionData.ClassData>
                                                {
                                                    new Snet.Core.reflection.ReflectionData.ClassData
                                                    {
                                                        ClassName="Class1",
                                                        SN="Snet.DAQ.Samples.DLL.Class1[Instance]",
                                                        MethodDatas=new List<Snet.Core.reflection.ReflectionData.MethodData>
                                                        {
                                                            new Snet.Core.reflection.ReflectionData.MethodData
                                                            {
                                                                MethodName="R1",
                                                                SN="[R1]"
                                                            },
                                                             new Snet.Core.reflection.ReflectionData.MethodData
                                                            {
                                                                MethodName="R2",
                                                                SN="[R2]"
                                                            },
                                                              new Snet.Core.reflection.ReflectionData.MethodData
                                                            {
                                                                MethodName="R3",
                                                                SN="[R3]"
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                    }
                                }
                            },
                            "Snet.DAQ.Samples.DLL.Class1[Instance][R2]"
                }
    };
    //转发参数 单个主题
    item.AddressMqParam = new AddressMq
    {
        ISns = new List<string> { "Snet.Mqtt.client.MqttClientOperate.mqtt1", "Snet.Mqtt.client.MqttClientOperate.mqtt2" },
        Topic = $"TEST/AddressValue",
        ContentFormat = item.AddressName + " ---- {0}"
    };

    ////转发参数 多个主题
    //item.AddressMqParam = new AddressMq
    //{
    //    ISns = new List<string> { "Snet.Mqtt.client.MqttClientOperate.mqtt1", "Snet.Mqtt.client.MqttClientOperate.mqtt2" },
    //    Topic = $"TEST/AddressValue/{item.AddressName}",
    //};

}

#endregion

//实例化daq对象
OpcUaClientOperate opcUaClientOperate = OpcUaClientOperate.Instance(new OpcUaClientData.Basics
{
    ServerUrl = "opc.tcp://127.0.0.1:6688/Opc.Ua.Service",
    CustomName = $"shunnet-{Guid.NewGuid().ToUpperNString()}",
});
OperateResult result = await opcUaClientOperate.OnAsync();
//打开
LogHelper.Info(result.ToJson(true));
//事件注册
opcUaClientOperate.OnDataEventAsync += OpcUaClientOperate_OnEvent;
opcUaClientOperate.OnInfoEventAsync += OpcUaClientOperate_OnInfoEventAsync;

Console.WriteLine("回车开始订阅数据");
Console.ReadLine();
//订阅数据
LogHelper.Info(opcUaClientOperate.Subscribe(address).ToJson(true));

while (true)
{
    Console.WriteLine("回车开始读取数据");
    Console.ReadLine();
    //读取一次数据
    LogHelper.Info(opcUaClientOperate.Read(address).ToJson(true));
}


//事件消息
Task OpcUaClientOperate_OnEvent(object? sender, EventDataResult e)
{
    //IEnumerable<AddressValueSimplify>? addressValueSimplifies = e.GetSource<ConcurrentDictionary<string, AddressValue>>()?.GetSimplifyArray();
    //if (addressValueSimplifies != null)
    //{
    //    foreach (var item in addressValueSimplifies)
    //    {
    //        Console.WriteLine($"{item.ToJson(true)}\r\n\r\n");
    //    }
    //}

    return Task.CompletedTask;
}

Task OpcUaClientOperate_OnInfoEventAsync(object? sender, EventInfoResult e)
{
    return Task.CompletedTask;
}
