namespace XsAndOs.Core;

internal enum SimPhase
{
    PreSnap,
    Dropback,
    BallCarried,
    BallInAir,
    Dead,
}

internal sealed class BallState
{
    public Vec2 Pos;
    public int CarrierIndex = -1;
    public bool InAir;

    // Flight (valid while InAir)
    public Vec2 FlightStart;
    public Vec2 FlightTarget;
    public float FlightTime;
    public float FlightElapsed;
    public int IntendedReceiverIndex = -1;
    public bool ThrownUnderPressure;
}

internal sealed class Engagement
{
    public int BlockerIndex;
    public int RusherIndex;
    public float Seconds;
}

/// <summary>Everything one simulation run needs, threaded through every behavior call.</summary>
internal sealed class SimContext
{
    public SimRandom Rng = null!;
    public PlayDesign Play = null!;
    public DefensiveCall Defense = null!;
    public SimPlayer[] Players = [];
    public Vec2 BallSnapPos;
    public float LosY;
    public int Seed;
    public IReadOnlyList<FormationSlot> FormationSlots = [];

    /// <summary>Player indices of the QB's reads, in progression (draw) order.</summary>
    public int[] ProgressionIndices = [];

    public Vec2 QbDropSpot;
    public bool WasCatch;

    public int Tick;
    public float Time => Tick * Tuning.Dt;
    public SimPhase Phase = SimPhase.PreSnap;
    public BallState Ball = new();
    public List<Engagement> Engagements = [];
    public List<PlayEvent> Events = [];
    public List<Frame> Frames = [];

    // --- QB scanning state ---
    public int ReadIndex;
    public float ReadTimer;
    public float ScanSeconds;

    /// <summary>Perception error for the current read; resampled per read, NOT per tick.</summary>
    public float ReadNoise;
    public bool ReadNoiseSampled;

    // --- Terminal state ---
    public PlayOutcome? Outcome;
    public Vec2 DeadSpot;
    public float DeadTime;

    public SimPlayer Qb = null!;

    public void Emit(PlayEventType type, int actor = -1, int target = -1, Vec2 spot = default)
    {
        Events.Add(new PlayEvent(Tick, Time, type, actor, target, spot));
    }

    public SimPlayer? FindBySlot(string slotId)
    {
        foreach (var p in Players)
        {
            if (p.IsOffense && p.SlotId == slotId)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>Distance from the nearest free (unengaged, unstunned) pass rusher to the QB.</summary>
    public float NearestFreeRusherDistance()
    {
        var best = float.MaxValue;
        foreach (var p in Players)
        {
            if (!p.IsOffense && p.Job == Job.Rusher && p.EngagedWith < 0 && p.StunTimer <= 0f)
            {
                var d = Vec2.Distance(p.Pos, Qb.Pos);
                if (d < best)
                {
                    best = d;
                }
            }
        }

        return best;
    }

    public void SetDead(PlayOutcome outcome, Vec2 spot)
    {
        if (Phase == SimPhase.Dead)
        {
            return;
        }

        Phase = SimPhase.Dead;
        Outcome = outcome;
        DeadSpot = spot;
        DeadTime = Time;
        Ball.InAir = false;
        Emit(PlayEventType.PlayDead, spot: spot);
    }
}
