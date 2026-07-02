using XsAndOs.Core;
using XsAndOs.Playbook;

namespace XsAndOs.Core.Tests;

public class PlayDraftTests
{
    private static Vec2 Snap => new(Field.CenterX, 50f);

    [Fact]
    public void Reset_AppliesPositionDefaults()
    {
        var draft = new PlayDraft();
        draft.Reset("Shotgun", PlayKind.Pass);

        Assert.Equal(BlockType.PassProtect, draft.Blocks["LT"]);
        Assert.Equal(BlockType.PassProtect, draft.Blocks["TE1"]);
        Assert.Null(draft.Blocks["WR1"]);
        Assert.Null(draft.Blocks["RB1"]);
        Assert.False(draft.Blocks.ContainsKey("QB1"));
        Assert.Equal(0.5f, draft.DropbackDepth); // gun

        draft.Reset("Ace", PlayKind.Run);
        Assert.Equal(BlockType.RunBlockZoneRight, draft.Blocks["LG"]);
        Assert.Equal(0.7f, draft.HandoffTime); // under center
    }

    [Fact]
    public void CycleBlock_WrapsThroughTheRightSets()
    {
        var draft = new PlayDraft();
        draft.Reset("Shotgun", PlayKind.Pass);

        // OL: PP -> ZL -> ZR -> PL -> PR -> LD -> PP.
        Assert.Equal(BlockType.RunBlockZoneLeft, draft.CycleBlock("LT"));
        Assert.Equal(BlockType.RunBlockZoneRight, draft.CycleBlock("LT"));
        Assert.Equal(BlockType.PullLeft, draft.CycleBlock("LT"));
        Assert.Equal(BlockType.PullRight, draft.CycleBlock("LT"));
        Assert.Equal(BlockType.LeadBlock, draft.CycleBlock("LT"));
        Assert.Equal(BlockType.PassProtect, draft.CycleBlock("LT"));

        // Skill players start unassigned and can return to unassigned.
        Assert.Equal(BlockType.PassProtect, draft.CycleBlock("RB1"));
        Assert.Equal(BlockType.RunBlockZoneLeft, draft.CycleBlock("RB1"));
        Assert.Equal(BlockType.RunBlockZoneRight, draft.CycleBlock("RB1"));
        Assert.Equal(BlockType.LeadBlock, draft.CycleBlock("RB1"));
        Assert.Null(draft.CycleBlock("RB1"));

        // QB and routed players don't cycle.
        draft.SetRoute("WR1", [Snap + new Vec2(-21f, 5f)]);
        Assert.Null(draft.CycleBlock("WR1"));
    }

    [Fact]
    public void RunMode_FirstDrawnPathCarriesTheBall()
    {
        var draft = new PlayDraft();
        draft.Reset("Ace", PlayKind.Run);

        draft.SetRoute("RB1", [Snap + new Vec2(2f, 8f)]);
        draft.SetRoute("WR1", [Snap + new Vec2(-21f, 10f)]);
        Assert.Equal("RB1", draft.BallCarrierSlotId);

        // Clearing the carrier hands the ball to the next-drawn path.
        draft.ClearRoute("RB1");
        Assert.Equal("WR1", draft.BallCarrierSlotId);

        draft.ClearRoute("WR1");
        Assert.Null(draft.BallCarrierSlotId);
        Assert.NotNull(draft.Validate());
    }

    [Fact]
    public void Compile_ProducesRelativeWaypoints_InDrawOrder()
    {
        var draft = new PlayDraft();
        draft.Reset("Shotgun", PlayKind.Pass);

        var wr1Start = draft.SlotWorldPos("WR1", 50f);
        var rb1Start = draft.SlotWorldPos("RB1", 50f);
        draft.SetRoute("WR1", [wr1Start + new Vec2(0f, 3f), wr1Start + new Vec2(9f, 9f)]);
        draft.SetRoute("RB1", [rb1Start + new Vec2(-5f, 3f)]);

        var play = draft.Compile(50f);

        Assert.Equal(PlayKind.Pass, play.Kind);
        Assert.Equal(["WR1", "RB1"], play.Routes.Select(r => r.SlotId));
        Assert.Equal(new Vec2(0f, 3f), play.Routes[0].Waypoints[0]);
        Assert.Equal(new Vec2(9f, 9f), play.Routes[0].Waypoints[1]);
        Assert.Equal(new Vec2(-5f, 3f), play.Routes[1].Waypoints[0]);
        Assert.Contains(play.Blocking, b => b.SlotId == "LT" && b.Type == BlockType.PassProtect);
        Assert.DoesNotContain(play.Blocking, b => b.SlotId == "WR1");
    }

    [Fact]
    public void CompiledDraft_SimulatesEndToEnd()
    {
        var draft = new PlayDraft();
        draft.Reset("Shotgun", PlayKind.Pass);
        var wr1 = draft.SlotWorldPos("WR1", 50f);
        var wr2 = draft.SlotWorldPos("WR2", 50f);
        draft.SetRoute("WR1", [wr1 + new Vec2(0f, 3f), wr1 + new Vec2(8f, 8f)]);
        draft.SetRoute("WR2", [wr2 + new Vec2(0f, 12f)]);
        Assert.Null(draft.Validate());

        var sim = Sim.Run(draft.Compile(50f), new DefensiveCall(CoverageShell.Cover3), seed: 42);
        Assert.NotEmpty(sim.Frames);
        Assert.Contains(sim.Events, e => e.Type == PlayEventType.PlayDead);

        // Run play: lane compiles to RunLane, not Routes.
        var runDraft = new PlayDraft();
        runDraft.Reset("Ace", PlayKind.Run);
        var rb = runDraft.SlotWorldPos("RB1", 50f);
        runDraft.SetRoute("RB1", [rb + new Vec2(1.5f, 8f), rb + new Vec2(2f, 20f)]);
        Assert.Null(runDraft.Validate());

        var runPlay = runDraft.Compile(50f);
        Assert.Equal("RB1", runPlay.BallCarrierSlotId);
        Assert.NotNull(runPlay.RunLane);
        Assert.Empty(runPlay.Routes);

        var runSim = Sim.Run(runPlay, new DefensiveCall(CoverageShell.Man), seed: 7);
        Assert.Contains(runSim.Events, e => e.Type == PlayEventType.Handoff);
    }

    [Fact]
    public void Validate_RequiresARouteOrALane()
    {
        var draft = new PlayDraft();
        draft.Reset("Shotgun", PlayKind.Pass);
        Assert.NotNull(draft.Validate());

        draft.Reset("Ace", PlayKind.Run);
        Assert.NotNull(draft.Validate());
    }
}
