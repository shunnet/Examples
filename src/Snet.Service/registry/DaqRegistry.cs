using Snet.Model.@interface;
using Snet.Service.@interface;
using System.Collections.Concurrent;

namespace Snet.Service.registry
{
    public class DaqRegistry : IDaqRegistry
    {
        private readonly ConcurrentDictionary<string, IDaq> _map = new();

        /// <inheritdoc/>
        internal void Add(string sn, IDaq daq) => _map[sn] = daq;

        /// <inheritdoc/>
        public IDaq? Get(string sn) => _map.GetValueOrDefault(sn);

        /// <inheritdoc/>
        public bool TryGet(string sn, out IDaq? daq) => _map.TryGetValue(sn, out daq);

        /// <inheritdoc/>
        public bool Remove(string sn) => _map.TryRemove(sn, out _);

        /// <inheritdoc/>
        public IReadOnlyCollection<IDaq> All => _map.Values.ToArray();
    }
}
