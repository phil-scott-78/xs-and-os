namespace XsAndOs.Core;

/// <summary>
/// Per-tick offensive decision logic. Behaviors only produce desired movement
/// (and QB decisions); the engine integrates movement and resolves contact.
/// </summary>
internal static class OffenseBehaviors
{
    public static void RouteRunner(SimContext ctx, SimPlayer p)
    {
        // The intended receiver attacks the ball instead of finishing the route.
        if (ctx.Ball.InAir && ctx.Ball.IntendedReceiverIndex == p.Index)
        {
            p.DesiredTarget = ctx.Ball.FlightTarget;
            p.DesiredSpeed = p.MaxSpeed;
            return;
        }

        RunWaypoints(p);
    }

    public static void BallCarrier(SimContext ctx, SimPlayer p)
    {
        // Once past the drawn lane, bend toward the end zone instead of drifting sideways.
        if (p.RouteDone)
        {
            p.RouteExtensionHeading = (p.RouteExtensionHeading * 0.96f + new Vec2(0f, 1f) * 0.04f).Normalized;
        }

        RunWaypoints(p, waypointRadius: Tuning.CarrierTurnAnticipation);

        // Simple open-field instinct: sidestep the nearest free defender in the
        // forward cone. The side choice is sticky — re-picking every tick makes the
        // carrier zigzag and bleed all his speed to the turn penalty.
        p.AvoidTimer -= Tuning.Dt;
        var heading = p.Vel.LengthSquared > 1f ? p.Vel.Normalized : new Vec2(0f, 1f);
        var threat = NearestConeThreat(ctx, p, heading);
        if (threat != null && p.AvoidTimer <= 0f)
        {
            var right = new Vec2(heading.Y, -heading.X);
            var toThreat = threat.Pos - p.Pos;
            p.AvoidSide = Vec2.Dot(toThreat, right) > 0f ? -1f : 1f;
            p.AvoidTimer = Tuning.AvoidanceCommitSeconds;
        }

        if (threat == null && p.AvoidTimer <= 0f)
        {
            p.AvoidSide = 0f;
        }

        if (p.AvoidSide != 0f)
        {
            var right = new Vec2(heading.Y, -heading.X);
            p.DesiredTarget += right * (p.AvoidSide * Tuning.AvoidanceSidestep);
        }

        p.DesiredSpeed = p.MaxSpeed;
    }

    public static void Qb(SimContext ctx, SimPlayer p)
    {
        switch (p.Job)
        {
            case Job.QbRun:
                // Reverse out toward the mesh point; the engine transfers the ball at HandoffTime.
                p.DesiredTarget = ctx.BallSnapPos + new Vec2(0f, -3.5f);
                p.DesiredSpeed = p.MaxSpeed * 0.6f;
                return;

            case Job.QbIdle:
                p.DesiredSpeed = 0f;
                return;
        }

        if (Vec2.Distance(p.Pos, ctx.QbDropSpot) > 0.4f && ctx.ScanSeconds <= 0f)
        {
            p.DesiredTarget = ctx.QbDropSpot;
            p.DesiredSpeed = p.MaxSpeed * 0.8f;
            return;
        }

        // Settled: scan the progression.
        p.DesiredSpeed = 0f;
        Scan(ctx, p);
    }

