namespace XsAndOs.Core;

/// <summary>
/// Every gameplay magic number lives here so the fun can be dialed in from one file
/// (iterate with `xso batch` / `xso matrix`).
/// </summary>
public static class Tuning
{
    // --- Simulation timing ---
    public const int TicksPerSecond = 60;
    public const float Dt = 1f / TicksPerSecond;
    public const float PreSnapSeconds = 0.5f;
    public const float PostDeadSeconds = 0.5f;
    public const float MaxPlaySeconds = 15f;

    // --- Movement (attribute 0-100 -> physics) ---
    /// <summary>yd/s. 100 speed = 10.5 yd/s (~4.2s forty); 0 speed = 6.5.</summary>
    public static float MaxSpeed(int speed) => 6.5f + 4.0f * speed / 100f;

    /// <summary>yd/s^2.</summary>
    public static float Accel(int acceleration) => 4.0f + 6.0f * acceleration / 100f;

    /// <summary>Speed retained through a sharp (>45 degree) direction change.</summary>
    public static float TurnSpeedRetention(int agility) => 0.55f + 0.45f * agility / 100f;

    /// <summary>Turn angle (degrees) above which the agility speed penalty applies.</summary>
    public const float SharpTurnAngleDeg = 45f;

    /// <summary>Route runners continue along their final segment at this speed fraction.</summary>
    public const float RouteExtensionSpeedFactor = 0.85f;

    /// <summary>A waypoint counts as reached inside this radius (yd).</summary>
    public const float WaypointRadius = 0.5f;

    // --- QB reads & throwing ---
    /// <summary>Seconds spent evaluating each progression read.</summary>
    public static float ReadWindowSeconds(int awareness) => 0.65f - 0.35f * awareness / 100f;

    /// <summary>Openness (yd of separation at the anticipated catch point) needed to pull the trigger.</summary>
    public const float BaseOpennessThreshold = 3.0f;

    /// <summary>Threshold decay per second after the first full progression.</summary>
    public const float OpennessThresholdDecayPerSecond = 0.8f;

    public const float MinOpennessThreshold = 1.1f;

    /// <summary>Free rusher within this range makes the QB speed up his decision.</summary>
    public const float PressureRadius = 4.0f;

    /// <summary>Threshold multiplier while under pressure.</summary>
    public const float PressureThresholdFactor = 0.55f;

    /// <summary>Deep-zone defender over the top reduces effective openness by this many yards.</summary>
    public const float DeepZoneOverTopPenalty = 2.0f;

    /// <summary>yd/s. 100 power = 30 yd/s; 0 power = 18.</summary>
    public static float BallSpeed(int throwPower) => 18f + 12f * throwPower / 100f;

    /// <summary>Standard deviation (yd) of the throw landing error.</summary>
    public static float ThrowErrorStdev(int throwAccuracy, float distance, bool pressured)
    {
        var baseError = 0.4f + distance / 25f;
        var accuracyFactor = 1.6f - 1.2f * throwAccuracy / 100f;
        var pressureFactor = pressured ? 1.6f : 1f;
        return baseError * accuracyFactor * pressureFactor;
    }

    // --- Blocking ---
    /// <summary>Blocker and rusher engage inside this range (yd).</summary>
    public const float EngageRadius = 1.1f;

    /// <summary>yd/s the engagement pair drifts toward the roll loser.</summary>
    public const float EngagementPushSpeed = 0.35f;

    /// <summary>Seconds between shed checks.</summary>
    public const float ShedCheckInterval = 0.5f;

    public static float BlockScore(PlayerAttributes a, SimRandom rng) =>
        a.Blocking + 0.5f * a.Strength + rng.NextGaussian(0f, 10f);

    public static float RushScore(PlayerAttributes a, SimRandom rng) =>
        a.Strength + 0.3f * a.Agility + rng.NextGaussian(0f, 10f);

    /// <summary>P(shed) per check; average matchup holds ~3.5-5s, a big mismatch ~1.5-2s.</summary>
    public static float ShedChance(float rushScore, float blockScore) =>
        global::System.Math.Clamp(0.04f + 0.005f * (rushScore - blockScore), 0.01f, 0.35f);

    /// <summary>Seconds a blocker is stunned after being shed.</summary>
    public const float BlockerRecoverySeconds = 0.4f;

    // --- Coverage ---
    /// <summary>Reaction delay (s) after a receiver breaks, scaled by awareness.</summary>
    public static float CoverageReactionSeconds(int awareness) => 0.35f - 0.25f * awareness / 100f;

    /// <summary>Heading change (degrees) that counts as a route break.</summary>
    public const float RouteBreakAngleDeg = 40f;

    /// <summary>Trail distance (yd) a man defender aims behind the receiver.</summary>
    public const float ManTrailDistance = 0.8f;

    /// <summary>Underneath zone radius (yd) inside which a zone defender shades a route.</summary>
    public const float UnderneathZoneRadius = 6f;

    /// <summary>Cushion (yd) deep zone defenders keep over the deepest threat.</summary>
    public const float DeepZoneCushion = 2f;

    // --- Catch / interception ---
    /// <summary>Players inside this range of the landing point contest the ball (yd).</summary>
    public const float CatchContestRadius = 1.5f;

    public static float CatchScore(PlayerAttributes a, bool isDefender, float distanceToBall, SimRandom rng)
    {
        var catching = isDefender ? 0.6f * a.Catching : a.Catching;
        return catching + rng.NextGaussian(0f, 12f) - 6f * distanceToBall;
    }

    /// <summary>Margin by which a defender must win the contest to intercept (else it's a breakup).</summary>
    public const float InterceptionMargin = 15f;

    // --- Tackling ---
    /// <summary>A free defender attempts a tackle inside this range (yd).</summary>
    public const float TackleRadius = 0.8f;

    public static float TackleChance(PlayerAttributes tackler, PlayerAttributes carrier) =>
        global::System.Math.Clamp(
            0.45f + (tackler.Tackling + 0.4f * tackler.Strength - 0.6f * carrier.Agility - 0.4f * carrier.Strength) / 150f,
            0.15f, 0.95f);

    /// <summary>Seconds a defender is stunned after a broken tackle.</summary>
    public const float BrokenTackleStunSeconds = 0.7f;

    /// <summary>Seconds between tackle attempts by the same defender.</summary>
    public const float TackleRetryInterval = 0.4f;

    /// <summary>Free rusher within this range of the QB triggers a sack attempt (yd).</summary>
    public const float SackRadius = 1.2f;

    // --- Ball carrier avoidance ---
    public const float AvoidanceConeRange = 2.5f;
    public const float AvoidanceConeHalfAngleDeg = 30f;
    public const float AvoidanceSidestep = 1.5f;
}
