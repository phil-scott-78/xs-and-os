namespace XsAndOs.Core;

/// <summary>Builds the initial state and runs the fixed-tick loop.</summary>
internal static class SimEngine
{
    public static SimResult Run(PlayDesign play, DefensiveCall defense, Team offense, Team defenseTeam,
        int seed, float losY)
    {
        var ctx = Setup(play, defense, offense, defenseTeam, seed, losY);

        var maxTicks = (int)(Tuning.MaxPlaySeconds * Tuning.TicksPerSecond);
        while (true)
        {
            Tick(ctx);
            if (ctx.Phase == SimPhase.Dead && ctx.Time - ctx.DeadTime >= Tuning.PostDeadSeconds)
            {
                break;
            }

            if (ctx.Tick >= maxTicks && ctx.Phase != SimPhase.Dead)
            {
                ForceDead(ctx);
            }
        }

        return BuildResult(ctx);
    }

    private static SimContext Setup(PlayDesign play, DefensiveCall defense, Team offense, Team defenseTeam,
        int seed, float losY)
    {
        var formation = Formations.ByName(play.FormationName);
        var ballSnap = new Vec2(Field.CenterX, losY);
        var assignments = DefensiveAligner.Align(defense, formation);

        var players = new SimPlayer[formation.Slots.Count + assignments.Count];
        var ctx = new SimContext
        {
            Rng = new SimRandom(seed),
            Play = play,
            Defense = defense,
            Players = players,
            BallSnapPos = ballSnap,
            LosY = losY,
            Seed = seed,
            FormationSlots = formation.Slots,
        };

        // Offense: indices 0..10 in formation slot order.
        for (var i = 0; i < formation.Slots.Count; i++)
        {
            var slot = formation.Slots[i];
            var info = FindPlayer(offense, slot.SlotId)
                ?? throw new ArgumentException($"Offense roster has no player '{slot.SlotId}' for formation {formation.Name}.");
            var p = new SimPlayer
            {
                Index = i,
                Info = info,
                IsOffense = true,
                SlotId = slot.SlotId,
                Pos = ballSnap + slot.Offset,
                Job = Job.Idle,
            };
            players[i] = p;

            if (slot.Position == PlayerPosition.QB)
            {
                ctx.Qb = p;
                p.Job = play.Kind == PlayKind.Pass ? Job.QbPass : Job.QbRun;
                ctx.QbDropSpot = p.Pos + new Vec2(0f, -play.DropbackDepth);
            }
        }

        foreach (var block in play.Blocking)
        {
            var p = ctx.FindBySlot(block.SlotId);
            if (p != null && p != ctx.Qb)
            {
                p.Job = Job.Blocker;
                p.BlockAssignment = block.Type;
                p.PassProAnchor = p.Pos + new Vec2(0f, -1f);
            }
        }

        var progression = new List<int>();
        foreach (var route in play.Routes)
        {
            var p = ctx.FindBySlot(route.SlotId);
            if (p == null || p == ctx.Qb)
            {
                continue;
            }

            p.Job = Job.RouteRunner;
            p.RouteWaypoints = ToWorld(route.Waypoints, p.Pos);
            progression.Add(p.Index);
        }

        ctx.ProgressionIndices = progression.ToArray();

        if (play.Kind == PlayKind.Run)
        {
            var carrier = ctx.FindBySlot(play.BallCarrierSlotId ?? "")
                ?? throw new ArgumentException($"Run play '{play.Name}' has no valid BallCarrierSlotId.");
            carrier.Job = Job.RouteRunner;
            carrier.RouteWaypoints = ToWorld(play.RunLane ?? [], carrier.Pos);
        }

        // Defense: indices 11..21 in aligner order.
        for (var i = 0; i < assignments.Count; i++)
        {
            var a = assignments[i];
            var info = FindPlayer(defenseTeam, a.DefenderId)
                ?? throw new ArgumentException($"Defense roster has no player '{a.DefenderId}'.");
            var idx = formation.Slots.Count + i;
            var p = new SimPlayer
            {
                Index = idx,
                Info = info,
                IsOffense = false,
                SlotId = a.DefenderId,
                Pos = ballSnap + a.AlignmentOffset,
                Job = a.Role switch
                {
                    DefensiveRole.PassRush => Job.Rusher,
                    DefensiveRole.ManCover => Job.ManCover,
                    _ => Job.ZoneCover,
                },
            };
            players[idx] = p;

            if (a.Role == DefensiveRole.ManCover)
            {
                var target = ctx.FindBySlot(a.TargetSlotId ?? "");
                if (target != null)
                {
                    p.CoverTargetIndex = target.Index;
                }
                else
                {
                    // Nobody to cover: fall back to a middle hook zone.
                    p.Job = Job.ZoneCover;
                    p.ZoneLandmark = ballSnap + new Vec2(0f, 6f);
                    p.ZoneHalfWidth = 6f;
                }
            }
            else if (a.Role == DefensiveRole.ZoneCover)
            {
                p.ZoneLandmark = ballSnap + a.ZoneLandmark;
                p.ZoneHalfWidth = a.ZoneHalfWidth;
                p.IsDeepZone = a.IsDeepZone;
            }
        }

        ctx.Ball.Pos = ballSnap;
        ctx.Ball.CarrierIndex = ctx.Qb.Index;
        return ctx;
    }

