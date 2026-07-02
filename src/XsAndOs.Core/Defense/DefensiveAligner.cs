namespace XsAndOs.Core;

/// <summary>
/// Derives per-defender alignment and assignments from a called shell and the
/// offensive formation. Defensive assignments are never authored by the user.
/// Base personnel is a fixed 4-3: DE1 DT1 DT2 DE2 / LB1(M) LB2 LB3 / CB1 CB2 / FS SS.
/// </summary>
public static class DefensiveAligner
{
    public static IReadOnlyList<DefensiveAssignment> Align(DefensiveCall call, Formation formation)
    {
        var eligibles = GetEligiblesSortedByX(formation);
        return call.Shell switch
        {
            CoverageShell.Man => AlignMan(eligibles),
            CoverageShell.Cover2 => AlignCover2(eligibles),
            CoverageShell.Cover3 => AlignCover3(eligibles),
            _ => throw new ArgumentOutOfRangeException(nameof(call)),
        };
    }

    private static List<FormationSlot> GetEligiblesSortedByX(Formation formation)
    {
        var eligibles = new List<FormationSlot>();
        foreach (var slot in formation.Slots)
        {
            if (slot.Position is PlayerPosition.WR or PlayerPosition.TE or PlayerPosition.RB)
            {
                eligibles.Add(slot);
            }
        }

        eligibles.Sort((a, b) => a.Offset.X.CompareTo(b.Offset.X));
        return eligibles;
    }

    private static DefensiveAssignment[] FourManRush() =>
    [
        Rush("DE1", new Vec2(-3.2f, 1f)),
        Rush("DT1", new Vec2(-1.0f, 1f)),
        Rush("DT2", new Vec2(1.0f, 1f)),
        Rush("DE2", new Vec2(3.2f, 1f)),
    ];

    private static IReadOnlyList<DefensiveAssignment> AlignMan(List<FormationSlot> eligibles)
    {
        var result = new List<DefensiveAssignment>(FourManRush());

        // CBs take the widest receiver on each side; leftover eligibles go to SS/LB2/LB3
        // by proximity; LB1 spies a middle hook; FS is the deep free player.
        var leftMost = eligibles[0];
        var rightMost = eligibles[^1];
        result.Add(ManOn("CB1", leftMost));
        result.Add(ManOn("CB2", rightMost));

        var remaining = new List<FormationSlot>();
        foreach (var e in eligibles)
        {
            if (e != leftMost && e != rightMost)
            {
                remaining.Add(e);
            }
        }

        // Greedy left-to-right hand-out: LB2 patrols the left, SS the strong/right middle, LB3 the right.
        var coverers = new Queue<string>(new[] { "LB2", "SS", "LB3" });
        foreach (var slot in remaining)
        {
            if (coverers.Count == 0)
            {
                break;
            }

            result.Add(ManOn(coverers.Dequeue(), slot));
        }

        // Anyone left uncovered by roster exhaustion is the offense's problem to exploit;
        // anyone left in the coverer queue drops to a hook zone.
        while (coverers.Count > 0)
        {
            result.Add(Zone(coverers.Dequeue(), new Vec2(0f, 5f), new Vec2(0f, 6f), 6f));
        }

        result.Add(Zone("LB1", new Vec2(0f, 4.5f), new Vec2(0f, 6f), 6f));
        result.Add(Zone("FS", new Vec2(0f, 13f), new Vec2(0f, 15f), Field.Width / 2f, isDeep: true));
        return result;
    }

    private static IReadOnlyList<DefensiveAssignment> AlignCover2(List<FormationSlot> eligibles)
    {
        var result = new List<DefensiveAssignment>(FourManRush())
        {
            // Corners squat in the flats; safeties split the deep field in halves.
            Zone("CB1", CbAlignment(eligibles[0]), new Vec2(-19f, 5f), 8f),
            Zone("CB2", CbAlignment(eligibles[^1]), new Vec2(19f, 5f), 8f),
            Zone("FS", new Vec2(-13.3f, 13f), new Vec2(-13.3f, 14f), 13.5f, isDeep: true),
            Zone("SS", new Vec2(13.3f, 13f), new Vec2(13.3f, 14f), 13.5f, isDeep: true),
            Zone("LB2", new Vec2(-3.5f, 4.5f), new Vec2(-9f, 6f), 6.5f),
            Zone("LB1", new Vec2(0f, 4.5f), new Vec2(0f, 7f), 6.5f),
            Zone("LB3", new Vec2(3.5f, 4.5f), new Vec2(9f, 6f), 6.5f),
        };
        return result;
    }

    private static IReadOnlyList<DefensiveAssignment> AlignCover3(List<FormationSlot> eligibles)
    {
        var result = new List<DefensiveAssignment>(FourManRush())
        {
            // Corners bail to the outside deep thirds, FS takes the middle third,
            // SS drops down as the fourth underneath defender.
            Zone("CB1", CbAlignment(eligibles[0]), new Vec2(-17.8f, 12f), 8.9f, isDeep: true),
            Zone("CB2", CbAlignment(eligibles[^1]), new Vec2(17.8f, 12f), 8.9f, isDeep: true),
            Zone("FS", new Vec2(0f, 13f), new Vec2(0f, 13f), 8.9f, isDeep: true),
            Zone("SS", new Vec2(8f, 8f), new Vec2(16f, 5.5f), 7f),
            Zone("LB2", new Vec2(-3.5f, 4.5f), new Vec2(-16f, 5.5f), 7f),
            Zone("LB1", new Vec2(-1f, 4.5f), new Vec2(-5f, 6f), 6f),
            Zone("LB3", new Vec2(3.5f, 4.5f), new Vec2(5f, 6f), 6f),
        };
        return result;
    }

    /// <summary>Corner lines up over the widest receiver on his side.</summary>
    private static Vec2 CbAlignment(FormationSlot widest) => new(widest.Offset.X, 6f);

    private static DefensiveAssignment Rush(string id, Vec2 align) =>
        new(id, DefensiveRole.PassRush, align);

    private static DefensiveAssignment ManOn(string id, FormationSlot target) =>
        new(id, DefensiveRole.ManCover, new Vec2(target.Offset.X, ManAlignmentDepth(target)), target.SlotId);

    private static DefensiveAssignment Zone(string id, Vec2 align, Vec2 landmark, float halfWidth, bool isDeep = false) =>
        new(id, DefensiveRole.ZoneCover, align, ZoneLandmark: landmark, ZoneHalfWidth: halfWidth, IsDeepZone: isDeep);

    /// <summary>Backfield eligibles are covered from linebacker depth, not press.</summary>
    private static float ManAlignmentDepth(FormationSlot target) => target.Offset.Y < -3f ? 4.5f : 5f;
}
