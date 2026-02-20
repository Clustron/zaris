using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;

namespace Clustron.Dkv.Samples.Shared
{
    public static class SampleClientFactory
    {
        public static Task<IDkvClient> ConnectAsync(DkvOptions options)
        {
            return DKVClient.Initialize(
                clusterId: options.ClusterId,
                mode: options.GetMode(),
                remoteHost: options.IsRemote ? options.RemoteHost : null,
                remotePort: options.IsRemote ? options.RemotePort : 0,
                logFilePath: options.LogFilePath
            );
        }
    }
}