    private static void Scan(SimContext ctx, SimPlayer qb)
    {
        ctx.ScanSeconds += Tuning.Dt;
        if (ctx.ProgressionIndices.Length == 0)
        {
            return;
        }

        // Let routes develop before the first throw is even considered.
        if (ctx.Time < Tuning.PreSnapSeconds + Tuning.MinRouteDevelopmentSeconds)
        {
            return;
        }

        var pressured = ctx.NearestFreeRusherDistance() < Tuning.PressureRadius;
        var threshold = global::System.Math.Max(
            Tuning.MinOpennessThreshold(qb.Attr.Awareness),
            Tuning.BaseOpennessThreshold - Tuning.OpennessThresholdDecayPerSecond * global::System.Math.Max(0f, ctx.ScanSeconds - 1.5f));
        if (pressured)
        {
            threshold *= Tuning.PressureThresholdFactor;
        }

        var receiver = ctx.Players[ctx.ProgressionIndices[ctx.ReadIndex]];
        var (openness, catchPoint, flightTime) = EvaluateReceiver(ctx, qb, receiver);

        // A route is only throwable once the ball would meet the receiver near or
        // past his break — no dumping a deep route off at its stem.
        var ready = RemainingRouteAfter(receiver, flightTime) <= Tuning.RouteReadyWindow;

        if (ready && openness > threshold)
        {
            Throw(ctx, qb, receiver, catchPoint, flightTime, pressured);
            return;
        }

        ctx.ReadTimer += Tuning.Dt;
        if (ctx.ReadTimer >= Tuning.ReadWindowSeconds(qb.Attr.Awareness))
        {
            ctx.ReadTimer = 0f;
            ctx.ReadIndex = (ctx.ReadIndex + 1) % ctx.ProgressionIndices.Length;
            ctx.Emit(PlayEventType.ProgressionRead, qb.Index, ctx.ProgressionIndices[ctx.ReadIndex]);
        }
    }

    /// <summary>Openness = separation (yd) at the anticipated catch point, defenders projected ahead.</summary>
    internal static (float Openness, Vec2 CatchPoint, float FlightTime) EvaluateReceiver(
        SimContext ctx, SimPlayer qb, SimPlayer receiver)
    {
        // Anticipation: project the receiver along his remaining ROUTE, not his raw
        // velocity — the QB throws to where the route will be when the ball arrives.
        // Two passes because flight time depends on the catch point and vice versa.
        var ballSpeed = Tuning.BallSpeed(qb.Attr.ThrowPower);
        var t0 = Vec2.Distance(qb.Pos, receiver.Pos) / ballSpeed;
        var catchPoint = Field.ClampToField(ProjectAlongRoute(receiver, t0));
        var flightTime = Vec2.Distance(qb.Pos, catchPoint) / ballSpeed;
        catchPoint = Field.ClampToField(ProjectAlongRoute(receiver, flightTime));
        flightTime = Vec2.Distance(qb.Pos, catchPoint) / ballSpeed;

        var openness = float.MaxValue;
        foreach (var d in ctx.Players)
        {
            if (d.IsOffense || d.EngagedWith >= 0 || d.StunTimer > 0f)
            {
                continue;
            }

            var projected = d.Pos + d.Vel * (flightTime * 0.5f);
            var sep = Vec2.Distance(projected, catchPoint);

            // A deep zone defender sitting over the top erases the window even at distance.
            if (d.Job == Job.ZoneCover && d.IsDeepZone
                && d.Pos.Y > catchPoint.Y
                && global::System.Math.Abs(d.Pos.X - catchPoint.X) < 5f)
            {
                sep -= Tuning.DeepZoneOverTopPenalty;
            }

            if (sep < openness)
            {
                openness = sep;
            }
        }

        return (openness, catchPoint, flightTime);
    }

    /// <summary>Route distance (yd) still left to run when the ball would arrive.</summary>
    private static float RemainingRouteAfter(SimPlayer r, float flightTime)
    {
        if (r.RouteDone)
        {
            return 0f;
        }

        var total = 0f;
        var pos = r.Pos;
        for (var i = r.RouteWaypointIndex; i < r.RouteWaypoints.Length; i++)
        {
            total += Vec2.Distance(pos, r.RouteWaypoints[i]);
            pos = r.RouteWaypoints[i];
        }

        var covered = global::System.Math.Max(r.Vel.Length, 0.75f * r.MaxSpeed) * flightTime;
        return global::System.Math.Max(0f, total - covered);
    }

