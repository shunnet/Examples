using Snet.Model.@interface;
using Snet.Service.@interface;
using System.Collections.Concurrent;

namespace Snet.Service.registry
{
    public class MqRegistry : IMqRegistry
    {
        private readonly ConcurrentDictionary<string, IMq> _map = new();

        /// <inheritdoc/>
        internal void Add(string sn, IMq mq) => _map[sn] = mq;

        /// <inheritdoc/>
        public IMq? Get(string sn) => _map.GetValueOrDefault(sn);

        /// <inheritdoc/>
        public bool TryGet(string sn, out IMq? mq) => _map.TryGetValue(sn, out mq);

        /// <inheritdoc/>
        public bool Remove(string sn) => _map.TryRemove(sn, out _);

        /// <inheritdoc/>
        public IReadOnlyCollection<IMq> All => _map.Values.ToArray();
    }
}
