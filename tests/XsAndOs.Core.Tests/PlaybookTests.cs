using XsAndOs.Core;
using XsAndOs.Playbook;

namespace XsAndOs.Core.Tests;

public class PlaybookTests
{
    [Fact]
    public void PlayDesign_RoundTripsThroughJson()
    {
        foreach (var play in SamplePlays.All)
        {
            var json = PlaybookSerializer.Serialize(play);
            var restored = PlaybookSerializer.Deserialize(json);

            Assert.Equal(play.Name, restored.Name);
            Assert.Equal(play.FormationName, restored.FormationName);
            Assert.Equal(play.Kind, restored.Kind);
            Assert.Equal(play.BallCarrierSlotId, restored.BallCarrierSlotId);
            Assert.Equal(play.DropbackDepth, restored.DropbackDepth);
            Assert.Equal(play.HandoffTime, restored.HandoffTime);

            Assert.Equal(play.Routes.Count, restored.Routes.Count);
            for (var i = 0; i < play.Routes.Count; i++)
            {
                Assert.Equal(play.Routes[i].SlotId, restored.Routes[i].SlotId);
                Assert.Equal(play.Routes[i].Waypoints, restored.Routes[i].Waypoints);
            }

            Assert.Equal(
                play.Blocking.Select(b => (b.SlotId, b.Type)),
                restored.Blocking.Select(b => (b.SlotId, b.Type)));
            Assert.Equal(play.RunLane ?? [], restored.RunLane ?? []);
        }
    }

    [Fact]
    public void SamplePlayFiles_LoadAndSimulate()
    {
        var dir = FindSamplesDir();
        var plays = PlaybookSerializer.LoadDirectory(dir);
        Assert.Equal(SamplePlays.All.Count, plays.Count);

        foreach (var play in plays)
        {
            var sim = Sim.Run(play, new DefensiveCall(CoverageShell.Cover3), seed: 42);
            Assert.NotEmpty(sim.Frames);
            Assert.Contains(sim.Events, e => e.Type == PlayEventType.PlayDead);
        }
    }

    /// <summary>Walk up from the test bin directory to the repo's samples/plays.</summary>
    private static string FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "plays");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("samples/plays not found above the test bin directory");
    }
}
