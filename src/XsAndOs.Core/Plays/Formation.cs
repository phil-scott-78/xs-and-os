namespace XsAndOs.Core;

/// <summary>
/// A spot in a formation. <see cref="Offset"/> is relative to the ball at snap:
/// +X toward the offense's right, -Y behind the line of scrimmage.
/// SlotIds match the roster player ids (QB1, RB1, WR1..3, TE1, LT..RT).
/// </summary>
public sealed record FormationSlot(string SlotId, PlayerPosition Position, Vec2 Offset);

public sealed record Formation(string Name, IReadOnlyList<FormationSlot> Slots);

public static class Formations
{
    private static FormationSlot[] OffensiveLine() =>
    [
        new("LT", PlayerPosition.OL, new Vec2(-2.6f, -0.5f)),
        new("LG", PlayerPosition.OL, new Vec2(-1.3f, -0.5f)),
        new("C", PlayerPosition.OL, new Vec2(0f, -0.5f)),
        new("RG", PlayerPosition.OL, new Vec2(1.3f, -0.5f)),
        new("RT", PlayerPosition.OL, new Vec2(2.6f, -0.5f)),
    ];

    /// <summary>2x2 gun: WR1/WR3 left, WR2/TE1 right, RB beside the QB.</summary>
    public static readonly Formation Shotgun = new("Shotgun",
    [
        .. OffensiveLine(),
        new("QB1", PlayerPosition.QB, new Vec2(0f, -5f)),
        new("RB1", PlayerPosition.RB, new Vec2(-3f, -5f)),
        new("WR1", PlayerPosition.WR, new Vec2(-21f, -0.5f)),
        new("WR3", PlayerPosition.WR, new Vec2(-13f, -1.5f)),
        new("WR2", PlayerPosition.WR, new Vec2(21f, -0.5f)),
        new("TE1", PlayerPosition.TE, new Vec2(4f, -1f)),
    ]);

    /// <summary>3x1 gun: WR2 outside / WR3 middle / TE1 inside on the right, WR1 solo left.</summary>
    public static readonly Formation Trips = new("Trips",
    [
        .. OffensiveLine(),
        new("QB1", PlayerPosition.QB, new Vec2(0f, -5f)),
        new("RB1", PlayerPosition.RB, new Vec2(3f, -5f)),
        new("WR1", PlayerPosition.WR, new Vec2(-21f, -0.5f)),
        new("WR2", PlayerPosition.WR, new Vec2(21f, -0.5f)),
        new("WR3", PlayerPosition.WR, new Vec2(15f, -1.5f)),
        new("TE1", PlayerPosition.TE, new Vec2(10f, -1.5f)),
    ]);

    /// <summary>Singleback under center: TE right, slot left.</summary>
    public static readonly Formation Ace = new("Ace",
    [
        .. OffensiveLine(),
        new("QB1", PlayerPosition.QB, new Vec2(0f, -1.5f)),
        new("RB1", PlayerPosition.RB, new Vec2(0f, -6f)),
        new("WR1", PlayerPosition.WR, new Vec2(-21f, -0.5f)),
        new("WR3", PlayerPosition.WR, new Vec2(-13f, -1.5f)),
        new("WR2", PlayerPosition.WR, new Vec2(21f, -0.5f)),
        new("TE1", PlayerPosition.TE, new Vec2(4f, -1f)),
    ]);

    /// <summary>Compressed heavy set: everyone tight to the core, RB deep.</summary>
    public static readonly Formation GoalLine = new("GoalLine",
    [
        .. OffensiveLine(),
        new("QB1", PlayerPosition.QB, new Vec2(0f, -1.5f)),
        new("RB1", PlayerPosition.RB, new Vec2(0f, -5f)),
        new("TE1", PlayerPosition.TE, new Vec2(4f, -1f)),
        new("WR1", PlayerPosition.WR, new Vec2(-8f, -0.5f)),
        new("WR3", PlayerPosition.WR, new Vec2(-5.5f, -1.5f)),
        new("WR2", PlayerPosition.WR, new Vec2(8f, -1f)),
    ]);

    public static readonly IReadOnlyList<Formation> All = [Shotgun, Trips, Ace, GoalLine];

    public static Formation ByName(string name)
    {
        foreach (var f in All)
        {
            if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return f;
            }
        }

        throw new ArgumentException($"Unknown formation '{name}'. Known: {string.Join(", ", GetNames())}");
    }

    private static IEnumerable<string> GetNames()
    {
        foreach (var f in All)
        {
            yield return f.Name;
        }
    }
}
