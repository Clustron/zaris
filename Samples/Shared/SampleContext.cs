using System;

namespace Clustron.Dkv.Samples.Shared
{
    /// <summary>
    /// Provides unique key scoping for each sample execution.
    /// Ensures no cross-sample or cross-run key collisions.
    /// </summary>
    public sealed class SampleContext
    {
        public string SampleName { get; }
        public string SessionId { get; }
        public string Prefix { get; }

        public SampleContext(string sampleName)
        {
            SampleName = sampleName;
            SessionId = Guid.NewGuid().ToString("N");
            Prefix = $"sample:{sampleName}:{SessionId}";
        }

        /// <summary>
        /// Creates a fully scoped key.
        /// </summary>
        public string Key(string suffix)
            => $"{Prefix}:{suffix}";

        public override string ToString()
            => Prefix;
    }
}
