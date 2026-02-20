using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;

namespace Clustron.Dkv.Samples.Shared
{
    /// <summary>
    /// Strongly-typed configuration for sample client initialization.
    /// Keeps samples clean and explicit.
    /// </summary>
    public sealed class SampleClientOptions
    {
        public string ClusterId { get; set; } = "demo-cluster";

        public DkvClientMode Mode { get; set; } = DkvClientMode.Remote;

        public string? RemoteHost { get; set; } = "127.0.0.1";

        public int RemotePort { get; set; } = 9000;

        public string? LogFilePath { get; set; }
    }
}
