using XsAndOs.Core;

namespace XsAndOs.Core.Tests;

public class InvariantTests
{
    private static readonly PlayEventType[] TerminalTypes =
    [
        PlayEventType.Incomplete, PlayEventType.Interception, PlayEventType.Tackle,
        PlayEventType.Sack, PlayEventType.OutOfBounds, PlayEventType.Touchdown,
    ];

    public static TheoryData<string, CoverageShell, int> Matchups()
    {
        var data = new TheoryData<string, CoverageShell, int>();
        foreach (var play in SamplePlays.All)
        {
            foreach (var call in DefensiveCall.All)
            {
                for (var seed = 1; seed <= 30; seed++)
                {
                    data.Add(play.Name, call.Shell, seed);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matchups))]
    public void SimulationInvariantsHold(string playName, CoverageShell shell, int seed)
    {
        var sim = Sim.Run(SamplePlays.ByName(playName), new DefensiveCall(shell), seed);

        // Terminates well before the hard cap, with frames present.
        Assert.NotEmpty(sim.Frames);
        Assert.True(sim.Result.Duration < Tuning.MaxPlaySeconds,
            $"play ran into the {Tuning.MaxPlaySeconds}s safety cap");

        // Exactly one PlayDead, preceded by at least one terminal event.
        Assert.Equal(1, sim.Events.Count(e => e.Type == PlayEventType.PlayDead));
        Assert.True(sim.Events.Any(e => TerminalTypes.Contains(e.Type)),
            "no terminal event before PlayDead");

        // Every position stays on the field (carrier may straddle the sideline briefly).
        foreach (var frame in sim.Frames)
        {
            Assert.Equal(22, frame.Players.Length);
            foreach (var p in frame.Players)
            {
                Assert.True(Field.Contains(p.Pos, margin: 1.5f),
                    $"player off the field at tick {frame.Tick}: {p.Pos}");
            }
        }

        // Time is strictly monotonic and the carrier index is always valid.
        for (var i = 1; i < sim.Frames.Count; i++)
        {
            Assert.True(sim.Frames[i].Time > sim.Frames[i - 1].Time);
            var carrier = sim.Frames[i].Ball.CarrierIndex;
            Assert.InRange(carrier, -1, 21);
        }

        // Yardage math: dead spot minus LOS, capped at the goal line.
        if (sim.Result.Outcome is not (PlayOutcome.IncompletePass or PlayOutcome.Interception))
        {
            Assert.InRange(sim.Result.YardsGained, -sim.LosY, Field.TargetGoalLineY - sim.LosY);
        }
        else
        {
            Assert.Equal(0f, sim.Result.YardsGained);
        }

        if (sim.Result.Outcome == PlayOutcome.Touchdown)
        {
            Assert.Equal(Field.TargetGoalLineY - sim.LosY, sim.Result.YardsGained, precision: 2);
        }
    }

    [Fact]
    public void CarrierChangesOnlyAtHandoffOrCatch()
    {
        foreach (var play in SamplePlays.All)
        {
            var sim = Sim.Run(play, new DefensiveCall(CoverageShell.Man), seed: 77);
            var transferTicks = sim.Events
                .Where(e => e.Type is PlayEventType.Handoff or PlayEventType.Catch
                    or PlayEventType.ThrowStart or PlayEventType.Interception
                    or PlayEventType.Incomplete)
                .Select(e => e.Tick)
                .ToHashSet();

            for (var i = 1; i < sim.Frames.Count; i++)
            {
                var prev = sim.Frames[i - 1].Ball.CarrierIndex;
                var cur = sim.Frames[i].Ball.CarrierIndex;
                if (prev != cur)
                {
                    Assert.True(transferTicks.Contains(sim.Frames[i].Tick),
                        $"{play.Name}: carrier changed {prev} -> {cur} at tick {sim.Frames[i].Tick} without a transfer event");
                }
            }
        }
    }

    [Fact]
    public void InvalidLos_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Sim.Run(SamplePlays.SlantFlat, new DefensiveCall(CoverageShell.Man), seed: 1, losY: 5f));
    }
}
