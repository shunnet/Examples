using Snet.Model.data;
using Snet.Model.@enum;
using Snet.TEP.master;
using Snet.Utility;
using System.Collections.Concurrent;

namespace Snet.Tep.Service.Samples
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("请输入设备名称：");
            //string? DevName = Console.ReadLine();
            //Console.WriteLine("请输入设备ID：");
            //string? DevID = Console.ReadLine();

            string? DevName = "测试设备";
            string? DevID = "10086";

            string[] AddressArray = new string[] { "长度", "宽度", "高度", "质量", "密度", "状态" };
            DataType[] Unit = new DataType[] { DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool };
            Address address = new Address()
            {
                AddressArray = new List<AddressDetails>()
            };
            for (int i = 0; i < AddressArray.Length; i++)
            {
                address.AddressArray.Add(new AddressDetails()
                {
                    AddressName = $"{DevName}.{DevID}.{AddressArray[i]}",
                    AddressDataType = Unit[i],
                    AddressType = Model.@enum.AddressType.Reality
                });
            }
            Console.WriteLine("地址信息");
            Console.WriteLine(address.ToJson(true));
            TepMasterOperate serviceOperate = TepMasterOperate.Instance(new() { UserName = "test", Password = "test" });
            //启动WEBAPI 
            Console.WriteLine(serviceOperate.WAOn(new WAModel("127.0.0.1", 1996)).ToJson(true));
            Console.WriteLine(serviceOperate.WARequestExample().ResultData);
            serviceOperate.OnDataEvent += ServiceOperate_OnEvent;
            serviceOperate.OnInfoEvent += ServiceOperate_OnInfoEvent;
            OperateResult result;

            Console.WriteLine("1.打开");
            Console.WriteLine("2.关闭");
            Console.WriteLine("3.写入数据");
            Console.WriteLine("4.读取数据");
            Console.WriteLine("5.获取状态");
            Console.WriteLine("6.订阅");
            Console.WriteLine("7.取消订阅");
            Console.WriteLine("8.获取参数");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                try
                {
                    int index = (Console.ReadLine() ?? string.Empty).ToInt();
                    switch (index)
                    {
                        case 1:
                            result = serviceOperate.On();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 2:
                            result = serviceOperate.Off();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 3:
                            Console.WriteLine("准备写入数据，内定写入的数据如下，回车开始写入");
                            ConcurrentDictionary<string, object> writeData = new ConcurrentDictionary<string, object>();
                            for (int i = 0; i < AddressArray.Length; i++)
                            {
                                switch (Unit[i])
                                {
                                    case DataType.Bool:

                                        writeData[$"{DevName}.{DevID}.{AddressArray[i]}"] = new Random().Next() % 2 == 0;
                                        break;
                                    case DataType.String:
                                        writeData[$"{DevName}.{DevID}.{AddressArray[i]}"] = StringRandom(new Random());
                                        break;
                                    case DataType.Double:
                                        writeData[$"{DevName}.{DevID}.{AddressArray[i]}"] = new Random().NextDouble();
                                        break;
                                    case DataType.Float:
                                        writeData[$"{DevName}.{DevID}.{AddressArray[i]}"] = new Random().NextSingle();
                                        break;
                                    case DataType.Int:
                                        writeData[$"{DevName}.{DevID}.{AddressArray[i]}"] = new Random().Next();
                                        break;
                                }
                            }
                            Console.WriteLine(writeData.ToJson(true));
                            Console.ReadLine();
                            result = serviceOperate.Write(writeData);
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 4:
                            result = serviceOperate.Read(address);
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 5:
                            result = serviceOperate.GetStatus();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 6:
                            result = serviceOperate.Subscribe(address);
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 7:
                            result = serviceOperate.UnSubscribe(address);
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 8:
                            result = serviceOperate.GetArgs();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        default:
                            Console.WriteLine("输入有误");
                            break;
                    }
                }
                catch
                {
                    Console.WriteLine("输入有误");
                }
            }
        }

        private static void ServiceOperate_OnInfoEvent(object? sender, EventInfoResult e)
        {
            Console.WriteLine(e.ToJson(true));
        }

        private static void ServiceOperate_OnEvent(object? sender, EventDataResult e)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(e.ToJson(true));
            //if (e.RData is ConcurrentDictionary<string, AddressValue>)
            //{
            //    ConcurrentDictionary<string, AddressValue>? pairs = e.GetRData<ConcurrentDictionary<string, AddressValue>>();
            //    if (pairs != null)
            //    {
            //        foreach (var itemc in pairs)
            //        {
            //            Console.WriteLine($"{e.Message}\r\n键：{itemc.Key}\r\n值：{itemc.Value.Value}\r\n");
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 随机字符串
        /// </summary>
        /// <returns></returns>
        private static string StringRandom(Random random)
        {
            int stringlen = random.Next(4, 10);
            int randValue;
            string str = "";
            char letter;
            for (int i = 0; i < stringlen; i++)
            {
                randValue = random.Next(0, 26);
                letter = Convert.ToChar(randValue + 65);
                str = str + letter;
            }
            return str;
        }
    }
}
