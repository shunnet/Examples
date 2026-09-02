using Snet.Model.data;
using Snet.Model.@enum;
using Snet.TEP.core;
using Snet.TEP.slave;
using Snet.Utility;
using System.Text;
using static Snet.TEP.core.CoreBasicsData;

namespace Snet.Tep.Client.Samples
{
    internal class Program
    {
        public static long GetBytes(List<byte> list)
        {
            return list?.Count ?? 0;
        }

        public static double GetKB(List<byte> list)
        {
            return (list?.Count ?? 0) / 1024.0;
        }

        public static double GetMB(List<byte> list)
        {
            return (list?.Count ?? 0) / (1024.0 * 1024.0);
        }

        public static double GetGB(List<byte> list)
        {
            return (list?.Count ?? 0) / (1024.0 * 1024.0 * 1024.0);
        }

        public static string GetFormattedSize(List<byte> list)
        {
            long bytes = GetBytes(list);

            if (bytes >= 1073741824) // 1 GB
                return $"{GetGB(list):F2} GB";
            else if (bytes >= 1048576) // 1 MB
                return $"{GetMB(list):F2} MB";
            else if (bytes >= 1024) // 1 KB
                return $"{GetKB(list):F2} KB";
            else
                return $"{bytes} B";
        }

        public static string GetFormattedSizeFromBytes(long bytes)
        {
            if (bytes >= 1073741824) // 1 GB
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            else if (bytes >= 1048576) // 1 MB
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else if (bytes >= 1024) // 1 KB
                return $"{bytes / 1024.0:F2} KB";
            else
                return $"{bytes} B";
        }
        static async Task Main(string[] args)
        {
            ////先虚拟出 设备名称与ID
            //string DevName = "测试设备";
            //string DevID = new Random().Next(10000, 999999).ToString();


            ////第一步，先实例化对象，用户账号密码就是服务端内置的账号密码，做身份认证使用，其余参数为默认值即可
            //TepSlaveOperate clientOperate = TepSlaveOperate.Instance(new TepSlaveData.Basics
            //{
            //    Ip = "127.0.0.1",
            //    Port = 6688,
            //    DevName = DevName,
            //    DevID = DevID,
            //    UserName = "test",
            //    Password = "test"
            //});


            ////第二步，设置方法
            //bool 服务端获取状态会进入到这里面()
            //{
            //    return true;
            //}
            //OperateResult 服务端往客户端写入点位数据会进入到这里面(List<CoreBasicsData.KeyValue> keyValues)
            //{
            //    //可以写入的返回
            //    return new OperateResult(true, "写入成功", 1);

            //    //不可以写入的返回
            //    return new OperateResult(true, "写入失败，不支持写入功能", 1);
            //}
            //clientOperate.SetBaseStateFunc(服务端获取状态会进入到这里面);
            //clientOperate.SetBaseWriteFunc(服务端往客户端写入点位数据会进入到这里面);


            ////第三步，打开
            //OperateResult result = clientOperate.On();
            ////输出结果
            //Console.WriteLine(result.ToJson(true));
            ////状态判断
            //if (result.State)
            //{
            //    //第四步，事件注册
            //    clientOperate.OnEvent += ClientOperate_OnEvent;
            //}


            ////当你要上传数据时就执行数据上传方法
            //result = clientOperate.DataUpload(new List<KeyValue>() { new KeyValue { Key = "键", Value = "值" } });
            ////输出结果
            //Console.WriteLine(result.ToJson(true));


            Console.WriteLine("请输入设备名称:");
            string DevName = Console.ReadLine();
            Console.WriteLine("请输入设备ID:");
            string DevID = Console.ReadLine();
            if (DevName.IsNullOrWhiteSpace() || DevID.IsNullOrWhiteSpace())
            {
                Console.WriteLine("用户名称或ID不能为空");
                return;
            }

            TepSlaveData.Basics basics = new TepSlaveData.Basics()
            {
                IpAddress = "127.0.0.1",
                DevName = DevName,
                DevID = DevID
            };


            Console.Title = $"服务器信息 {basics.IpAddress}:{basics.Port} - {DevName} - {DevID}";

            Console.WriteLine("基础数据：" + basics.ToJson(true));
            List<KeyValue> keyValues = new List<KeyValue>();
            TepSlaveOperate clientOperate = TepSlaveOperate.Instance(basics);
            bool 服务端获取状态会进入到这里面()
            {
                return true;
            }
            OperateResult 服务端往客户端写入点位数据会进入到这里面(List<CoreBasicsData.KeyValue> keyValues)
            {
                //不可以写入的返回
                //return OperateResult.CreateFailureResult("不支持写入功能");
                OperateResult result = clientOperate.DataUploadAsync(keyValues).GetAwaiter().GetResult();
                if (result.GetDetails(out string? message))
                {
                    return OperateResult.CreateSuccessResult(message);
                }
                else
                {
                    return OperateResult.CreateFailureResult(message);
                }

            }

            int count = 0;
            int fail_count = 0;
            List<byte> bytes = new List<byte>();
            object bytesLock = new object();
            Task.Run(async () =>
            {

                while (true)
                {
                    await Task.Delay(1000);
                    int c = Interlocked.Exchange(ref count, 0);
                    int fc = Interlocked.Exchange(ref fail_count, 0);
                    long byteCount;
                    lock (bytesLock)
                    {
                        byteCount = GetBytes(bytes);
                        bytes.Clear();
                    }
                    Console.WriteLine($"一秒钟成功发送:{c} 次，发送失败:{fc} 次，共发送:{GetFormattedSizeFromBytes(byteCount)}");
                }

            });

            //string DevName = $"测试设备{StringRandom(new Random())}";
            //string DevID = new Random().Next(100000, 999999).ToString();

            //Console.WriteLine("请输入设备名称：");
            //string? DevName = Console.ReadLine();
            //Console.WriteLine("请输入设备ID：");
            //string? DevID = Console.ReadLine();
            string[] AddressArray = new string[] { "长度0", "宽度0", "高度0", "质量0", "密度0", "状态0", "长度1", "宽度1", "高度1", "质量1", "密度1", "状态1", "长度2", "宽度2", "高度2", "质量2", "密度2", "状态2", "长度3", "宽度3", "高度3", "质量3", "密度3", "状态3", "长度4", "宽度4", "高度4", "质量4", "密度4", "状态4", "长度5", "宽度5", "高度5", "质量5", "密度5", "状态5" };
            DataType[] Unit = new DataType[] { DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool, DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool, DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool, DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool, DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool, DataType.Int, DataType.Float, DataType.Double, DataType.Float, DataType.String, DataType.Bool };




            //设备数据写入响应
            clientOperate.SetBaseWriteFunc(服务端往客户端写入点位数据会进入到这里面);
            //设备状态响应
            clientOperate.SetBaseStateFunc(服务端获取状态会进入到这里面);

            clientOperate.OnInfoEvent += ClientOperate_OnEvent;
            clientOperate.OnInfoEvent += ClientOperate_OnInfoEvent;
            OperateResult result = null;


            for (int i = 0; i < AddressArray.Length; i++)
            {
                Console.WriteLine($"{DevName}.{DevID}.{AddressArray[i]}      ----      {Unit[i]}");
            }





            Console.WriteLine("0.压力测试");
            Console.WriteLine("1.打开");
            Console.WriteLine("2.关闭");
            Console.WriteLine("3.数据上传");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                try
                {
                    int index = Console.ReadLine().ToInt();
                    switch (index)
                    {
                        case 0:
                            result = clientOperate.On();
                            Console.WriteLine(result.ToJson(true));
                            if (!result.Status)
                            {
                                Console.WriteLine("连接失败，无法启动压力测试");
                                break;
                            }
                            while (true)
                            {
                                try
                                {
                                    keyValues = new List<KeyValue>();
                                    for (int i = 0; i < AddressArray.Length; i++)
                                    {
                                        switch (Unit[i])
                                        {
                                            case DataType.Bool:
                                                keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().Next() % 2 == 0 });
                                                break;
                                            case DataType.String:
                                                keyValues.Add(new KeyValue { Key = AddressArray[i], Value = StringRandom(new Random()) });
                                                break;
                                            case DataType.Double:
                                                keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().NextDouble() });
                                                break;
                                            case DataType.Float:
                                                keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().NextSingle() });
                                                break;
                                            case DataType.Int:
                                                keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().Next() });
                                                break;
                                        }
                                    }
                                    result = await clientOperate.DataUploadAsync(keyValues);

                                    if (result.Status)
                                    {
                                        string str = keyValues.ToJson();
                                        byte[] bs = Encoding.UTF8.GetBytes(str);
                                        lock (bytesLock)
                                        {
                                            bytes.AddRange(bs);
                                        }
                                        Interlocked.Increment(ref count);
                                    }
                                    else
                                    {
                                        Interlocked.Increment(ref fail_count);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Interlocked.Increment(ref fail_count);
                                    Console.WriteLine($"上传异常: {ex.Message}");
                                }

                                //Console.WriteLine(result.ToJson(true));
                            }
                            break;
                        case 1:
                            result = clientOperate.On();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 2:
                            result = clientOperate.Off();
                            Console.WriteLine(result.ToJson(true));
                            break;
                        case 3:
                            Console.WriteLine("准备上传数据，内定上传的数据如下，回车开始上传");
                            keyValues = new List<KeyValue>();
                            for (int i = 0; i < AddressArray.Length; i++)
                            {
                                switch (Unit[i])
                                {
                                    case DataType.Bool:
                                        keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().Next() % 2 == 0 });
                                        break;
                                    case DataType.String:
                                        keyValues.Add(new KeyValue { Key = AddressArray[i], Value = StringRandom(new Random()) });
                                        break;
                                    case DataType.Double:
                                        keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().NextDouble() });
                                        break;
                                    case DataType.Float:
                                        keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().NextSingle() });
                                        break;
                                    case DataType.Int:
                                        keyValues.Add(new KeyValue { Key = AddressArray[i], Value = new Random().Next() });
                                        break;
                                }
                            }
                            Console.WriteLine(keyValues.ToJson(true));
                            Console.ReadLine();
                            result = clientOperate.DataUpload(keyValues);
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

        private static void ClientOperate_OnInfoEvent(object? sender, EventInfoResult e)
        {
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
        private static void ClientOperate_OnEvent(object? sender, Model.data.EventInfoResult e)
        {
            Console.WriteLine(e.ToJson(true));
        }
    }
}
