using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;

namespace Clustron.Dictus.Samples.Shared
{
    public static class SampleClientFactory
    {
        public static async Task<IDictusClient> ConnectAsync(DictusOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var mode = options.GetMode();

            if (mode == DictusClientMode.Remote)
            {
                if (options.Seeds == null || !options.Seeds.Any())
                    throw new InvalidOperationException(
                        "Remote mode requires at least one seed server in configuration.");

                return await DictusClient.InitializeRemote(
                    options.ClusterId,
                    options.Seeds,
                    options.LogFilePath);
            }

            return await DictusClient.InitializeInProc(
                options.ClusterId,
                options.LogFilePath);
        }
    }
}