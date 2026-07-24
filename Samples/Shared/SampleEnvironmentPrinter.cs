using System;
using System.Linq;
using Clustron.Dictus.Client;
using Clustron.Dictus.Client.Helpers;

namespace Clustron.Dictus.Samples.Shared;

public static class SampleEnvironmentPrinter
{
    public static void Print(DictusOptions options, DictusClientMode mode)
    {
        Console.WriteLine();

        Console.WriteLine($"Cluster : {options.ClusterId}");

        if (mode == DictusClientMode.InProc)
        {
            Console.WriteLine("Mode    : InProc (embedded store)");
            Console.WriteLine("Hint    : Set \"Mode\": \"Remote\" and configure Seeds to connect to a cluster");
        }
        else
        {
            var seeds = options.Seeds == null
                ? ""
                : string.Join(",", options.Seeds.Select(s => $"{s.Host}:{s.ClientPort}"));

            Console.WriteLine($"Mode    : Remote ({seeds})");
            Console.WriteLine("Hint    : Set \"Mode\": \"InProc\" in appsettings.json to run without servers");
        }

        Console.WriteLine();
    }
}