using System;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;

namespace Clustron.Dkv.Samples.Shared
{
    /// <summary>
    /// Represents configuration settings required to initialize a DKV client.
    /// Bound from appsettings.json in each sample.
    /// </summary>
    public sealed class DkvOptions
    {
        public string ClusterId { get; set; } = default!;
        public string Mode { get; set; } = default!;
        public List<DkvServerInfo>? Seeds { get; set; }
        public string? LogFilePath { get; set; }

        public DkvClientMode GetMode() =>
            Enum.Parse<DkvClientMode>(Mode, ignoreCase: true);
    }
}
