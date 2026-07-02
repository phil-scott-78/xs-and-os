namespace XsAndOs.Core;

internal static class DefenseBehaviors
{
    public static void Rusher(SimContext ctx, SimPlayer p)
    {
        if (ctx.Ball.InAir)
        {
            // Ball's gone: peel toward the landing point to join pursuit.
            p.DesiredTarget = ctx.Ball.FlightTarget;
            p.DesiredSpeed = p.MaxSpeed;
            return;
        }

        var carrierIdx = ctx.Ball.CarrierIndex;
        p.DesiredTarget = carrierIdx >= 0 ? InterceptPoint(p, ctx.Players[carrierIdx]) : ctx.Ball.Pos;
        p.DesiredSpeed = p.MaxSpeed;
    }

    /// <summary>
    /// True intercept solution: the point on the carrier's current path the chaser
    /// can actually reach at max speed. Falls back to a deep cutoff angle when the
    /// carrier is flat-out faster — hem him toward help instead of trailing him.
    /// </summary>
    private static Vec2 InterceptPoint(SimPlayer chaser, SimPlayer carrier)
    {
        var r = carrier.Pos - chaser.Pos;
        var v = carrier.Vel;
        var s = chaser.MaxSpeed;

        var a = Vec2.Dot(v, v) - s * s;
        var b = 2f * Vec2.Dot(r, v);
        var c = Vec2.Dot(r, r);

        float t;
        if (global::System.Math.Abs(a) < 0.5f)
        {
            // Nearly equal speeds: linear solution (or no closing at all).
            t = b < -0.1f ? -c / b : 3f;
        }
        else
        {
            var disc = b * b - 4f * a * c;
            if (disc < 0f)
            {
                t = 3f;
            }
            else
            {
                var sqrt = (float)global::System.Math.Sqrt(disc);
                var t1 = (-b - sqrt) / (2f * a);
                var t2 = (-b + sqrt) / (2f * a);
                t = t1 > 0.02f && t2 > 0.02f ? global::System.Math.Min(t1, t2)
                    : t1 > 0.02f ? t1
                    : t2 > 0.02f ? t2
                    : 3f;
            }
        }

        t = global::System.Math.Clamp(t, 0.02f, 6f);
        return carrier.Pos + v * t;
    }

    public static void ManCover(SimContext ctx, SimPlayer p)
    {
        if (p.CoverTargetIndex < 0)
        {
            p.DesiredSpeed = 0f;
            return;
        }

        var receiver = ctx.Players[p.CoverTargetIndex];

        if (ctx.Ball.InAir)
        {
            // One awareness check to recognize the throw and break on the ball.
            if (!p.ReactedToThrow)
            {
                p.ReactedToThrow = true;
                p.BreakOnBall = ctx.Rng.Chance(0.25f + 0.65f * p.Attr.Awareness / 100f);
            }

            if (p.BreakOnBall)
            {
                p.DesiredTarget = ctx.Ball.FlightTarget;
                p.DesiredSpeed = p.MaxSpeed;
                return;
            }
        }

        // Detect route breaks; eat a reaction delay before re-tracking.
        var speed = receiver.Vel.Length;
        if (speed > 2f)
        {
            var heading = receiver.Vel / speed;
            if (p.LastSeenTargetHeading != Vec2.Zero
                && Vec2.Dot(heading, p.LastSeenTargetHeading) < CosRouteBreak)
            {
                p.ReactionTimer = Tuning.CoverageReactionSeconds(p.Attr.Awareness);
            }

            p.LastSeenTargetHeading = heading;
        }

        if (p.ReactionTimer > 0f)
        {
            // Frozen on the old aim point (DesiredTarget persists from last tick).
            p.DesiredSpeed = p.MaxSpeed;
            return;
        }

        // Mirror control law: match the receiver's velocity plus a correction toward
        // the leverage point (a cushion on the end-zone side). The defender is never
        // flat-footed while the receiver accelerates; separation comes from
        // break-reaction freezes, turn speed loss, and raw speed differences.
        var leverage = receiver.Pos + receiver.Vel * 0.2f + new Vec2(0f, Tuning.ManTrailDistance);
        var desiredVel = receiver.Vel + (leverage - p.Pos) * Tuning.MirrorCorrectionGain;
        var desiredSpeed = global::System.Math.Min(p.MaxSpeed, desiredVel.Length);
        p.DesiredTarget = p.Pos + desiredVel;
        p.DesiredSpeed = desiredSpeed;
    }

