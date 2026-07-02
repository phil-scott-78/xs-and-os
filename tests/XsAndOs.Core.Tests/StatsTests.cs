using XsAndOs.Core;

namespace XsAndOs.Core.Tests;

/// <summary>
/// Statistical sanity pins with deliberately wide bands (fixed seed ranges, so they
/// are reproducible). When one fails after a gameplay change, re-tune via
/// `xso batch` / `xso matrix` and adjust Tuning constants — the bands encode
/// "this still resembles football and attributes still matter".
/// </summary>
public class StatsTests
{
    private const int Sims = 300;

    [Fact]
    public void SlantVsCover3_IsAHealthyQuickGame()
    {
        var results = Batch(SamplePlays.SlantFlat, CoverageShell.Cover3);
        var completion = CompletionRate(results);
        Assert.InRange(completion, 0.40, 0.85);
        Assert.InRange(results.Average(r => r.YardsGained), 3.0, 18.0);
    }

    [Fact]
    public void FourVerts_IsBoomOrBust()
    {
        var vsMan = Batch(SamplePlays.FourVerts, CoverageShell.Man);

        // Deep shots vs tight man are low-percentage, the pass rush is a real threat
        // when nobody uncovers, and interceptions exist but stay rare.
        Assert.InRange(CompletionRate(vsMan), 0.02, 0.55);
        Assert.InRange(Rate(vsMan, PlayOutcome.Sack), 0.02, 0.5);
        Assert.True(Rate(vsMan, PlayOutcome.Interception) < 0.15);

        // The payoff: verts hit far bigger than the quick game against soft shells.
        var vertsVsC2 = Batch(SamplePlays.FourVerts, CoverageShell.Cover2);
        var slantVsC2 = Batch(SamplePlays.SlantFlat, CoverageShell.Cover2);
        Assert.True(vertsVsC2.Average(r => r.YardsGained) > slantVsC2.Average(r => r.YardsGained) + 5.0,
            "four verts should out-gain the quick game against Cover 2");
    }

    [Fact]
    public void InsideZone_LooksLikeARunningGame()
    {
        var results = Batch(SamplePlays.InsideZone, CoverageShell.Man);
        var yards = results.Select(r => r.YardsGained).OrderBy(y => y).ToArray();
        Assert.InRange(yards.Average(), 0.0, 9.0);
        Assert.True(yards[(int)(yards.Length * 0.9)] < 25.0, "p90 of an inside run should not be a breakaway");
        Assert.Contains(yards, y => y >= 10.0);
    }

    [Fact]
    public void HbToss_IsAViableOutsideRun()
    {
        var vsMan = Batch(SamplePlays.HbToss, CoverageShell.Man);
        var yards = vsMan.Select(r => r.YardsGained).OrderBy(y => y).ToArray();

        Assert.InRange(yards.Average(), 1.5, 10.0);
        Assert.True(yards[(int)(yards.Length * 0.9)] >= 4.0, "a healthy toss should have chunk-gain upside");
        Assert.Contains(yards, y => y < 0.0);

        // Sweeping into a zone's overhang defenders is worse, but not a guaranteed loss.
        var vsCover3 = Batch(SamplePlays.HbToss, CoverageShell.Cover3);
        Assert.InRange(vsCover3.Average(r => r.YardsGained), -1.0, 8.0);
    }

    [Fact]
    public void FastReceiverBeatsSlowCorner_AndViceVersa()
    {
        var offense = SampleRosters.CreateOffense();
        var defense = SampleRosters.CreateDefense();

        var fastWr = WithAttributes(offense, "WR1", a => a with { Speed = 95, Acceleration = 95 });
        var slowCb = WithAttributes(defense, "CB1", a => a with { Speed = 60, Acceleration = 60 });

        var slowWr = WithAttributes(offense, "WR1", a => a with { Speed = 60, Acceleration = 60 });
        var fastCb = WithAttributes(defense, "CB1", a => a with { Speed = 95, Acceleration = 95 });

        var mismatch = Batch(SamplePlays.FourVerts, CoverageShell.Man, fastWr, slowCb);
        var blanket = Batch(SamplePlays.FourVerts, CoverageShell.Man, slowWr, fastCb);

        Assert.True(mismatch.Average(r => r.YardsGained) > blanket.Average(r => r.YardsGained),
            "a burner against a slow corner must out-produce the inverted matchup");
    }

