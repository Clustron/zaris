using System.Collections.Generic;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;

namespace Clustron.Dkv.Samples.Shared
{
    /// <summary>
    /// Tracks created keys and cleans them at the end of sample execution.
    /// </summary>
    public sealed class SampleCleanup
    {
        private readonly List<string> _keys = new();

        public void Track(string key)
        {
            _keys.Add(key);
        }

        public async Task CleanupAsync(IDkvClient client)
        {
            foreach (var key in _keys)
            {
                await client.DeleteAsync(key);
            }
        }
    }
}
