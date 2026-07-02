using XsAndOs.Core;

namespace XsAndOs.Cli;

/// <summary>Batch simulation + stats tables: the tuning workhorse.</summary>
internal static class BatchStats
{
    public static PlayResult[] RunBatch(PlayDesign play, DefensiveCall defense, int sims, int seedStart)
    {
        var results = new PlayResult[sims];
        Parallel.For(0, sims, i =>
        {
            results[i] = Sim.Run(play, defense, seedStart + i).Result;
        });
        return results;
    }

    public static void PrintReport(PlayResult[] results)
    {
        PrintOutcomeTable(results);
        Console.WriteLine();
        PrintYardageStats(results);
        Console.WriteLine();
        PrintHistogram(results);
    }

    private static void PrintOutcomeTable(PlayResult[] results)
    {
        var counts = new Dictionary<PlayOutcome, int>();
        foreach (var r in results)
        {
            counts[r.Outcome] = counts.GetValueOrDefault(r.Outcome) + 1;
        }

        Console.WriteLine("  Outcome            Count      %");
        Console.WriteLine("  ---------------- ------- ------");
        foreach (var (outcome, count) in counts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {outcome,-16} {count,7} {100.0 * count / results.Length,5:0.0}%");
        }
    }

    private static void PrintYardageStats(PlayResult[] results)
    {
        var yards = results.Select(r => r.YardsGained).OrderBy(y => y).ToArray();
        float Pct(double p) => yards[(int)Math.Min(yards.Length - 1, p * yards.Length)];
        Console.WriteLine($"  Yards: mean {yards.Average():0.0}  median {Pct(0.5):0.0}  " +
                          $"p10 {Pct(0.1):0.0}  p90 {Pct(0.9):0.0}  min {yards[0]:0.0}  max {yards[^1]:0.0}");
        Console.WriteLine($"  Avg duration: {results.Average(r => r.Duration):0.00}s");
    }

    private static void PrintHistogram(PlayResult[] results)
    {
        const int bucketSize = 5;
        var buckets = new SortedDictionary<int, int>();
        foreach (var r in results)
        {
            var b = (int)Math.Floor(r.YardsGained / bucketSize) * bucketSize;
            buckets[b] = buckets.GetValueOrDefault(b) + 1;
        }

        var max = buckets.Values.Max();
        foreach (var (bucket, count) in buckets)
        {
            var bar = new string('#', Math.Max(1, count * 40 / max));
            Console.WriteLine($"  {bucket,4}..{bucket + bucketSize,-4} {count,5} {bar}");
        }
    }

    public static void PrintMatrix(IReadOnlyList<PlayDesign> plays, int sims)
    {
        Console.WriteLine($"  {"Play",-14} {"Defense",-8} {"Comp%",6} {"Sack%",6} {"Int%",6} {"TD%",5} {"AvgYd",6}");
        Console.WriteLine("  " + new string('-', 58));
        foreach (var play in plays)
        {
            foreach (var call in DefensiveCall.All)
            {
                var results = RunBatch(play, call, sims, seedStart: 1);
                var n = (float)results.Length;
                var comp = results.Count(r => r.Outcome is PlayOutcome.CompletedPass or PlayOutcome.Touchdown
                    || (r.Outcome == PlayOutcome.OutOfBounds && play.Kind == PlayKind.Pass)) / n;
                var sack = results.Count(r => r.Outcome == PlayOutcome.Sack) / n;
                var pick = results.Count(r => r.Outcome == PlayOutcome.Interception) / n;
                var td = results.Count(r => r.Outcome == PlayOutcome.Touchdown) / n;
                var avg = results.Average(r => r.YardsGained);
                var compText = play.Kind == PlayKind.Pass ? $"{100 * comp,5:0.0}%" : "     -";
                Console.WriteLine($"  {play.Name,-14} {call,-8} {compText} {100 * sack,5:0.0}% {100 * pick,5:0.0}% {100 * td,4:0.0}% {avg,6:0.0}");
            }
        }
    }
}
