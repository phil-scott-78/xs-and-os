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

    /// <summary>Deceleration/redirection is quicker than acceleration.</summary>
    public const float BrakeAccelFactor = 1.8f;

    /// <summary>Route runners continue along their final segment at this speed fraction.</summary>
    public const float RouteExtensionSpeedFactor = 0.85f;

    /// <summary>A waypoint counts as reached inside this radius (yd).</summary>
    public const float WaypointRadius = 0.5f;

    // --- QB reads & throwing ---
    /// <summary>Seconds spent evaluating each progression read.</summary>
    public static float ReadWindowSeconds(int awareness) => 0.65f - 0.35f * awareness / 100f;

    /// <summary>Openness (yd of separation at the anticipated catch point) needed to pull the trigger.</summary>
    public const float BaseOpennessThreshold = 3.0f;

    /// <summary>No throw before this many seconds after the snap, however open someone looks.</summary>
    public const float MinRouteDevelopmentSeconds = 0.7f;

    /// <summary>A read becomes throwable when the arrival point is within this route distance of the end (yd).</summary>
    public const float RouteReadyWindow = 6f;

    /// <summary>Stdev (yd) of the QB's per-read misjudgment of a receiver's separation.</summary>
    public static float OpennessPerceptionStdev(int awareness) => 0.3f + 0.9f * (1f - awareness / 100f);

    /// <summary>Extra separation demanded per second of ball hang time beyond half a second.</summary>
    public const float HangTimeThresholdPerSecond = 0.25f;

    // --- Scramble drill ---
    /// <summary>Free rusher inside this range makes the QB consider bailing (yd).</summary>
    public const float EscapeTriggerRadius = 3.5f;

    /// <summary>One-time chance the QB escapes the collapsing pocket instead of freezing.</summary>
    public static float EscapeChance(int agility, int speed, int awareness) =>
        global::System.Math.Clamp(
            0.15f + 0.5f * (0.4f * agility + 0.3f * speed + 0.3f * awareness) / 100f, 0.1f, 0.85f);

    /// <summary>Scrambling QBs move fast, but not receiver-fast — eyes are downfield.</summary>
    public const float ScrambleSpeedFactor = 0.85f;

    /// <summary>Seconds the beaten rusher needs to redirect after the QB's escape move.</summary>
    public const float EscapeJukeStunSeconds = 0.6f;

    /// <summary>Seconds after the escape move before the QB's eyes come back downfield.</summary>
    public const float ScrambleEyesDownSeconds = 1.0f;

    /// <summary>Openness threshold multiplier while scrambling — throwing on the move demands a wider window.</summary>
    public const float ScrambleThresholdFactor = 1.15f;

    /// <summary>Outside this X-distance from the snap the QB is out of the tackle box (yd).</summary>
    public const float TackleBoxHalfWidth = 4.5f;

    /// <summary>Outside the box with nobody open: smart QBs live to play the next down.</summary>
    public static float ThrowawayChance(int awareness) => 0.25f + 0.65f * awareness / 100f;

    /// <summary>Threshold decay per second once the QB has seen his first read or two.</summary>
    public const float OpennessThresholdDecayPerSecond = 1.0f;

    /// <summary>Seconds of scanning before the QB starts accepting tighter windows.</summary>
    public const float ThresholdDecayStartSeconds = 1.2f;

    /// <summary>
    /// The floor the threshold decays to: smart QBs accept a tight-window throw
    /// rather than eat the sack; low-awareness QBs hold the ball.
    /// </summary>
    public static float MinOpennessThreshold(int awareness) => 1.2f + 1.5f * (1f - awareness / 100f);

    /// <summary>Free rusher within this range makes the QB speed up his decision.</summary>
    public const float PressureRadius = 4.0f;

    /// <summary>
    /// Threshold multiplier while under pressure: a smart QB shortens his trigger and
    /// gets the ball out; a low-awareness QB barely adjusts and eats the sack.
    /// </summary>
    public static float PressureThresholdFactor(int awareness) => 1f - 0.5f * awareness / 100f;

    /// <summary>Deep-zone defender over the top reduces effective openness by this many yards.</summary>
    public const float DeepZoneOverTopPenalty = 2.0f;

    /// <summary>yd/s. 100 power = 30 yd/s; 0 power = 18.</summary>
    public static float BallSpeed(int throwPower) => 18f + 12f * throwPower / 100f;

    /// <summary>Standard deviation (yd) of the throw landing error.</summary>
    public static float ThrowErrorStdev(int throwAccuracy, float distance, bool pressured)
    {
        var baseError = 0.4f + distance / 12f;
        var accuracyFactor = 1.6f - 1.2f * throwAccuracy / 100f;
        var pressureFactor = pressured ? 1.6f : 1f;
        return baseError * accuracyFactor * pressureFactor;
    }

    // --- Blocking ---
    /// <summary>Blocker and rusher engage inside this range (yd).</summary>
    public const float EngageRadius = 1.1f;

    /// <summary>yd/s the engagement pair drifts toward the roll loser — the pocket collapses.</summary>
    public const float EngagementPushSpeed = 0.7f;

    /// <summary>Seconds between shed checks.</summary>
    public const float ShedCheckInterval = 0.5f;

    public static float BlockScore(PlayerAttributes a, SimRandom rng) =>
        a.Blocking + 0.5f * a.Strength + rng.NextGaussian(0f, 10f);

    public static float RushScore(PlayerAttributes a, SimRandom rng) =>
        a.Strength + 0.3f * a.Agility + rng.NextGaussian(0f, 10f);

    /// <summary>P(shed) per check; average matchup holds ~4-6s, a clear mismatch ~2-3s.</summary>
    public static float ShedChance(float rushScore, float blockScore) =>
        global::System.Math.Clamp(0.06f + 0.008f * (rushScore - blockScore), 0.02f, 0.4f);

    /// <summary>Seconds a blocker is stunned after being shed.</summary>
    public const float BlockerRecoverySeconds = 0.4f;

    /// <summary>Seconds a rusher who won his shed is past blockers and can't be re-engaged.</summary>
    public const float ShedFreeRunSeconds = 1.2f;

    // --- Coverage ---
    /// <summary>Reaction delay (s) after a receiver breaks, scaled by awareness.</summary>
    public static float CoverageReactionSeconds(int awareness) => 0.35f - 0.25f * awareness / 100f;

    /// <summary>Heading change (degrees) that counts as a route break.</summary>
    public const float RouteBreakAngleDeg = 40f;

    /// <summary>Cushion (yd) a man defender keeps on the end-zone side of the receiver.</summary>
    public const float ManTrailDistance = 0.8f;

    /// <summary>How aggressively a man defender corrects toward his leverage point (1/s).</summary>
    public const float MirrorCorrectionGain = 1.5f;

    /// <summary>Underneath zone radius (yd) inside which a zone defender shades a route.</summary>
    public const float UnderneathZoneRadius = 6f;

    /// <summary>Cushion (yd) deep zone defenders keep over the deepest threat.</summary>
    public const float DeepZoneCushion = 2f;

    // --- Catch / interception ---
    /// <summary>Players inside this range of the landing point contest the ball (yd).</summary>
    public const float CatchContestRadius = 2.0f;

    public static float CatchScore(PlayerAttributes a, bool isDefender, float distanceToBall, SimRandom rng)
    {
        var catching = isDefender ? 0.6f * a.Catching : a.Catching;
        return catching + rng.NextGaussian(0f, 12f) - 4f * distanceToBall;
    }

    /// <summary>Margin by which a defender must win the contest to intercept (else it's a breakup).</summary>
    public const float InterceptionMargin = 15f;

    /// <summary>Minimum contest score to secure the ball at all; below this it's a drop/breakup.</summary>
    public const float CatchThreshold = 50f;

    /// <summary>Velocity retained through the act of catching (gather + secure).</summary>
    public const float CatchGatherSpeedFactor = 0.55f;

    /// <summary>A defender within this range of the ball or receiver contests the catch (yd).</summary>
    public const float ContestRange = 2.2f;

    /// <summary>Receiver catch-score penalty per yard of defender proximity inside ContestRange.</summary>
    public const float ContestPenaltyPerYard = 15f;

    // --- Tackling ---
    /// <summary>A free defender attempts a tackle inside this range (yd).</summary>
    public const float TackleRadius = 1.4f;

    public static float TackleChance(PlayerAttributes tackler, PlayerAttributes carrier) =>
        global::System.Math.Clamp(
            0.6f + (tackler.Tackling + 0.4f * tackler.Strength - 0.6f * carrier.Agility - 0.4f * carrier.Strength) / 150f,
            0.2f, 0.95f);

    /// <summary>Seconds a defender is stunned after a broken tackle.</summary>
    public const float BrokenTackleStunSeconds = 0.4f;

    /// <summary>Velocity the carrier keeps after running through a tackle attempt.</summary>
    public const float BrokenTackleCarrierSpeedFactor = 0.7f;

    /// <summary>Seconds between tackle attempts by the same defender.</summary>
    public const float TackleRetryInterval = 0.4f;

    /// <summary>Free rusher within this range of the QB triggers a sack attempt (yd).</summary>
    public const float SackRadius = 1.2f;

    /// <summary>Seconds a free rusher pauses at the mesh to read run vs pass.</summary>
    public static float RushRunReactSeconds(int awareness) => 0.3f - 0.15f * awareness / 100f;

    /// <summary>
    /// Extra run-recognition delay per yard of distance from the mesh — news of the
    /// handoff reaches the overhang and deep defenders later than the box.
    /// </summary>
    public const float RunReadSecondsPerYard = 0.035f;

    /// <summary>Cap on the distance term so deep safeties still play run support.</summary>
    public const float MaxRunReadExtraSeconds = 0.3f;

    /// <summary>
    /// Speed multiplier for defenders chasing a live ball carrier — keeps equal-speed
    /// footraces from being decided forever at the moment of the catch/handoff.
    /// </summary>
    public const float PursuitSpeedBonus = 1.04f;

    /// <summary>Lead blockers look for the force defender within this range of the carrier (yd).</summary>
    public const float ForceDefenderRange = 12f;

    // --- Ball carrier avoidance ---
    public const float AvoidanceConeRange = 2.5f;
    public const float AvoidanceConeHalfAngleDeg = 30f;
    public const float AvoidanceSidestep = 1.5f;

    /// <summary>Seconds a carrier commits to a chosen juke side.</summary>
    public const float AvoidanceCommitSeconds = 0.4f;

    /// <summary>Carriers cut corners: waypoints count as reached from further out (yd).</summary>
    public const float CarrierTurnAnticipation = 1.5f;
}
