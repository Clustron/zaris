using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clustron.Zaris.Samples.Shared
{
    /// <summary>
    /// Wraps a sample's body so the process reports an unambiguous, machine-parseable
    /// result that the sanity harness can grade. On clean completion it prints
    /// <c>##SAMPLE:PASS &lt;name&gt; (&lt;mode&gt;)##</c> and returns exit code 0; on any
    /// unhandled exception (e.g. a remote connection that never establishes, or an
    /// engine error) it prints <c>##SAMPLE:FAIL &lt;name&gt; (&lt;mode&gt;): ...##</c> and
    /// returns exit code 1. A watchdog also fails the run if the body exceeds
    /// <c>ZARIS_SAMPLE_TIMEOUT_SECONDS</c> (default 180) so a hung sample can never wedge
    /// the harness.
    ///
    /// The mode string is read from the <c>ZARIS_SAMPLE_MODE</c> environment variable
    /// (Inproc/Remote) purely for reporting; it does not change behaviour.
    /// </summary>
    public static class SampleRunner
    {
        public static async Task<int> RunAsync(string name, Func<Task> body)
        {
            var mode = Environment.GetEnvironmentVariable("ZARIS_SAMPLE_MODE") ?? "default";

            var timeoutSeconds = 180;
            if (int.TryParse(Environment.GetEnvironmentVariable("ZARIS_SAMPLE_TIMEOUT_SECONDS"), out var t) && t > 0)
                timeoutSeconds = t;

            try
            {
                var work = body();
                var completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)))
                    .ConfigureAwait(false);

                if (completed != work)
                    throw new TimeoutException($"Sample did not complete within {timeoutSeconds}s.");

                await work.ConfigureAwait(false); // surface any exception from the body

                Console.WriteLine($"##SAMPLE:PASS {name} ({mode})##");
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"##SAMPLE:FAIL {name} ({mode}): {ex.GetType().Name}: {ex.Message}##");
                Console.ResetColor();
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
