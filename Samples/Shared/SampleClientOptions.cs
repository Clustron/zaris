using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace Clustron.Zaris.Samples.Shared
{
    /// <summary>
    /// Strongly-typed configuration for sample client initialization.
    /// Keeps samples clean and explicit.
    /// </summary>
    public sealed class SampleClientOptions
    {
        public string ClusterId { get; set; } = "demo-cluster";

        public ZarisClientMode Mode { get; set; } = ZarisClientMode.Remote;

        public string? RemoteHost { get; set; } = "";

        public int RemotePort { get; set; } = 9000;

        public string? LogFilePath { get; set; }
    }
}
