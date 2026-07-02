namespace XsAndOs.Core;

/// <summary>
/// A drawn route: waypoints are RELATIVE to the player's snap position (+Y downfield).
/// After the last waypoint the runner keeps going along the final segment's heading.
/// The position of a route within <see cref="PlayDesign.Routes"/> is the QB's
/// progression order — draw order IS read order.
/// </summary>
public sealed record RouteAssignment(string SlotId, IReadOnlyList<Vec2> Waypoints);

public enum BlockType
{
    PassProtect,
    RunBlockZoneLeft,
    RunBlockZoneRight,
    PullLeft,
    PullRight,
    LeadBlock,
}

public sealed record BlockingAssignment(string SlotId, BlockType Type);

public enum PlayKind
{
    Pass,
    Run,
}

/// <summary>
/// A complete offensive play call. This record set is also the playbook JSON schema.
/// </summary>
/// <param name="Routes">Pass routes (and run-play decoy routes) in QB progression order.</param>
/// <param name="BallCarrierSlotId">Run plays only: who takes the handoff.</param>
/// <param name="RunLane">Run plays only: the drawn lane, relative to the carrier's snap position.</param>
/// <param name="DropbackDepth">Pass plays: additional yards the QB drops behind his snap spot.</param>
/// <param name="HandoffTime">Run plays: seconds after the snap when the mesh happens.</param>
public sealed record PlayDesign(
    string Name,
    string FormationName,
    PlayKind Kind,
    IReadOnlyList<RouteAssignment> Routes,
    IReadOnlyList<BlockingAssignment> Blocking,
    string? BallCarrierSlotId = null,
    IReadOnlyList<Vec2>? RunLane = null,
    float DropbackDepth = 0f,
    float HandoffTime = 0f);
