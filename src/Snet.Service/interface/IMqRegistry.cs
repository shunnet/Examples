using Snet.Model.@interface;

namespace Snet.Service.@interface
{
    /// <summary>
    /// 消息中间件注册表（按 SN 索引，支持运行时移除）
    /// </summary>
    public interface IMqRegistry
    {
        /// <summary>按 SN 获取中间件，不存在返回 null</summary>
        IMq? Get(string sn);

        /// <summary>按 SN 尝试获取中间件</summary>
        bool TryGet(string sn, out IMq? mq);

        /// <summary>从索引移除中间件（不释放连接资源，由调用方决定是否 OffAsync）</summary>
        bool Remove(string sn);

        /// <summary>全部已注册中间件</summary>
        IReadOnlyCollection<IMq> All { get; }
    }
}