    /// <summary>Where the receiver will be in <paramref name="seconds"/>, following his route.</summary>
    private static Vec2 ProjectAlongRoute(SimPlayer r, float seconds)
    {
        var speed = global::System.Math.Max(r.Vel.Length, 0.75f * r.MaxSpeed);
        var remaining = speed * seconds;
        var pos = r.Pos;
        var heading = r.Vel.LengthSquared > 1f ? r.Vel.Normalized : new Vec2(0f, 1f);

        for (var i = r.RouteDone ? r.RouteWaypoints.Length : r.RouteWaypointIndex;
             i < r.RouteWaypoints.Length && remaining > 0f;
             i++)
        {
            var to = r.RouteWaypoints[i] - pos;
            var d = to.Length;
            if (d >= remaining)
            {
                return pos + to / d * remaining;
            }

            pos = r.RouteWaypoints[i];
            remaining -= d;
            if (d > 0.1f)
            {
                heading = to / d;
            }
        }

        if (r.RouteDone && r.RouteExtensionHeading != Vec2.Zero)
        {
            heading = r.RouteExtensionHeading;
        }

        return pos + heading * (remaining * Tuning.RouteExtensionSpeedFactor);
    }

    private static void Throw(SimContext ctx, SimPlayer qb, SimPlayer receiver, Vec2 catchPoint,
        float flightTime, bool pressured)
    {
        var dist = Vec2.Distance(qb.Pos, catchPoint);
        var stdev = Tuning.ThrowErrorStdev(qb.Attr.ThrowAccuracy, dist, pressured);
        var landing = Field.ClampToField(catchPoint + new Vec2(
            ctx.Rng.NextGaussian(0f, stdev),
            ctx.Rng.NextGaussian(0f, stdev)));

        var ball = ctx.Ball;
        ball.InAir = true;
        ball.CarrierIndex = -1;
        ball.FlightStart = qb.Pos;
        ball.FlightTarget = landing;
        ball.FlightTime = global::System.Math.Max(0.15f, flightTime);
        ball.FlightElapsed = 0f;
        ball.IntendedReceiverIndex = receiver.Index;
        ball.ThrownUnderPressure = pressured;

        ctx.Phase = SimPhase.BallInAir;
        qb.Job = Job.QbIdle;
        ctx.Emit(PlayEventType.ThrowStart, qb.Index, receiver.Index, landing);
    }

    public static void Blocker(SimContext ctx, SimPlayer p)
    {
        switch (p.BlockAssignment)
        {
            case BlockType.PassProtect:
                PassProtect(ctx, p);
                break;
            case BlockType.RunBlockZoneLeft:
                ZoneBlock(ctx, p, new Vec2(-0.6f, 0.8f));
                break;
            case BlockType.RunBlockZoneRight:
                ZoneBlock(ctx, p, new Vec2(0.6f, 0.8f));
                break;
            case BlockType.PullLeft:
                Pull(ctx, p, -1f);
                break;
            case BlockType.PullRight:
                Pull(ctx, p, 1f);
                break;
            case BlockType.LeadBlock:
                LeadBlock(ctx, p);
                break;
        }
    }

    private static void PassProtect(SimContext ctx, SimPlayer p)
    {
        var threat = NearestFreeDefender(ctx, p.PassProAnchor, 7f, rushersOnly: true);
        if (threat != null)
        {
            // Slide between the anchor and the rusher, staying home.
            var toThreat = threat.Pos - p.PassProAnchor;
            var reach = global::System.Math.Min(toThreat.Length, 2f);
            p.DesiredTarget = p.PassProAnchor + toThreat.Normalized * reach;
        }
        else
        {
            p.DesiredTarget = p.PassProAnchor;
        }

        p.DesiredSpeed = p.MaxSpeed;
    }

    private static void ZoneBlock(SimContext ctx, SimPlayer p, Vec2 dir)
    {
        var probe = p.Pos + dir * 2f;
        var threat = NearestFreeDefender(ctx, probe, 5f, rushersOnly: false);
        if (threat != null)
        {
            p.DesiredTarget = LeadDefender(p, threat);
            p.DesiredSpeed = p.MaxSpeed;
        }
        else
        {
            // Nobody in the zone: climb to the second level.
            p.DesiredTarget = p.Pos + dir * 3f;
            p.DesiredSpeed = p.MaxSpeed * 0.9f;
        }
    }

    private static void Pull(SimContext ctx, SimPlayer p, float side)
    {
        if (!p.PullArrived)
        {
            var spot = ctx.BallSnapPos + new Vec2(side * 6.5f, -1.5f);
            if (Vec2.Distance(p.Pos, spot) < 1f)
            {
                p.PullArrived = true;
            }
            else
            {
                p.DesiredTarget = spot;
                p.DesiredSpeed = p.MaxSpeed;
                return;
            }
        }

        LeadBlock(ctx, p);
    }

