using Microsoft.Extensions.DependencyInjection;
using Snet.Service.builder;
using Snet.Service.@interface;

namespace Snet.Service
{
    /// <summary>
    /// 服务注册扩展
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 统一注册所有 DAQ 设备
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="configure">配置委托（同步）</param>
        public static async Task<IServiceCollection> AddDaqAsync(this IServiceCollection services, Action<IDaqBuilder> configure)
        {
            var builder = new DaqBuilder(services);
            configure(builder);
            await builder.ExecuteAsync();
            return services;
        }

        /// <summary>
        /// 统一注册所有 Mq 设备
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="configure">配置委托（同步）</param>
        public static async Task<IServiceCollection> AddMqAsync(this IServiceCollection services, Action<IMqBuilder> configure)
        {
            var builder = new MqBuilder(services);
            configure(builder);
            await builder.ExecuteAsync();
            return services;
        }

    }
}
