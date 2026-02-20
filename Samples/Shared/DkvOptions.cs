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

        public string? RemoteHost { get; set; }

        public int RemotePort { get; set; }

        public string? LogFilePath { get; set; }

        public DkvClientMode GetMode()
        {
            if (!Enum.TryParse<DkvClientMode>(Mode, ignoreCase: true, out var mode))
                throw new InvalidOperationException($"Invalid DkvClientMode: '{Mode}'");

            return mode;
        }

        public bool IsRemote => GetMode() == DkvClientMode.Remote;
    }
}