    private static void Tick(SimContext ctx)
    {
        ctx.Tick++;

        UpdatePhase(ctx);

        foreach (var p in ctx.Players)
        {
            p.StunTimer = global::System.Math.Max(0f, p.StunTimer - Tuning.Dt);
            p.TackleCooldown = global::System.Math.Max(0f, p.TackleCooldown - Tuning.Dt);
            p.NoBlockTimer = global::System.Math.Max(0f, p.NoBlockTimer - Tuning.Dt);
            if (p.ReactionTimer > 0f)
            {
                p.ReactionTimer -= Tuning.Dt;
            }
        }

        if (ctx.Phase != SimPhase.PreSnap && ctx.Phase != SimPhase.Dead)
        {
            foreach (var p in ctx.Players)
            {
                RunBehavior(ctx, p);
            }

            Engagements.Update(ctx);
        }
        else
        {
            foreach (var p in ctx.Players)
            {
                p.DesiredSpeed = 0f;
            }
        }

        Integrate(ctx);
        UpdateBall(ctx);
        CheckEvents(ctx);
        AppendFrame(ctx);
    }

    private static void UpdatePhase(SimContext ctx)
    {
        if (ctx.Phase == SimPhase.PreSnap && ctx.Time >= Tuning.PreSnapSeconds)
        {
            ctx.Phase = SimPhase.Dropback;
            ctx.Emit(PlayEventType.Snap, ctx.Qb.Index, spot: ctx.BallSnapPos);
        }

        // Run plays: the mesh happens on a timer.
        if (ctx.Phase == SimPhase.Dropback && ctx.Play.Kind == PlayKind.Run
            && ctx.Time >= Tuning.PreSnapSeconds + ctx.Play.HandoffTime)
        {
            var carrier = ctx.FindBySlot(ctx.Play.BallCarrierSlotId!)!;
            carrier.Job = Job.BallCarrier;
            ctx.Ball.CarrierIndex = carrier.Index;
            ctx.Qb.Job = Job.QbIdle;
            ctx.Phase = SimPhase.BallCarried;
            ctx.Emit(PlayEventType.Handoff, ctx.Qb.Index, carrier.Index, carrier.Pos);
            ConvertCoverageToPursuit(ctx, delayFactor: 1.5f, run: true);
        }
    }

    private static void RunBehavior(SimContext ctx, SimPlayer p)
    {
        if (p.StunTimer > 0f || p.EngagedWith >= 0)
        {
            return;
        }

        switch (p.Job)
        {
            case Job.QbPass:
            case Job.QbRun:
            case Job.QbIdle:
                OffenseBehaviors.Qb(ctx, p);
                break;
            case Job.RouteRunner:
                OffenseBehaviors.RouteRunner(ctx, p);
                break;
            case Job.Blocker:
                OffenseBehaviors.Blocker(ctx, p);
                break;
            case Job.BallCarrier:
                OffenseBehaviors.BallCarrier(ctx, p);
                break;
            case Job.Rusher:
                DefenseBehaviors.Rusher(ctx, p);
                break;
            case Job.ManCover:
                DefenseBehaviors.ManCover(ctx, p);
                break;
            case Job.ZoneCover:
                DefenseBehaviors.ZoneCover(ctx, p);
                break;
            case Job.Pursuer:
                DefenseBehaviors.Pursuer(ctx, p);
                break;
            case Job.Idle:
                p.DesiredSpeed = 0f;
                break;
        }
    }

