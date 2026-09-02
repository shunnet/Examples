namespace Snet.DAQ.Samples.DLL
{
    public class Class1
    {
        public string R1(string addressname, string value)
        {
            return $"【这是调用反射动态库解析】传入的地址是：{addressname}----传入的参数是：{value} - R1";
        }
        public string R2(string addressname, string value)
        {
            return $"【这是调用反射动态库解析】传入的地址是：{addressname}----传入的参数是：{value} - R2";
        }
        public string R3(string addressname, string value)
        {
            return $"【这是调用反射动态库解析】传入的地址是：{addressname}----传入的参数是：{value} - R3";
        }
    }
}
