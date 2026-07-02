namespace XsAndOs.Core;

/// <summary>
/// Blocking engagement lifecycle: create on proximity, contest every tick,
/// shed on an interval roll. Engaged players' velocity is fully owned here.
/// </summary>
internal static class Engagements
{
    public static void Update(SimContext ctx)
    {
        BreakStale(ctx);
        CreateNew(ctx);
        Contest(ctx);
    }

    /// <summary>Blocks release once the ball carrier has clearly left the neighborhood.</summary>
    private static void BreakStale(SimContext ctx)
    {
        if (ctx.Phase != SimPhase.BallCarried || ctx.Ball.CarrierIndex < 0
            || ctx.Ball.CarrierIndex == ctx.Qb.Index)
        {
            return;
        }

        var carrier = ctx.Players[ctx.Ball.CarrierIndex];
        for (var i = ctx.Engagements.Count - 1; i >= 0; i--)
        {
            var e = ctx.Engagements[i];
            var mid = Vec2.Lerp(ctx.Players[e.BlockerIndex].Pos, ctx.Players[e.RusherIndex].Pos, 0.5f);
            if (Vec2.Distance(carrier.Pos, mid) > 6f)
            {
                Release(ctx, i);
            }
        }
    }

    private static void CreateNew(SimContext ctx)
    {
        // During a pass drop only actual rushers get blocked; on runs (and after a catch)
        // anyone at the point of attack can be engaged.
        var rushersOnly = ctx.Play.Kind == PlayKind.Pass
            && (ctx.Phase == SimPhase.Dropback || ctx.Phase == SimPhase.BallInAir);

        foreach (var blocker in ctx.Players)
        {
            if (!blocker.IsOffense || blocker.Job != Job.Blocker
                || blocker.EngagedWith >= 0 || blocker.StunTimer > 0f)
            {
                continue;
            }

            SimPlayer? target = null;
            var bestDist = Tuning.EngageRadius;
            foreach (var d in ctx.Players)
            {
                if (d.IsOffense || d.EngagedWith >= 0 || d.StunTimer > 0f || d.NoBlockTimer > 0f)
                {
                    continue;
                }

                if (rushersOnly && d.Job != Job.Rusher)
                {
                    continue;
                }

                var dist = Vec2.Distance(blocker.Pos, d.Pos);
                if (dist < bestDist)
                {
                    target = d;
                    bestDist = dist;
                }
            }

            if (target != null)
            {
                blocker.EngagedWith = target.Index;
                target.EngagedWith = blocker.Index;
                blocker.ShedCheckTimer = 0f;
                ctx.Engagements.Add(new Engagement { BlockerIndex = blocker.Index, RusherIndex = target.Index });
            }
        }
    }

    private static void Contest(SimContext ctx)
    {
        for (var i = ctx.Engagements.Count - 1; i >= 0; i--)
        {
            var e = ctx.Engagements[i];
            var blocker = ctx.Players[e.BlockerIndex];
            var rusher = ctx.Players[e.RusherIndex];
            e.Seconds += Tuning.Dt;

            var blockScore = Tuning.BlockScore(blocker.Attr, ctx.Rng);
            var rushScore = Tuning.RushScore(rusher.Attr, ctx.Rng);

            var push = PushVector(ctx, blocker, rusher, blockerWinning: blockScore >= rushScore);
            blocker.Vel = push;
            rusher.Vel = push;

            blocker.ShedCheckTimer += Tuning.Dt;
            if (blocker.ShedCheckTimer >= Tuning.ShedCheckInterval)
            {
                blocker.ShedCheckTimer = 0f;
                if (ctx.Rng.Chance(Tuning.ShedChance(rushScore, blockScore)))
                {
                    ctx.Emit(PlayEventType.BlockShed, rusher.Index, blocker.Index, blocker.Pos);
                    blocker.StunTimer = Tuning.BlockerRecoverySeconds;
                    rusher.NoBlockTimer = Tuning.ShedFreeRunSeconds;
                    Release(ctx, i);
                }
            }
        }
    }

    private static Vec2 PushVector(SimContext ctx, SimPlayer blocker, SimPlayer rusher, bool blockerWinning)
    {
        if (blocker.BlockAssignment == BlockType.PassProtect)
        {
            var axis = (ctx.Qb.Pos - rusher.Pos).Normalized;
            // A pass-pro win is a stalemate (anchor, don't pancake); a loss is the
            // pocket visibly caving toward the QB.
            return blockerWinning ? -axis * (Tuning.EngagementPushSpeed * 0.1f) : axis * Tuning.EngagementPushSpeed;
        }

        if (blockerWinning)
        {
            var dir = blocker.BlockAssignment switch
            {
                BlockType.RunBlockZoneLeft => new Vec2(-0.6f, 0.8f),
                BlockType.RunBlockZoneRight => new Vec2(0.6f, 0.8f),
                BlockType.PullLeft => new Vec2(-0.8f, 0.6f),
                BlockType.PullRight => new Vec2(0.8f, 0.6f),
                _ => new Vec2(0f, 1f),
            };
            return dir.Normalized * Tuning.EngagementPushSpeed;
        }

        // Rusher winning a run block fights toward the ball.
        var toBall = (ctx.Ball.Pos - rusher.Pos).Normalized;
        return toBall * Tuning.EngagementPushSpeed;
    }

    private static void Release(SimContext ctx, int engagementIndex)
    {
        var e = ctx.Engagements[engagementIndex];
        ctx.Players[e.BlockerIndex].EngagedWith = -1;
        ctx.Players[e.RusherIndex].EngagedWith = -1;
        ctx.Engagements.RemoveAt(engagementIndex);
    }
}
