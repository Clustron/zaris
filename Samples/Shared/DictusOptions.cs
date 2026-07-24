using System;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;

namespace Clustron.Dictus.Samples.Shared
{
    /// <summary>
    /// Represents configuration settings required to initialize a Dictus client.
    /// Bound from appsettings.json in each sample.
    /// </summary>
    public sealed class DictusOptions
    {
        public string ClusterId { get; set; } = default!;
        public string Mode { get; set; } = default!;
        public List<DictusNodeInfo>? Seeds { get; set; }
        public string? LogFilePath { get; set; }

        public DictusClientMode GetMode() =>
            Enum.Parse<DictusClientMode>(Mode, ignoreCase: true);
    }
}