    private static void Integrate(SimContext ctx)
    {
        foreach (var p in ctx.Players)
        {
            if (p.EngagedWith >= 0)
            {
                // Engagements.Update already set Vel to the push vector.
                p.Pos += p.Vel * Tuning.Dt;
                continue;
            }

            Vec2 desiredVel;
            if (p.StunTimer > 0f || p.DesiredSpeed <= 0.01f)
            {
                desiredVel = Vec2.Zero;
            }
            else
            {
                var dir = (p.DesiredTarget - p.Pos);
                var dist = dir.Length;
                if (dist < 0.05f)
                {
                    desiredVel = Vec2.Zero;
                }
                else
                {
                    desiredVel = dir / dist * p.DesiredSpeed;

                    // Sharp cuts cost speed; crisp cutters keep more of it.
                    var speed = p.Vel.Length;
                    if (speed > 3f && Vec2.Dot(p.Vel / speed, dir / dist) < CosSharpTurn)
                    {
                        desiredVel *= Tuning.TurnSpeedRetention(p.Attr.Agility);
                    }
                }
            }

            // Braking/redirecting is quicker than building speed from scratch.
            var accel = Vec2.Dot(p.Vel, desiredVel) < 0f || desiredVel == Vec2.Zero
                ? p.Accel * Tuning.BrakeAccelFactor
                : p.Accel;
            p.Vel = Vec2.MoveTowards(p.Vel, desiredVel, accel * Tuning.Dt);
            p.Pos += p.Vel * Tuning.Dt;

            // Non-carriers stay on the field; the carrier's sideline is handled as OOB.
            if (p.Index != ctx.Ball.CarrierIndex)
            {
                p.Pos = Field.ClampToField(p.Pos);
            }
            else
            {
                p.Pos = new Vec2(p.Pos.X, global::System.Math.Clamp(p.Pos.Y, 0f, Field.Length));
            }
        }
    }

    private static void UpdateBall(SimContext ctx)
    {
        var ball = ctx.Ball;
        if (ball.InAir)
        {
            ball.FlightElapsed += Tuning.Dt;
            var t = global::System.Math.Min(1f, ball.FlightElapsed / ball.FlightTime);
            ball.Pos = Vec2.Lerp(ball.FlightStart, ball.FlightTarget, t);
            if (ball.FlightElapsed >= ball.FlightTime && ctx.Phase == SimPhase.BallInAir)
            {
                ResolveCatch(ctx);
            }
        }
        else if (ball.CarrierIndex >= 0)
        {
            ball.Pos = ctx.Players[ball.CarrierIndex].Pos;
        }
    }

