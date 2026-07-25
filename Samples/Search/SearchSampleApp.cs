using System;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.Helpers;
using Clustron.Zaris.Samples.Shared;
using Clustron.Zaris.Samples.Shared.Models;

namespace Clustron.Zaris.Sample.Search;

internal class SearchSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";
    private const string EntityName = "search-sample-customer";

    public SearchSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("search-sample");

        var zaris = (IZaris)client;
        var scan = zaris.Scan;

        // -------------------------------------------------
        // Seed Data
        // -------------------------------------------------

        ConsoleHelper.Section("Seeding Customers");

        var cities = new[] { "London", "New York", "Berlin" };

        for (int i = 1; i <= 30; i++)
        {
            var city = cities[i % cities.Length];
            var age = 25 + (i % 10);

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = $"Customer-{i}",
                Email = $"user{i}@example.com"
            };

            var key = context.Key($"cust:{i}");

            var putOpts = Put.WithEntity(EntityName)
                .WithLabel("city", city)
                .WithLabel("age", age.ToString())
                .WithLabel("email", customer.Email);

            await client.PutAsync(key, customer, putOpts);
        }

        // Deterministic AND result
        var special = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Berlin-32",
            Email = "berlin32@example.com"
        };

        var specialKey = context.Key("cust:special");

        await client.PutAsync(
            specialKey,
            special,
            Put.WithEntity(EntityName)
                .WithLabel("city", "Berlin")
                .WithLabel("age", "32")
                .WithLabel("email", special.Email));

        Console.WriteLine("Seeded dataset.\n");

        var opts = new SearchOptions { PageSize = 10 };

        // -------------------------------------------------
        // Equality Search
        // -------------------------------------------------

        ConsoleHelper.Section("Eq: city = London");

        var eqQuery = SearchQuery
            .For(EntityName)
            .Eq("city", "London");

        await using (var reader =
            await (await scan.SearchAsync(eqQuery, opts)).AsEntities<Customer>())
        {
            while (await reader.ReadAsync())
                Console.WriteLine($"{reader.Current.Name} | {reader.Current.Email}");
        }

        // -------------------------------------------------
        // Range Search
        // -------------------------------------------------

        ConsoleHelper.Section("Range: age 28–32");

        var rangeQuery = SearchQuery
            .For(EntityName)
            .Range("age", 28, 32);

        await using (var reader =
            await (await scan.SearchAsync(rangeQuery, opts)).AsEntries())
        {
            while (await reader.ReadAsync())
            {
                var labels = reader.Current.Metadata.Labels;
                Console.WriteLine($"{labels["city"].Value} | Age {labels["age"].Value}");
            }
        }

        // -------------------------------------------------
        // AND Query
        // -------------------------------------------------

        ConsoleHelper.Section("AND: city=Berlin AND age=32");

        var andQuery = SearchQuery
            .For(EntityName)
            .And(
                new EqClause("city", "Berlin"),
                new EqClause("age", "32"));

        await using (var reader =
            await (await scan.SearchAsync(andQuery, opts)).AsEntities<Customer>())
        {
            while (await reader.ReadAsync())
                Console.WriteLine(reader.Current.Name);
        }

        // -------------------------------------------------
        // Prefix Search
        // -------------------------------------------------

        ConsoleHelper.Section("LikePrefix: email starts with user1");

        var prefixQuery = SearchQuery
            .For(EntityName)
            .LikePrefix("email", "user1");

        await using (var reader =
            await (await scan.SearchAsync(prefixQuery, opts)).AsKeys())
        {
            while (await reader.ReadAsync())
                Console.WriteLine(reader.Current);
        }

        // -------------------------------------------------
        // Sorting + Limit
        // -------------------------------------------------

        ConsoleHelper.Section("OrderBy age DESC (Top 5)");

        var sortedQuery = SearchQuery
            .For(EntityName)
            .OrderBy("age", ascending: false)
            .Limit(5);

        await using (var reader =
            await (await scan.SearchAsync(sortedQuery, opts)).AsEntries())
        {
            while (await reader.ReadAsync())
            {
                var labels = reader.Current.Metadata.Labels;
                Console.WriteLine($"{labels["city"].Value} | Age {labels["age"].Value}");
            }
        }

        // -------------------------------------------------
        // Projection
        // -------------------------------------------------

        ConsoleHelper.Section("Projection: select only email");

        var selectQuery = SearchQuery
            .For(EntityName)
            .Eq("city", "New York")
            .Select("email");

        await using (var reader =
            await (await scan.SearchAsync(selectQuery, opts))
                .Select(new[] { "email" }))
        {
            while (await reader.ReadAsync())
                Console.WriteLine(reader.Current["email"].Value);
        }

        // -------------------------------------------------
        // Cleanup
        // -------------------------------------------------

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
        ConsoleHelper.Success("\nSearch sample completed.");
    }
}