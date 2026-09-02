using Snet.Model.@interface;

namespace Snet.Service.@interface
{
    /// <summary>
    /// 采集设备注册表（按 SN 索引，支持运行时移除）
    /// </summary>
    public interface IDaqRegistry
    {
        /// <summary>按 SN 获取设备，不存在返回 null</summary>
        IDaq? Get(string sn);

        /// <summary>按 SN 尝试获取设备</summary>
        bool TryGet(string sn, out IDaq? daq);

        /// <summary>从索引移除设备（不释放连接资源，由调用方决定是否 OffAsync）</summary>
        bool Remove(string sn);

        /// <summary>全部已注册设备</summary>
        IReadOnlyCollection<IDaq> All { get; }
    }
}