    private static void LeadBlock(SimContext ctx, SimPlayer p)
    {
        var ballPos = ctx.Ball.Pos;
        var threat = NearestFreeDefender(ctx, ballPos + new Vec2(0f, 2f), 8f, rushersOnly: false);
        if (threat != null)
        {
            p.DesiredTarget = LeadDefender(p, threat);
        }
        else
        {
            p.DesiredTarget = ballPos + new Vec2(0f, 3f);
        }

        p.DesiredSpeed = p.MaxSpeed;
    }

    private static void RunWaypoints(SimPlayer p, float waypointRadius = Tuning.WaypointRadius)
    {
        if (!p.RouteDone)
        {
            if (p.RouteWaypoints.Length == 0)
            {
                p.RouteDone = true;
                p.RouteExtensionHeading = new Vec2(0f, 1f);
            }
            else
            {
                var wp = p.RouteWaypoints[p.RouteWaypointIndex];
                if (Vec2.Distance(p.Pos, wp) < waypointRadius)
                {
                    p.RouteWaypointIndex++;
                    if (p.RouteWaypointIndex >= p.RouteWaypoints.Length)
                    {
                        p.RouteDone = true;
                        p.RouteExtensionHeading = ComputeExtensionHeading(p);
                    }
                }
            }
        }

        if (p.RouteDone)
        {
            p.DesiredTarget = p.Pos + p.RouteExtensionHeading * 5f;
            p.DesiredSpeed = p.MaxSpeed * Tuning.RouteExtensionSpeedFactor;
        }
        else
        {
            p.DesiredTarget = p.RouteWaypoints[p.RouteWaypointIndex];
            p.DesiredSpeed = p.MaxSpeed;
        }
    }

    private static Vec2 ComputeExtensionHeading(SimPlayer p)
    {
        var n = p.RouteWaypoints.Length;
        var last = p.RouteWaypoints[n - 1];
        var prev = n >= 2 ? p.RouteWaypoints[n - 2] : p.Pos;
        var heading = (last - prev).Normalized;
        return heading == Vec2.Zero ? new Vec2(0f, 1f) : heading;
    }

    /// <summary>Meet a moving defender where he's going, not where he is.</summary>
    private static Vec2 LeadDefender(SimPlayer blocker, SimPlayer defender)
    {
        var t = global::System.Math.Min(1f, Vec2.Distance(blocker.Pos, defender.Pos) / blocker.MaxSpeed);
        return defender.Pos + defender.Vel * t;
    }

    private static SimPlayer? NearestConeThreat(SimContext ctx, SimPlayer carrier, Vec2 heading)
    {
        SimPlayer? best = null;
        var bestDist = Tuning.AvoidanceConeRange;
        // cos(30 degrees)
        const float coneCos = 0.866f;
        foreach (var d in ctx.Players)
        {
            if (d.IsOffense || d.EngagedWith >= 0 || d.StunTimer > 0f)
            {
                continue;
            }

            var to = d.Pos - carrier.Pos;
            var dist = to.Length;
            if (dist >= bestDist || dist < 0.01f)
            {
                continue;
            }

            if (Vec2.Dot(to / dist, heading) >= coneCos)
            {
                best = d;
                bestDist = dist;
            }
        }

        return best;
    }

    private static SimPlayer? NearestFreeDefender(SimContext ctx, Vec2 near, float maxDist, bool rushersOnly)
    {
        SimPlayer? best = null;
        var bestDist = maxDist;
        foreach (var d in ctx.Players)
        {
            if (d.IsOffense || d.EngagedWith >= 0 || d.StunTimer > 0f)
            {
                continue;
            }

            if (rushersOnly && d.Job != Job.Rusher)
            {
                continue;
            }

            var dist = Vec2.Distance(d.Pos, near);
            if (dist < bestDist)
            {
                best = d;
                bestDist = dist;
            }
        }

        return best;
    }
}
