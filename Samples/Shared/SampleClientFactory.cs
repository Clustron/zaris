using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace Clustron.Zaris.Samples.Shared
{
    public static class SampleClientFactory
    {
        public static async Task<IZarisClient> ConnectAsync(ZarisOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var mode = options.GetMode();

            if (mode == ZarisClientMode.Remote)
            {
                if (options.Seeds == null || !options.Seeds.Any())
                    throw new InvalidOperationException(
                        "Remote mode requires at least one seed server in configuration.");

                return await ZarisClient.InitializeRemote(
                    options.ClusterId,
                    options.Seeds,
                    options.LogFilePath);
            }

            return await ZarisClient.InitializeInProc(
                options.ClusterId,
                options.LogFilePath);
        }
    }
}