    private static void ResolveCatch(SimContext ctx)
    {
        var ball = ctx.Ball;
        var landing = ball.FlightTarget;

        var bestOffense = float.MinValue;
        var bestDefense = float.MinValue;
        SimPlayer? bestReceiver = null;
        SimPlayer? bestDefender = null;

        var intended = ball.IntendedReceiverIndex >= 0 ? ctx.Players[ball.IntendedReceiverIndex] : null;

        // How blanketed is the catch? The nearest live defender contests the ball
        // even when he can't win it outright.
        var nearestDefender = float.MaxValue;
        foreach (var d in ctx.Players)
        {
            if (d.IsOffense || d.StunTimer > 0f || d.EngagedWith >= 0)
            {
                continue;
            }

            var toBall = Vec2.Distance(d.Pos, landing);
            var toReceiver = intended != null ? Vec2.Distance(d.Pos, intended.Pos) : float.MaxValue;
            nearestDefender = global::System.Math.Min(nearestDefender, global::System.Math.Min(toBall, toReceiver));
        }

        var contestPenalty = global::System.Math.Max(0f, (Tuning.ContestRange - nearestDefender) * Tuning.ContestPenaltyPerYard);

        foreach (var p in ctx.Players)
        {
            var dist = Vec2.Distance(p.Pos, landing);
            // Defenders draped on the receiver contest even when the ball leads him away.
            var inPhase = !p.IsOffense && intended != null
                && Vec2.Distance(p.Pos, intended.Pos) < 1.2f;
            if ((dist > Tuning.CatchContestRadius && !inPhase) || p.StunTimer > 0f || p.EngagedWith >= 0)
            {
                continue;
            }

            if (p.IsOffense)
            {
                if (p.Job != Job.RouteRunner)
                {
                    continue;
                }

                var score = Tuning.CatchScore(p.Attr, isDefender: false, dist, ctx.Rng) - contestPenalty;
                if (score > bestOffense)
                {
                    bestOffense = score;
                    bestReceiver = p;
                }
            }
            else
            {
                var score = Tuning.CatchScore(p.Attr, isDefender: true, dist, ctx.Rng);
                if (score > bestDefense)
                {
                    bestDefense = score;
                    bestDefender = p;
                }
            }
        }

        ball.InAir = false;

        if (bestDefender != null && bestDefense > Tuning.CatchThreshold
            && bestDefense > bestOffense + Tuning.InterceptionMargin)
        {
            ctx.Emit(PlayEventType.Interception, bestDefender.Index, ball.IntendedReceiverIndex, landing);
            ctx.SetDead(PlayOutcome.Interception, landing);
            return;
        }

        if (bestReceiver != null && bestOffense > Tuning.CatchThreshold && bestOffense >= bestDefense)
        {
            ball.CarrierIndex = bestReceiver.Index;
            bestReceiver.Job = Job.BallCarrier;
            bestReceiver.RouteDone = true;
            // Gathering the catch costs speed — nearby defenders get their shot.
            bestReceiver.Vel *= Tuning.CatchGatherSpeedFactor;
            if (bestReceiver.RouteExtensionHeading == Vec2.Zero)
            {
                bestReceiver.RouteExtensionHeading = new Vec2(0f, 1f);
            }

            ctx.Phase = SimPhase.BallCarried;
            ctx.WasCatch = true;
            ctx.Emit(PlayEventType.Catch, bestReceiver.Index, ctx.Qb.Index, landing);
            // Everyone was already breaking on the ball — pursuit is immediate-ish.
            ConvertCoverageToPursuit(ctx, delayFactor: 0.5f, run: false);
            return;
        }

        ctx.Emit(PlayEventType.Incomplete, ball.IntendedReceiverIndex, spot: landing);
        ctx.SetDead(PlayOutcome.IncompletePass, landing);
    }

    private static void ConvertCoverageToPursuit(SimContext ctx, float delayFactor, bool run)
    {
        foreach (var p in ctx.Players)
        {
            if (!p.IsOffense && (p.Job == Job.ManCover || p.Job == Job.ZoneCover))
            {
                // On runs, deep safeties stay honest a beat longer before committing downhill.
                var factor = run && p.Job == Job.ZoneCover && p.IsDeepZone ? delayFactor * 1.6f : delayFactor;
                p.Job = Job.Pursuer;
                p.ReactionTimer = Tuning.CoverageReactionSeconds(p.Attr.Awareness) * factor;
            }
        }
    }

