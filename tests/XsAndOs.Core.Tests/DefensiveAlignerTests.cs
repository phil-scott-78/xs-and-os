using XsAndOs.Core;

namespace XsAndOs.Core.Tests;

public class DefensiveAlignerTests
{
    public static TheoryData<string, CoverageShell> AllMatchups()
    {
        var data = new TheoryData<string, CoverageShell>();
        foreach (var formation in Formations.All)
        {
            foreach (var call in DefensiveCall.All)
            {
                data.Add(formation.Name, call.Shell);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllMatchups))]
    public void ElevenAssignments_AllOnRoster(string formationName, CoverageShell shell)
    {
        var formation = Formations.ByName(formationName);
        var assignments = DefensiveAligner.Align(new DefensiveCall(shell), formation);

        Assert.Equal(11, assignments.Count);
        Assert.Equal(11, assignments.Select(a => a.DefenderId).Distinct().Count());

        var roster = SampleRosters.CreateDefense().Players.Select(p => p.Id).ToHashSet();
        foreach (var a in assignments)
        {
            Assert.Contains(a.DefenderId, roster);
        }
    }

    [Theory]
    [MemberData(nameof(AllMatchups))]
    public void NickelSwapsSlotCornerForLb3_When3Wrs(string formationName, CoverageShell shell)
    {
        var formation = Formations.ByName(formationName);
        var wrCount = formation.Slots.Count(s => s.Position == PlayerPosition.WR);
        var ids = DefensiveAligner.Align(new DefensiveCall(shell), formation)
            .Select(a => a.DefenderId)
            .ToHashSet();

        if (wrCount >= 3)
        {
            Assert.Contains("CB3", ids);
            Assert.DoesNotContain("LB3", ids);
        }
        else
        {
            Assert.Contains("LB3", ids);
            Assert.DoesNotContain("CB3", ids);
        }
    }

    [Fact]
    public void InMan_NickelCornerCoversAWr_NotTheWidest()
    {
        foreach (var formation in Formations.All)
        {
            var assignments = DefensiveAligner.Align(new DefensiveCall(CoverageShell.Man), formation);
            var cb3 = assignments.Single(a => a.DefenderId == "CB3");

            Assert.Equal(DefensiveRole.ManCover, cb3.Role);
            var target = formation.Slots.Single(s => s.SlotId == cb3.TargetSlotId);
            Assert.Equal(PlayerPosition.WR, target.Position);

            // The outermost eligibles on each side belong to the boundary corners.
            var eligibles = formation.Slots
                .Where(s => s.Position is PlayerPosition.WR or PlayerPosition.TE or PlayerPosition.RB)
                .OrderBy(s => s.Offset.X)
                .ToList();
            Assert.NotEqual(eligibles[0].SlotId, cb3.TargetSlotId);
            Assert.NotEqual(eligibles[^1].SlotId, cb3.TargetSlotId);
        }
    }

    [Fact]
    public void AllEligiblesCovered_InMan()
    {
        foreach (var formation in Formations.All)
        {
            var assignments = DefensiveAligner.Align(new DefensiveCall(CoverageShell.Man), formation);
            var covered = assignments
                .Where(a => a.Role == DefensiveRole.ManCover)
                .Select(a => a.TargetSlotId)
                .ToHashSet();

            foreach (var slot in formation.Slots)
            {
                if (slot.Position is PlayerPosition.WR or PlayerPosition.TE or PlayerPosition.RB)
                {
                    Assert.Contains(slot.SlotId, covered);
                }
            }
        }
    }
}
