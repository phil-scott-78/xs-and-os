# Xs and Os

A football play-design game. Pick a formation, draw the play — receiver routes,
blocking assignments, running lanes — then hit Play and watch the simulation run.
The order you draw routes is the QB's read progression. On defense you don't draw
anything: you call a scheme ("Cover 2", "Man") and the defenders derive their own
assignments.

Unity will eventually be the frontend, but the whole game lives in a plain .NET
library that Unity (or anything else) just renders.

**The sandbox is playable now**: run `dotnet run --project src/XsAndOs.Viewer`
(on a desktop), pick a formation and pass/run, click a player, click the field to
draw his route (double-click or Enter to finish, right-click to step back, Esc to
cancel, drag waypoints to adjust). Click linemen or their badges to cycle blocking
assignments. On run plays the first path drawn carries the ball. Pick a defense,
hit **Run Play**, and watch it unfold with a follow camera, ball trail, and a
seekable event timeline — then jump back to the chalkboard and tweak.

## Projects

| Project | Target | What it is |
|---|---|---|
| `src/XsAndOs.Core` | `netstandard2.1`, `net9.0` | The simulation. **Zero dependencies** so the DLL drops straight into Unity. |
| `src/XsAndOs.Playbook` | `netstandard2.1`, `net9.0` | JSON persistence for drawn plays (System.Text.Json, isolated from Core). |
| `src/XsAndOs.Cli` | `net9.0` | `xso` — run one play with play-by-play text, or thousands for tuning stats. |
| `src/XsAndOs.Viewer` | `net9.0` | Avalonia desktop replay viewer: field, moving dots, scrubbing, event seeking. |
| `tests/XsAndOs.Core.Tests` | `net9.0` | Determinism, invariant, statistical-sanity, and serialization tests. |

## How the simulation works

- **Deterministic fixed-tick sim**: 60 Hz, one seeded PCG32 RNG per run. Same play +
  same seed = bit-identical result on any runtime (custom `Vec2`/`SimRandom`, no
  `System.Random`, no trig in steering).
- **Output contract** (`SimResult`): a frame stream (22 player positions/velocities +
  ball per tick), a semantic event stream (Snap, ThrowStart, Catch, BrokenTackle,
  Touchdown...), and a `PlayResult`. Renderers only consume this — the Avalonia
  viewer today, Unity later.
- **Players are behavior state machines** driven by ~10 attributes (0–100): speed,
  acceleration, agility, strength, awareness, catching, throw power, throw accuracy,
  blocking, tackling. Attribute → physics mappings and every gameplay constant live
  in one file: `src/XsAndOs.Core/Tuning.cs`.
- Notable mechanics: QB reads his progression in drawn order with awareness-scaled
  read speed and perception error; routes are only throwable near their break;
  man/deep-zone defenders play a velocity-mirroring control law with leverage;
  blocking is engagement + shed rolls with pocket collapse and free-run moves;
  pursuit solves true intercept geometry; catches are contested by defender
  proximity; athletic QBs escape the pocket and throw the ball away outside the
  tackle box; lead blockers hunt the force defender; the defense checks into
  nickel personnel against 3+ WR formations.

## Play data model

A `PlayDesign` is exactly what a user could draw: freeform waypoint polylines per
route (relative to the player's snap spot, draw order = progression), blocking
assignments picked from a menu (pass protect, zone left/right, pull, lead), and for
runs a ball-carrier + lane + handoff time. Serialized as JSON — see
`samples/plays/*.json`. `DefensiveCall` is just a shell (Man, Cover 2, Cover 3);
`DefensiveAligner` derives alignments and assignments from it against any formation.

## Getting started

```bash
dotnet build
dotnet test

# one play, narrated
dotnet run --project src/XsAndOs.Cli -- run --play slant-flat --defense cover3 --seed 42 -v

# tuning workhorse: distribution over 500 seeds
dotnet run --project src/XsAndOs.Cli -- batch --play four-verts --defense cover2 --sims 500

# every sample play vs every shell
dotnet run --project src/XsAndOs.Cli -- matrix

# watch replays (needs a desktop; run locally, not in a headless container)
dotnet run --project src/XsAndOs.Viewer
```

## Tuning the fun

The statistical tests in `tests/XsAndOs.Core.Tests/StatsTests.cs` pin down "this
still resembles football and attributes still matter" with wide bands. The loop is:
change a constant in `Tuning.cs` → `xso batch` / `xso matrix` to eyeball the
distributions → `dotnet test` to make sure nothing structural broke.

## Roadmap

1. **Defensive depth**: blitz packages, fronts (3-4/dime/goal-line), pre-snap
   disguise; `DefensiveCall` grows non-breakingly.
2. **Sim feel**: play-action, hot routes vs pressure, fumbles, penalties, fatigue,
   designed QB runs.
3. **Sandbox depth**: progression reordering without redrawing, playbook
   saving/loading (the JSON layer already exists), drive/downs loop for stakes.
4. **Unity integration**: drop the `netstandard2.1` `XsAndOs.Core.dll` into Unity
   and render `SimResult` frames; then the game loop (drives, downs, opponent AI).
