using XsAndOs.Core;

namespace XsAndOs.Playbook;

/// <summary>
/// A play being drawn on the chalkboard. Mutable editing state (routes kept in
/// WORLD coordinates while editing), compiled to an immutable <see cref="PlayDesign"/>
/// to run. Lives outside the UI so every rule here is headless-testable.
/// </summary>
public sealed class PlayDraft
{
    public string FormationName { get; private set; } = "Shotgun";
    public PlayKind Kind { get; private set; } = PlayKind.Pass;
    public float DropbackDepth { get; set; }
    public float HandoffTime { get; set; }

    /// <summary>Per-slot route waypoints in world coordinates, excluding the start dot.</summary>
    public Dictionary<string, List<Vec2>> Routes { get; } = [];

    /// <summary>Draw order = the QB's progression order.</summary>
    public List<string> RouteOrder { get; } = [];

    /// <summary>Blocking assignment per slot; null = no assignment (releases nobody blocks).</summary>
    public Dictionary<string, BlockType?> Blocks { get; } = [];

    /// <summary>Run plays: the first player a path was drawn on carries the ball.</summary>
    public string? BallCarrierSlotId { get; private set; }

    public Formation Formation => Formations.ByName(FormationName);

    public PlayDraft()
    {
        Reset("Shotgun", PlayKind.Pass);
    }

    /// <summary>Fresh sheet of chalk: clears everything and applies position defaults.</summary>
    public void Reset(string formationName, PlayKind kind)
    {
        FormationName = formationName;
        Kind = kind;
        Routes.Clear();
        RouteOrder.Clear();
        Blocks.Clear();
        BallCarrierSlotId = null;

        var underCenter = IsUnderCenter(Formation);
        DropbackDepth = underCenter ? 2f : 0.5f;
        HandoffTime = underCenter ? 0.7f : 0.4f;

        foreach (var slot in Formation.Slots)
        {
            if (slot.Position == PlayerPosition.QB)
            {
                continue;
            }

            Blocks[slot.SlotId] = DefaultBlock(slot.Position, kind);
        }
    }

    private static bool IsUnderCenter(Formation formation)
    {
        foreach (var slot in formation.Slots)
        {
            if (slot.Position == PlayerPosition.QB)
            {
                return slot.Offset.Y > -3f;
            }
        }

        return false;
    }

    private static BlockType? DefaultBlock(PlayerPosition position, PlayKind kind) => position switch
    {
        PlayerPosition.OL or PlayerPosition.TE =>
            kind == PlayKind.Pass ? BlockType.PassProtect : BlockType.RunBlockZoneRight,
        _ => null,
    };

    /// <summary>True for players who can be given a route (everyone but the QB and OL).</summary>
    public bool CanRunRoute(string slotId)
    {
        var slot = FindSlot(slotId);
        return slot != null && slot.Position is PlayerPosition.WR or PlayerPosition.TE or PlayerPosition.RB;
    }

    /// <summary>
    /// Commits a drawn path. Clears any blocking assignment. On run plays, the
    /// first path drawn on an eligible player becomes the carrier's lane.
    /// </summary>
    public void SetRoute(string slotId, List<Vec2> worldWaypoints)
    {
        if (!CanRunRoute(slotId) || worldWaypoints.Count == 0)
        {
            return;
        }

        Routes[slotId] = worldWaypoints;
        if (!RouteOrder.Contains(slotId))
        {
            RouteOrder.Add(slotId);
        }

        Blocks[slotId] = null;
        if (Kind == PlayKind.Run && BallCarrierSlotId == null)
        {
            BallCarrierSlotId = slotId;
        }
    }

    public void ClearRoute(string slotId)
    {
        Routes.Remove(slotId);
        RouteOrder.Remove(slotId);
        if (BallCarrierSlotId == slotId)
        {
            // The next-oldest drawn path inherits the ball.
            BallCarrierSlotId = Kind == PlayKind.Run && RouteOrder.Count > 0 ? RouteOrder[0] : null;
        }

        var slot = FindSlot(slotId);
        if (slot != null)
        {
            Blocks[slotId] = DefaultBlock(slot.Position, Kind);
        }
    }

