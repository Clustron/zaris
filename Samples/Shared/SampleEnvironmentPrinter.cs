using System;
using System.Linq;
using Clustron.DKV.Client;
using Clustron.DKV.Client.Helpers;

namespace Clustron.Dkv.Samples.Shared;

public static class SampleEnvironmentPrinter
{
    public static void Print(DkvOptions options, DkvClientMode mode)
    {
        Console.WriteLine();

        Console.WriteLine($"Cluster : {options.ClusterId}");

        if (mode == DkvClientMode.InProc)
        {
            Console.WriteLine("Mode    : InProc (embedded store)");
            Console.WriteLine("Hint    : Set \"Mode\": \"Remote\" and configure Seeds to connect to a cluster");
        }
        else
        {
            var seeds = options.Seeds == null
                ? ""
                : string.Join(",", options.Seeds.Select(s => $"{s.Host}:{s.Port}"));

            Console.WriteLine($"Mode    : Remote ({seeds})");
            Console.WriteLine("Hint    : Set \"Mode\": \"InProc\" in appsettings.json to run without servers");
        }

        Console.WriteLine();
    }
}