    // Max protect with only the two outside WRs releasing: against man both are
    // blanketed by corners, no mismatch exists, and the QB's internal clock is
    // the whole ballgame.
    private static readonly PlayDesign DoubleGoMaxProtect = new(
        Name: "test-double-go",
        FormationName: "Shotgun",
        Kind: PlayKind.Pass,
        Routes:
        [
            new RouteAssignment("WR1", [new Vec2(0f, 28f)]),
            new RouteAssignment("WR2", [new Vec2(0f, 28f)]),
        ],
        Blocking:
        [
            new BlockingAssignment("LT", BlockType.PassProtect),
            new BlockingAssignment("LG", BlockType.PassProtect),
            new BlockingAssignment("C", BlockType.PassProtect),
            new BlockingAssignment("RG", BlockType.PassProtect),
            new BlockingAssignment("RT", BlockType.PassProtect),
            new BlockingAssignment("TE1", BlockType.PassProtect),
            new BlockingAssignment("RB1", BlockType.PassProtect),
            new BlockingAssignment("WR3", BlockType.PassProtect),
        ],
        DropbackDepth: 2f);

    [Fact]
    public void SmartQbTakesFewerSacks()
    {
        var doubleGo = DoubleGoMaxProtect;

        var offense = SampleRosters.CreateOffense();
        var defense = SampleRosters.CreateDefense();

        var smart = WithAttributes(offense, "QB1", a => a with { Awareness = 90 });
        var panicky = WithAttributes(offense, "QB1", a => a with { Awareness = 30 });

        var smartResults = Batch(doubleGo, CoverageShell.Man, smart, defense);
        var panickyResults = Batch(doubleGo, CoverageShell.Man, panicky, defense);

        var smartSacks = Rate(smartResults, PlayOutcome.Sack);
        var panickySacks = Rate(panickyResults, PlayOutcome.Sack);
        Assert.True(panickySacks >= smartSacks,
            $"low-awareness QB should not take fewer sacks (smart {smartSacks:P1}, panicky {panickySacks:P1})");
        Assert.True(panickySacks > 0.0, "a low-awareness QB with nobody open must take some sacks");
    }

    [Fact]
    public void AthleticQbEscapesAndThrowsAway_StatueQbEatsIt()
    {
        var offense = SampleRosters.CreateOffense();
        var defense = SampleRosters.CreateDefense();

        var athletic = WithAttributes(offense, "QB1",
            a => a with { Agility = 90, Speed = 85, Awareness = 85 });
        var statue = WithAttributes(offense, "QB1",
            a => a with { Agility = 25, Speed = 25, Awareness = 30 });

        // Full SimResults so we can count Scramble/ThrowAway events.
        var athleticSims = new SimResult[150];
        var statueSims = new SimResult[150];
        Parallel.For(0, 150, i =>
        {
            athleticSims[i] = Sim.Run(DoubleGoMaxProtect, new DefensiveCall(CoverageShell.Man),
                athletic, defense, seed: i + 1);
            statueSims[i] = Sim.Run(DoubleGoMaxProtect, new DefensiveCall(CoverageShell.Man),
                statue, defense, seed: i + 1);
        });

        var athleticSackRate = athleticSims.Count(s => s.Result.Outcome == PlayOutcome.Sack) / 150.0;
        var statueSackRate = statueSims.Count(s => s.Result.Outcome == PlayOutcome.Sack) / 150.0;
        var throwaways = athleticSims.Count(s => s.Events.Any(e => e.Type == PlayEventType.ThrowAway));
        var scrambles = athleticSims.Count(s => s.Events.Any(e => e.Type == PlayEventType.Scramble));

        Assert.True(athleticSackRate < statueSackRate,
            $"athletic QB must take fewer sacks (athletic {athleticSackRate:P1}, statue {statueSackRate:P1})");
        Assert.True(scrambles > 0, "an athletic QB under pressure must scramble sometimes");
        Assert.True(throwaways > 0, "an athletic QB outside the pocket must throw some away");
        Assert.True(statueSackRate > 0.0, "a statue QB with nobody open still eats sacks");
    }

    private static PlayResult[] Batch(PlayDesign play, CoverageShell shell,
        Team? offense = null, Team? defense = null)
    {
        offense ??= SampleRosters.CreateOffense();
        defense ??= SampleRosters.CreateDefense();
        var results = new PlayResult[Sims];
        Parallel.For(0, Sims, i =>
        {
            results[i] = Sim.Run(play, new DefensiveCall(shell), offense, defense, seed: i + 1).Result;
        });
        return results;
    }

    private static double CompletionRate(PlayResult[] results) =>
        results.Count(r => r.Outcome is PlayOutcome.CompletedPass or PlayOutcome.Touchdown
            or PlayOutcome.OutOfBounds) / (double)results.Length;

    private static double Rate(PlayResult[] results, PlayOutcome outcome) =>
        results.Count(r => r.Outcome == outcome) / (double)results.Length;

    private static Team WithAttributes(Team team, string playerId, Func<PlayerAttributes, PlayerAttributes> change)
    {
        var players = team.Players
            .Select(p => p.Id == playerId ? p with { Attributes = change(p.Attributes) } : p)
            .ToArray();
        return team with { Players = players };
    }
}
