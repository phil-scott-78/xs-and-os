namespace XsAndOs.Core;

/// <summary>What a player is doing right now. Jobs change mid-play (receiver becomes ball carrier).</summary>
internal enum Job
{
    QbPass,
    QbRun,
    QbIdle,
    RouteRunner,
    Blocker,
    BallCarrier,
    Rusher,
    ManCover,
    ZoneCover,
    Pursuer,
    Idle,
}

/// <summary>Mutable per-player runtime state. Plain fields; all logic lives in the behavior classes.</summary>
internal sealed class SimPlayer
{
    public int Index;
    public PlayerInfo Info = null!;
    public bool IsOffense;
    public string SlotId = "";

    public Vec2 Pos;
    public Vec2 Vel;

    public Job Job;

    // Desired movement, produced by behaviors each tick, consumed by the integrator.
    public Vec2 DesiredTarget;
    public float DesiredSpeed;

    /// <summary>While positive the player can only decelerate (broken tackle, shed block...).</summary>
    public float StunTimer;

    // --- Route running ---
    public Vec2[] RouteWaypoints = [];
    public int RouteWaypointIndex;
    public bool RouteDone;
    public Vec2 RouteExtensionHeading;

    // --- Blocking ---
    public BlockType? BlockAssignment;
    public Vec2 PassProAnchor;
    public bool PullArrived;

    // --- Engagement (index of the opponent, -1 when free) ---
    public int EngagedWith = -1;
    public float ShedCheckTimer;

    // --- Man coverage ---
    public int CoverTargetIndex = -1;
    public Vec2 LastSeenTargetHeading;
    public float ReactionTimer;
    public bool ReactedToThrow;
    public bool BreakOnBall;

    // --- Zone coverage ---
    public Vec2 ZoneLandmark;
    public float ZoneHalfWidth;
    public bool IsDeepZone;
    public bool ZoneArrived;

    // --- Tackling ---
    public float TackleCooldown;

    // --- Ball carrier avoidance (sticky side pick so the target doesn't flicker) ---
    public float AvoidSide;
    public float AvoidTimer;

    public PlayerAttributes Attr => Info.Attributes;

    public float MaxSpeed => Tuning.MaxSpeed(Attr.Speed);
    public float Accel => Tuning.Accel(Attr.Acceleration);
}
