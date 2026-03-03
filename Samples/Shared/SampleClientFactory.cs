using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;

namespace Clustron.Dkv.Samples.Shared
{
    public static class SampleClientFactory
    {
        public static async Task<IDkvClient> ConnectAsync(DkvOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var mode = options.GetMode();

            if (mode == DkvClientMode.Remote)
            {
                if (options.Seeds == null || !options.Seeds.Any())
                    throw new InvalidOperationException(
                        "Remote mode requires at least one seed server in configuration.");

                return await DKVClient.InitializeRemote(
                    options.ClusterId,
                    options.Seeds,
                    options.LogFilePath);
            }

            return await DKVClient.InitializeInProc(
                options.ClusterId,
                options.LogFilePath);
        }
    }
}