using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;

namespace Clustron.Dictus.Samples.Shared
{
    /// <summary>
    /// Strongly-typed configuration for sample client initialization.
    /// Keeps samples clean and explicit.
    /// </summary>
    public sealed class SampleClientOptions
    {
        public string ClusterId { get; set; } = "demo-cluster";

        public DictusClientMode Mode { get; set; } = DictusClientMode.Remote;

        public string? RemoteHost { get; set; } = "";

        public int RemotePort { get; set; } = 9000;

        public string? LogFilePath { get; set; }
    }
}
