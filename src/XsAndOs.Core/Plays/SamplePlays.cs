namespace XsAndOs.Core;

/// <summary>
/// Built-in plays used until the drawing UI exists. These stand in for user-drawn
/// plays, so they only use data a user could produce (waypoints + menu picks).
/// </summary>
public static class SamplePlays
{
    /// <summary>Quick game: WR1 slant is the first read, RB flat the checkdown.</summary>
    public static readonly PlayDesign SlantFlat = new(
        Name: "slant-flat",
        FormationName: "Shotgun",
        Kind: PlayKind.Pass,
        Routes:
        [
            new RouteAssignment("WR1", [new Vec2(0f, 3f), new Vec2(9f, 9f)]),
            new RouteAssignment("RB1", [new Vec2(-5f, 3f), new Vec2(-12f, 4.5f)]),
            new RouteAssignment("WR3", [new Vec2(0f, 4f), new Vec2(-5f, 4.5f)]),
            new RouteAssignment("WR2", [new Vec2(0f, 16f)]),
        ],
        Blocking:
        [
            new BlockingAssignment("LT", BlockType.PassProtect),
            new BlockingAssignment("LG", BlockType.PassProtect),
            new BlockingAssignment("C", BlockType.PassProtect),
            new BlockingAssignment("RG", BlockType.PassProtect),
            new BlockingAssignment("RT", BlockType.PassProtect),
            new BlockingAssignment("TE1", BlockType.PassProtect),
        ],
        DropbackDepth: 0.5f);

    /// <summary>Four verticals out of the gun: outside gos, seam reads inside.</summary>
    public static readonly PlayDesign FourVerts = new(
        Name: "four-verts",
        FormationName: "Shotgun",
        Kind: PlayKind.Pass,
        Routes:
        [
            new RouteAssignment("WR3", [new Vec2(2f, 26f)]),
            new RouteAssignment("TE1", [new Vec2(-1f, 22f)]),
            new RouteAssignment("WR1", [new Vec2(1f, 28f)]),
            new RouteAssignment("WR2", [new Vec2(-1f, 28f)]),
        ],
        Blocking:
        [
            new BlockingAssignment("LT", BlockType.PassProtect),
            new BlockingAssignment("LG", BlockType.PassProtect),
            new BlockingAssignment("C", BlockType.PassProtect),
            new BlockingAssignment("RG", BlockType.PassProtect),
            new BlockingAssignment("RT", BlockType.PassProtect),
            new BlockingAssignment("RB1", BlockType.PassProtect),
        ],
        DropbackDepth: 2f);

    /// <summary>Inside zone right from under center.</summary>
    public static readonly PlayDesign InsideZone = new(
        Name: "inside-zone",
        FormationName: "Ace",
        Kind: PlayKind.Run,
        Routes:
        [
            // Receivers clear out / occupy their corners.
            new RouteAssignment("WR1", [new Vec2(0f, 10f)]),
            new RouteAssignment("WR2", [new Vec2(0f, 10f)]),
            new RouteAssignment("WR3", [new Vec2(0f, 8f)]),
        ],
        Blocking:
        [
            new BlockingAssignment("LT", BlockType.RunBlockZoneRight),
            new BlockingAssignment("LG", BlockType.RunBlockZoneRight),
            new BlockingAssignment("C", BlockType.RunBlockZoneRight),
            new BlockingAssignment("RG", BlockType.RunBlockZoneRight),
            new BlockingAssignment("RT", BlockType.RunBlockZoneRight),
            new BlockingAssignment("TE1", BlockType.RunBlockZoneRight),
        ],
        BallCarrierSlotId: "RB1",
        RunLane: [new Vec2(1.5f, 8f), new Vec2(2f, 26f)],
        HandoffTime: 0.9f);

    /// <summary>Toss sweep left behind a pulling guard, with both left receivers blocking.</summary>
    public static readonly PlayDesign HbToss = new(
        Name: "hb-toss",
        FormationName: "Ace",
        Kind: PlayKind.Run,
        Routes:
        [
            new RouteAssignment("WR2", [new Vec2(0f, 10f)]),
        ],
        Blocking:
        [
            new BlockingAssignment("LT", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("LG", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("C", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("RG", BlockType.PullLeft),
            new BlockingAssignment("RT", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("TE1", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("WR1", BlockType.RunBlockZoneLeft),
            new BlockingAssignment("WR3", BlockType.RunBlockZoneLeft),
        ],
        BallCarrierSlotId: "RB1",
        RunLane: [new Vec2(-7f, -1f), new Vec2(-12f, 4f), new Vec2(-14f, 24f)],
        HandoffTime: 0.5f);

    public static readonly IReadOnlyList<PlayDesign> All = [SlantFlat, FourVerts, InsideZone, HbToss];

    public static PlayDesign ByName(string name)
    {
        foreach (var p in All)
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }

        throw new ArgumentException($"Unknown play '{name}'. Known: {string.Join(", ", GetNames())}");
    }

    private static IEnumerable<string> GetNames()
    {
        foreach (var p in All)
        {
            yield return p.Name;
        }
    }
}
