using XsAndOs.Core;

namespace XsAndOs.Core.Tests;

public class DeterminismTests
{
    public static TheoryData<string, CoverageShell> AllMatchups()
    {
        var data = new TheoryData<string, CoverageShell>();
        foreach (var play in SamplePlays.All)
        {
            foreach (var call in DefensiveCall.All)
            {
                data.Add(play.Name, call.Shell);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllMatchups))]
    public void SameSeed_ProducesIdenticalOutput(string playName, CoverageShell shell)
    {
        var play = SamplePlays.ByName(playName);
        var call = new DefensiveCall(shell);

        var a = Sim.Run(play, call, seed: 1234);
        var b = Sim.Run(play, call, seed: 1234);

        Assert.Equal(Hash(a), Hash(b));
        Assert.Equal(a.Result.Outcome, b.Result.Outcome);
        Assert.Equal(a.Result.YardsGained, b.Result.YardsGained);
        Assert.Equal(a.Events.Count, b.Events.Count);
        for (var i = 0; i < a.Events.Count; i++)
        {
            Assert.Equal(a.Events[i].Type, b.Events[i].Type);
            Assert.Equal(a.Events[i].Tick, b.Events[i].Tick);
            Assert.Equal(a.Events[i].ActorIndex, b.Events[i].ActorIndex);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentOutput()
    {
        var play = SamplePlays.SlantFlat;
        var call = new DefensiveCall(CoverageShell.Cover3);

        // At least one pair among a handful of seeds must differ; identical output
        // across all of them would mean the RNG is being ignored.
        var hashes = new HashSet<long>();
        for (var seed = 1; seed <= 5; seed++)
        {
            hashes.Add(Hash(Sim.Run(play, call, seed)));
        }

        Assert.True(hashes.Count > 1, "five different seeds produced bit-identical simulations");
    }

    /// <summary>Rolling hash over every frame's positions and velocities.</summary>
    private static long Hash(SimResult sim)
    {
        long h = 17;
        foreach (var frame in sim.Frames)
        {
            foreach (var p in frame.Players)
            {
                h = unchecked(h * 31 + p.Pos.X.GetHashCode());
                h = unchecked(h * 31 + p.Pos.Y.GetHashCode());
                h = unchecked(h * 31 + p.Vel.X.GetHashCode());
                h = unchecked(h * 31 + p.Vel.Y.GetHashCode());
            }

            h = unchecked(h * 31 + frame.Ball.Pos.X.GetHashCode());
            h = unchecked(h * 31 + frame.Ball.Pos.Y.GetHashCode());
        }

        return h;
    }
}
