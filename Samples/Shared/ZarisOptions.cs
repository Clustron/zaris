using System;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace Clustron.Zaris.Samples.Shared
{
    /// <summary>
    /// Represents configuration settings required to initialize a Zaris client.
    /// Bound from appsettings.json in each sample.
    /// </summary>
    public sealed class ZarisOptions
    {
        public string ClusterId { get; set; } = default!;
        public string Mode { get; set; } = default!;
        public List<ZarisNodeInfo>? Seeds { get; set; }
        public string? LogFilePath { get; set; }

        public ZarisClientMode GetMode() =>
            Enum.Parse<ZarisClientMode>(Mode, ignoreCase: true);
    }
}