    private static readonly BlockType?[] LineCycle =
    [
        BlockType.PassProtect, BlockType.RunBlockZoneLeft, BlockType.RunBlockZoneRight,
        BlockType.PullLeft, BlockType.PullRight, BlockType.LeadBlock,
    ];

    private static readonly BlockType?[] SkillCycle =
    [
        null, BlockType.PassProtect, BlockType.RunBlockZoneLeft, BlockType.RunBlockZoneRight,
        BlockType.LeadBlock,
    ];

    /// <summary>Clicking a badge advances the assignment; returns the new value.</summary>
    public BlockType? CycleBlock(string slotId)
    {
        var slot = FindSlot(slotId);
        if (slot == null || slot.Position == PlayerPosition.QB || Routes.ContainsKey(slotId))
        {
            return Blocks.TryGetValue(slotId, out var existing) ? existing : null;
        }

        var cycle = slot.Position == PlayerPosition.OL ? LineCycle : SkillCycle;
        var current = Blocks.TryGetValue(slotId, out var b) ? b : null;
        var idx = Array.IndexOf(cycle, current);
        var next = cycle[(idx + 1) % cycle.Length];
        Blocks[slotId] = next;
        return next;
    }

    /// <summary>Null when runnable; otherwise a user-facing reason.</summary>
    public string? Validate()
    {
        if (Kind == PlayKind.Pass)
        {
            return RouteOrder.Count == 0 ? "Draw at least one route for the QB to read." : null;
        }

        if (BallCarrierSlotId == null || !Routes.ContainsKey(BallCarrierSlotId))
        {
            return "Draw the ball carrier's running lane (the first path you draw carries the ball).";
        }

        return null;
    }

    /// <summary>World waypoints → the relative-coordinate PlayDesign the sim runs.</summary>
    public PlayDesign Compile(float losY)
    {
        var snap = new Vec2(Field.CenterX, losY);

        List<Vec2> Relative(string slotId)
        {
            var origin = snap + FindSlot(slotId)!.Offset;
            return Routes[slotId].Select(w => w - origin).ToList();
        }

        var routes = new List<RouteAssignment>();
        foreach (var slotId in RouteOrder)
        {
            if (slotId != BallCarrierSlotId && Routes.ContainsKey(slotId))
            {
                routes.Add(new RouteAssignment(slotId, Relative(slotId)));
            }
        }

        var blocking = new List<BlockingAssignment>();
        foreach (var slot in Formation.Slots)
        {
            if (Blocks.TryGetValue(slot.SlotId, out var type) && type is { } block)
            {
                blocking.Add(new BlockingAssignment(slot.SlotId, block));
            }
        }

        return new PlayDesign(
            Name: "sandbox",
            FormationName: FormationName,
            Kind: Kind,
            Routes: routes,
            Blocking: blocking,
            BallCarrierSlotId: Kind == PlayKind.Run ? BallCarrierSlotId : null,
            RunLane: Kind == PlayKind.Run && BallCarrierSlotId != null
                ? Relative(BallCarrierSlotId)
                : null,
            DropbackDepth: Kind == PlayKind.Pass ? DropbackDepth : 0f,
            HandoffTime: Kind == PlayKind.Run ? HandoffTime : 0f);
    }

    /// <summary>World position of a slot's dot at the current LOS.</summary>
    public Vec2 SlotWorldPos(string slotId, float losY)
    {
        var slot = FindSlot(slotId) ?? throw new ArgumentException($"Unknown slot '{slotId}'.");
        return new Vec2(Field.CenterX, losY) + slot.Offset;
    }

    private FormationSlot? FindSlot(string slotId)
    {
        foreach (var slot in Formation.Slots)
        {
            if (slot.SlotId == slotId)
            {
                return slot;
            }
        }

        return null;
    }
}
