namespace XsAndOs.Core;

/// <summary>
/// The renderer-agnostic output contract. The Avalonia dev viewer consumes exactly
/// this, and Unity will later. Plain data only: yards, seconds, player indices.
/// </summary>
public readonly struct PlayerFrame
{
    public readonly Vec2 Pos;
    public readonly Vec2 Vel;

    public PlayerFrame(Vec2 pos, Vec2 vel)
    {
        Pos = pos;
        Vel = vel;
    }
}

public enum BallStateKind
{
    Held,
    InAir,
    Dead,
}

public readonly struct BallFrame
{
    public readonly Vec2 Pos;
    public readonly BallStateKind State;

    /// <summary>Index into the frame's player array, or -1 when nobody holds the ball.</summary>
    public readonly int CarrierIndex;

    public BallFrame(Vec2 pos, BallStateKind state, int carrierIndex)
    {
        Pos = pos;
        State = state;
        CarrierIndex = carrierIndex;
    }
}

/// <summary>One tick of the simulation. Frames are self-contained, so replay is random-access.</summary>
public sealed class Frame
{
    public int Tick { get; }
    public float Time { get; }

    /// <summary>22 entries: offense 0-10 (formation slot order), defense 11-21 (assignment order).</summary>
    public PlayerFrame[] Players { get; }

    public BallFrame Ball { get; }

    public Frame(int tick, float time, PlayerFrame[] players, BallFrame ball)
    {
        Tick = tick;
        Time = time;
        Players = players;
        Ball = ball;
    }
}

public enum PlayEventType
{
    Snap,
    Handoff,
    ProgressionRead,
    ThrowStart,
    Catch,
    Incomplete,
    Interception,
    BlockShed,
    BrokenTackle,
    Tackle,
    Sack,
    OutOfBounds,
    Touchdown,
    PlayDead,
    Scramble,
    ThrowAway,
}

public sealed class PlayEvent
{
    public int Tick { get; }
    public float Time { get; }
    public PlayEventType Type { get; }

    /// <summary>Primary player (thrower, tackler, defender...), or -1.</summary>
    public int ActorIndex { get; }

    /// <summary>Secondary player (target receiver, tackled carrier...), or -1.</summary>
    public int TargetIndex { get; }

    public Vec2 Spot { get; }

    public PlayEvent(int tick, float time, PlayEventType type, int actorIndex, int targetIndex, Vec2 spot)
    {
        Tick = tick;
        Time = time;
        Type = type;
        ActorIndex = actorIndex;
        TargetIndex = targetIndex;
        Spot = spot;
    }
}

public enum PlayOutcome
{
    CompletedPass,
    IncompletePass,
    Interception,
    Sack,
    RunTackled,
    OutOfBounds,
    Touchdown,
}

public sealed class PlayResult
{
    public PlayOutcome Outcome { get; }
    public float YardsGained { get; }
    public float Duration { get; }
    public int Seed { get; }

    public PlayResult(PlayOutcome outcome, float yardsGained, float duration, int seed)
    {
        Outcome = outcome;
        YardsGained = yardsGained;
        Duration = duration;
        Seed = seed;
    }
}

public sealed class SimResult
{
    public PlayResult Result { get; }
    public IReadOnlyList<Frame> Frames { get; }
    public IReadOnlyList<PlayEvent> Events { get; }

    /// <summary>22 entries in the same index order as every frame's player array.</summary>
    public IReadOnlyList<PlayerInfo> Participants { get; }

    public float LosY { get; }

    public SimResult(PlayResult result, IReadOnlyList<Frame> frames, IReadOnlyList<PlayEvent> events,
        IReadOnlyList<PlayerInfo> participants, float losY)
    {
        Result = result;
        Frames = frames;
        Events = events;
        Participants = participants;
        LosY = losY;
    }
}
