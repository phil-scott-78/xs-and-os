using XsAndOs.Core;

namespace XsAndOs.Cli;

/// <summary>Turns the semantic event stream into play-by-play text.</summary>
internal static class Narrator
{
    public static void Print(SimResult sim, bool verbose)
    {
        foreach (var e in sim.Events)
        {
            var line = Describe(sim, e, verbose);
            if (line != null)
            {
                Console.WriteLine($"  {e.Time,5:0.00}s  {line}");
            }
        }

        var r = sim.Result;
        Console.WriteLine($"  RESULT: {r.Outcome} for {r.YardsGained:+0.0;-0.0;0.0} yards ({r.Duration:0.00}s)");
    }

    private static string? Describe(SimResult sim, PlayEvent e, bool verbose)
    {
        string Actor() => Name(sim, e.ActorIndex);
        string Target() => Name(sim, e.TargetIndex);
        var yards = e.Spot.Y - sim.LosY;

        return e.Type switch
        {
            PlayEventType.Snap => "SNAP",
            PlayEventType.Handoff => $"HANDOFF to {Target()}",
            PlayEventType.ProgressionRead when verbose => $"read -> {Target()}",
            PlayEventType.ProgressionRead => null,
            PlayEventType.ThrowStart => $"THROW to {Target()} (aimed {yards:+0.0;-0.0;0.0} yd)",
            PlayEventType.Catch => $"CATCH by {Actor()} at {yards:+0.0;-0.0;0.0}",
            PlayEventType.Incomplete => $"INCOMPLETE intended for {Actor()}",
            PlayEventType.Interception => $"INTERCEPTED by {Actor()}!",
            PlayEventType.BlockShed when verbose => $"{Actor()} sheds {Target()}'s block",
            PlayEventType.BlockShed => null,
            PlayEventType.BrokenTackle => $"{Actor()} breaks {Target()}'s tackle!",
            PlayEventType.Tackle => $"TACKLE by {Actor()} at {yards:+0.0;-0.0;0.0}",
            PlayEventType.Sack => $"SACK by {Actor()} at {yards:+0.0;-0.0;0.0}",
            PlayEventType.OutOfBounds => $"{Actor()} out of bounds at {yards:+0.0;-0.0;0.0}",
            PlayEventType.Touchdown => $"TOUCHDOWN {Actor()}!",
            PlayEventType.PlayDead => null,
            _ => null,
        };
    }

    private static string Name(SimResult sim, int index)
    {
        if (index < 0 || index >= sim.Participants.Count)
        {
            return "?";
        }

        var p = sim.Participants[index];
        return $"{p.Name} ({p.Position})";
    }
}
