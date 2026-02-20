namespace Clustron.Dkv.Samples.Shared.Models
{
    public sealed class Customer
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = default!;

        public string Email { get; set; } = default!;
    }
}
