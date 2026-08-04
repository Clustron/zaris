using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace Clustron.Zaris.Samples.Shared
{
    public static class SampleClientFactory
    {
        // Build a Zaris connection string from the sample options and connect. In-process vs remote is chosen by
        // the connection string alone (zaris://inproc/<store> vs zaris://host.../<store>) — the one public way to
        // create a client is ZarisClient.ConnectAsync(connectionString).
        public static Task<IZarisClient> ConnectAsync(ZarisOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            string connectionString;
            if (options.GetMode() == ZarisClientMode.Remote)
            {
                if (options.Seeds == null || !options.Seeds.Any())
                    throw new InvalidOperationException(
                        "Remote mode requires at least one seed server in configuration.");

                var hosts = string.Join(",", options.Seeds.Select(s => $"{s.Host}:{s.ClientPort}"));
                connectionString = $"zaris://{hosts}/{options.ClusterId}";
            }
            else
            {
                connectionString = $"zaris://inproc/{options.ClusterId}";
            }

            return string.IsNullOrWhiteSpace(options.LogFilePath)
                ? ZarisClient.ConnectAsync(connectionString)
                : ZarisClient.ConnectAsync(connectionString, o => o.LogFilePath = options.LogFilePath);
        }
    }
}
