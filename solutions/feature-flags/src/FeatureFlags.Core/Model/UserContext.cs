namespace FeatureFlags.Model;

/// <summary>
/// The subject a flag is evaluated against — a user, an account, a device, anything with a stable
/// <see cref="Key"/>. The key is the <b>only</b> input to percentage-rollout bucketing, which is what
/// makes a rollout sticky: the same key always lands in the same bucket, independently of any server.
/// Attributes drive targeting rules (country, plan, email, betaOptIn, …).
/// </summary>
public sealed class UserContext
{
    /// <summary>Stable identifier used for rollout bucketing and explicit targeting. Required.</summary>
    public string Key { get; }

    /// <summary>Arbitrary attributes matched by targeting-rule clauses. Compared as strings
    /// (numeric clauses parse both sides as <see cref="double"/>).</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    public UserContext(string key, IReadOnlyDictionary<string, string>? attributes = null)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("User key is required.", nameof(key));
        Key = key;
        Attributes = attributes ?? new Dictionary<string, string>();
    }

    /// <summary>Fluent builder: <c>UserContext.For("u1").With("country","US").With("plan","pro")</c>.</summary>
    public static Builder For(string key) => new(key);

    public bool TryGetAttribute(string name, out string value)
    {
        if (string.Equals(name, "key", StringComparison.OrdinalIgnoreCase))
        {
            value = Key;
            return true;
        }
        return Attributes.TryGetValue(name, out value!);
    }

    public sealed class Builder
    {
        private readonly string _key;
        private readonly Dictionary<string, string> _attrs = new();
        internal Builder(string key) => _key = key;

        public Builder With(string name, string value)
        {
            _attrs[name] = value;
            return this;
        }

        public Builder With(string name, double value) => With(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public Builder With(string name, bool value) => With(name, value ? "true" : "false");

        public UserContext Build() => new(_key, _attrs);

        public static implicit operator UserContext(Builder b) => b.Build();
    }
}