    private static void CheckEvents(SimContext ctx)
    {
        if (ctx.Phase != SimPhase.BallCarried && ctx.Phase != SimPhase.Dropback)
        {
            return;
        }

        var carrierIdx = ctx.Ball.CarrierIndex;
        if (carrierIdx < 0)
        {
            return;
        }

        var carrier = ctx.Players[carrierIdx];
        var carrierIsQb = carrier == ctx.Qb;

        if (!carrierIsQb)
        {
            if (carrier.Pos.Y >= Field.TargetGoalLineY)
            {
                ctx.Emit(PlayEventType.Touchdown, carrier.Index, spot: carrier.Pos);
                ctx.SetDead(PlayOutcome.Touchdown, carrier.Pos);
                return;
            }

            if (Field.IsOutOfBoundsX(carrier.Pos.X))
            {
                ctx.Emit(PlayEventType.OutOfBounds, carrier.Index, spot: carrier.Pos);
                ctx.SetDead(PlayOutcome.OutOfBounds, Field.ClampToField(carrier.Pos));
                return;
            }
        }

        // Tackle attempts from every free defender in range.
        foreach (var d in ctx.Players)
        {
            if (d.IsOffense || d.EngagedWith >= 0 || d.StunTimer > 0f || d.TackleCooldown > 0f)
            {
                continue;
            }

            var radius = carrierIsQb ? Tuning.SackRadius : Tuning.TackleRadius;
            if (Vec2.Distance(d.Pos, carrier.Pos) > radius)
            {
                continue;
            }

            if (ctx.Rng.Chance(Tuning.TackleChance(d.Attr, carrier.Attr)))
            {
                if (carrierIsQb)
                {
                    ctx.Emit(PlayEventType.Sack, d.Index, carrier.Index, carrier.Pos);
                    ctx.SetDead(PlayOutcome.Sack, carrier.Pos);
                }
                else
                {
                    ctx.Emit(PlayEventType.Tackle, d.Index, carrier.Index, carrier.Pos);
                    ctx.SetDead(ctx.WasCatch ? PlayOutcome.CompletedPass : PlayOutcome.RunTackled, carrier.Pos);
                }

                return;
            }

            ctx.Emit(PlayEventType.BrokenTackle, carrier.Index, d.Index, carrier.Pos);
            d.StunTimer = Tuning.BrokenTackleStunSeconds;
            d.TackleCooldown = Tuning.TackleRetryInterval;
            // Fighting through contact isn't free: pursuit gets a chance to close.
            carrier.Vel *= Tuning.BrokenTackleCarrierSpeedFactor;
        }
    }

    private static void ForceDead(SimContext ctx)
    {
        // Safety net: the cap should never trigger with sane tuning.
        var carrierIdx = ctx.Ball.CarrierIndex;
        if (ctx.Ball.InAir || carrierIdx < 0)
        {
            ctx.SetDead(PlayOutcome.IncompletePass, ctx.Ball.Pos);
        }
        else if (carrierIdx == ctx.Qb.Index)
        {
            ctx.SetDead(PlayOutcome.Sack, ctx.Qb.Pos);
        }
        else
        {
            ctx.SetDead(ctx.WasCatch ? PlayOutcome.CompletedPass : PlayOutcome.RunTackled,
                ctx.Players[carrierIdx].Pos);
        }
    }

    private static void AppendFrame(SimContext ctx)
    {
        var players = new PlayerFrame[ctx.Players.Length];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = new PlayerFrame(ctx.Players[i].Pos, ctx.Players[i].Vel);
        }

        var state = ctx.Phase == SimPhase.Dead ? BallStateKind.Dead
            : ctx.Ball.InAir ? BallStateKind.InAir
            : BallStateKind.Held;
        var ball = new BallFrame(ctx.Ball.Pos, state, ctx.Ball.InAir ? -1 : ctx.Ball.CarrierIndex);
        ctx.Frames.Add(new Frame(ctx.Tick, ctx.Time, players, ball));
    }

    private static SimResult BuildResult(SimContext ctx)
    {
        var outcome = ctx.Outcome ?? PlayOutcome.IncompletePass;
        float yards;
        if (outcome == PlayOutcome.IncompletePass || outcome == PlayOutcome.Interception)
        {
            yards = 0f;
        }
        else
        {
            var spotY = global::System.Math.Min(ctx.DeadSpot.Y, Field.TargetGoalLineY);
            yards = spotY - ctx.LosY;
        }

        var participants = new PlayerInfo[ctx.Players.Length];
        for (var i = 0; i < participants.Length; i++)
        {
            participants[i] = ctx.Players[i].Info;
        }

        var result = new PlayResult(outcome, yards, ctx.DeadTime, ctx.Seed);
        return new SimResult(result, ctx.Frames, ctx.Events, participants, ctx.LosY);
    }

    private static PlayerInfo? FindPlayer(Team team, string id)
    {
        foreach (var p in team.Players)
        {
            if (p.Id == id)
            {
                return p;
            }
        }

        return null;
    }

    private static Vec2[] ToWorld(IReadOnlyList<Vec2> relative, Vec2 origin)
    {
        var result = new Vec2[relative.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = origin + relative[i];
        }

        return result;
    }

    // cos(45 degrees)
    private const float CosSharpTurn = 0.707f;
}