    public static void ZoneCover(SimContext ctx, SimPlayer p)
    {
        if (ctx.Ball.InAir)
        {
            // Drive on any throw landing near the zone.
            if (Vec2.Distance(ctx.Ball.FlightTarget, p.ZoneLandmark) < p.ZoneHalfWidth + 5f
                || Vec2.Distance(ctx.Ball.FlightTarget, p.Pos) < 8f)
            {
                p.DesiredTarget = ctx.Ball.FlightTarget;
                p.DesiredSpeed = p.MaxSpeed;
                return;
            }
        }

        if (!p.ZoneArrived)
        {
            if (Vec2.Distance(p.Pos, p.ZoneLandmark) < 1f)
            {
                p.ZoneArrived = true;
            }
            else
            {
                // Underneath defenders drop with their eyes in the backfield — slower,
                // and not so deep that every run gashes the vacated box.
                p.DesiredTarget = p.ZoneLandmark;
                p.DesiredSpeed = p.MaxSpeed * (p.IsDeepZone ? 0.95f : 0.7f);
                return;
            }
        }

        if (p.IsDeepZone)
        {
            DeepZoneMirror(ctx, p);
        }
        else
        {
            p.DesiredTarget = UnderneathZoneTarget(ctx, p);
            p.DesiredSpeed = p.MaxSpeed * 0.95f;
        }
    }

    /// <summary>
    /// Deep defenders velocity-match the deepest threat in their zone while staying
    /// over the top — same mirror control law as man coverage, so they're never
    /// flat-footed when a vertical route arrives at speed.
    /// </summary>
    private static void DeepZoneMirror(SimContext ctx, SimPlayer p)
    {
        var (first, second) = FindDeepThreats(ctx, p);
        if (first == null)
        {
            p.DesiredTarget = p.ZoneLandmark;
            p.DesiredSpeed = p.MaxSpeed;
            return;
        }

        // With two verticals in the zone, split the difference (weighted to the
        // deeper one) instead of locking onto one and conceding the other seam.
        var trackPos = first.Pos;
        var trackVel = first.Vel;
        if (second != null)
        {
            trackPos = Vec2.Lerp(first.Pos, second.Pos, 0.35f);
            trackVel = Vec2.Lerp(first.Vel, second.Vel, 0.35f);
        }

        var x = Clamp(trackPos.X, p.ZoneLandmark.X - p.ZoneHalfWidth, p.ZoneLandmark.X + p.ZoneHalfWidth);
        var y = global::System.Math.Max(p.ZoneLandmark.Y, trackPos.Y + Tuning.DeepZoneCushion);
        var overTop = new Vec2(x, y);
        var desiredVel = trackVel + (overTop - p.Pos) * Tuning.MirrorCorrectionGain;
        p.DesiredTarget = p.Pos + desiredVel;
        p.DesiredSpeed = global::System.Math.Min(p.MaxSpeed, desiredVel.Length);
    }

    /// <summary>The two deepest active threats inside (or entering) this deep zone.</summary>
    private static (SimPlayer? First, SimPlayer? Second) FindDeepThreats(SimContext ctx, SimPlayer p)
    {
        SimPlayer? first = null;
        SimPlayer? second = null;
        foreach (var o in ctx.Players)
        {
            if (!o.IsOffense || (o.Job != Job.RouteRunner && o.Job != Job.BallCarrier))
            {
                continue;
            }

            if (global::System.Math.Abs(o.Pos.X - p.ZoneLandmark.X) > p.ZoneHalfWidth + 2f)
            {
                continue;
            }

            if (o.Pos.Y < p.ZoneLandmark.Y - 6f)
            {
                continue;
            }

            if (first == null || o.Pos.Y > first.Pos.Y)
            {
                second = first;
                first = o;
            }
            else if (second == null || o.Pos.Y > second.Pos.Y)
            {
                second = o;
            }
        }

        return (first, second);
    }

    /// <summary>Underneath defenders shade the nearest route in their area but never vacate the zone.</summary>
    private static Vec2 UnderneathZoneTarget(SimContext ctx, SimPlayer p)
    {
        SimPlayer? threat = null;
        var bestDist = global::System.Math.Max(Tuning.UnderneathZoneRadius, p.ZoneHalfWidth);
        foreach (var o in ctx.Players)
        {
            if (!o.IsOffense || (o.Job != Job.RouteRunner && o.Job != Job.BallCarrier))
            {
                continue;
            }

            var dist = Vec2.Distance(o.Pos, p.ZoneLandmark);
            if (dist < bestDist)
            {
                threat = o;
                bestDist = dist;
            }
        }

        if (threat == null)
        {
            return p.ZoneLandmark;
        }

        return new Vec2(
            Clamp(threat.Pos.X, p.ZoneLandmark.X - p.ZoneHalfWidth, p.ZoneLandmark.X + p.ZoneHalfWidth),
            Clamp(threat.Pos.Y, p.ZoneLandmark.Y - 3f, p.ZoneLandmark.Y + 3f));
    }

    public static void Pursuer(SimContext ctx, SimPlayer p)
    {
        if (p.ReactionTimer > 0f)
        {
            p.DesiredSpeed = 0f;
            return;
        }

        var carrierIdx = ctx.Ball.CarrierIndex;
        if (carrierIdx < 0)
        {
            p.DesiredSpeed = 0f;
            return;
        }

        var carrier = ctx.Players[carrierIdx];
        p.DesiredTarget = InterceptPoint(p, carrier);
        p.DesiredSpeed = p.MaxSpeed;
    }

    // cos(40 degrees) — the route-break recognition threshold.
    private const float CosRouteBreak = 0.766f;

    private static float Clamp(float v, float min, float max) =>
        v < min ? min : v > max ? max : v;
}
