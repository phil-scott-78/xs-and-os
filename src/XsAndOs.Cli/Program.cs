using XsAndOs.Cli;
using XsAndOs.Core;
using XsAndOs.Playbook;

var opts = CliArgs.Parse(args);
try
{
    return opts.Verb switch
    {
        "run" => RunOnce(opts),
        "batch" => Batch(opts),
        "matrix" => Matrix(opts),
        "list-plays" => ListPlays(opts),
        "debug-chase" => DebugChase(opts),
        "debug-pressure" => DebugPressure(opts),
        "export-samples" => ExportSamples(opts),
        _ => Usage(),
    };
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int RunOnce(CliArgs opts)
{
    var play = ResolvePlay(opts.Play ?? throw new ArgumentException("--play is required"));
    var defense = DefensiveCall.Parse(opts.Defense ?? "cover3");
    var sim = Sim.Run(play, defense, opts.Seed);
    Console.WriteLine($"{play.Name} ({play.FormationName}) vs {defense}, seed {opts.Seed}:");
    Narrator.Print(sim, opts.Verbose);
    return 0;
}

static int Batch(CliArgs opts)
{
    var play = ResolvePlay(opts.Play ?? throw new ArgumentException("--play is required"));
    var defense = DefensiveCall.Parse(opts.Defense ?? "cover3");
    var results = BatchStats.RunBatch(play, defense, opts.Sims, opts.SeedStart);
    Console.WriteLine($"{play.Name} ({play.FormationName}) vs {defense}, {opts.Sims} sims (seeds {opts.SeedStart}..{opts.SeedStart + opts.Sims - 1}):");
    BatchStats.PrintReport(results);
    return 0;
}

static int Matrix(CliArgs opts)
{
    Console.WriteLine($"All plays x all shells, {opts.Sims} sims each:");
    BatchStats.PrintMatrix(SamplePlays.All, opts.Sims);
    return 0;
}

static int ListPlays(CliArgs opts)
{
    Console.WriteLine("built-in:");
    foreach (var p in SamplePlays.All)
    {
        Console.WriteLine($"  {p.Name,-14} {p.FormationName,-8} {p.Kind}");
    }

    if (Directory.Exists(opts.PlaysDir))
    {
        Console.WriteLine($"from {opts.PlaysDir}:");
        foreach (var p in PlaybookSerializer.LoadDirectory(opts.PlaysDir))
        {
            Console.WriteLine($"  {p.Name,-14} {p.FormationName,-8} {p.Kind}");
        }
    }

    return 0;
}

static int ExportSamples(CliArgs opts)
{
    Directory.CreateDirectory(opts.PlaysDir);
    foreach (var play in SamplePlays.All)
    {
        var path = Path.Combine(opts.PlaysDir, play.Name + ".json");
        PlaybookSerializer.SaveToFile(play, path);
        Console.WriteLine($"wrote {path}");
    }

    return 0;
}

static int DebugChase(CliArgs opts)
{
    var play = ResolvePlay(opts.Play ?? throw new ArgumentException("--play is required"));
    var defense = DefensiveCall.Parse(opts.Defense ?? "cover3");
    var sim = Sim.Run(play, defense, opts.Seed);
    foreach (var frame in sim.Frames)
    {
        if (frame.Time is > 0.9f and < 4.0f && frame.Tick % 12 == 0)
        {
            string P(string id)
            {
                var i = Enumerable.Range(0, 22).First(k => sim.Participants[k].Id == id);
                var f = frame.Players[i];
                return $"{id}=({f.Pos.X:0.0},{f.Pos.Y:0.0})";
            }

            Console.WriteLine($"    t={frame.Time:0.00} {P("WR1")} {P("WR3")} {P("CB1")} {P("FS")} " +
                              $"ball=({frame.Ball.Pos.X:0.0},{frame.Ball.Pos.Y:0.0}) {frame.Ball.State}");
        }

        if (frame.Tick % 30 != 0 || frame.Ball.CarrierIndex < 0)
        {
            continue;
        }

        var carrier = frame.Players[frame.Ball.CarrierIndex];
        var dists = Enumerable.Range(11, 11)
            .Select(i => (i, d: MathF.Sqrt(
                (frame.Players[i].Pos.X - carrier.Pos.X) * (frame.Players[i].Pos.X - carrier.Pos.X)
                + (frame.Players[i].Pos.Y - carrier.Pos.Y) * (frame.Players[i].Pos.Y - carrier.Pos.Y))))
            .OrderBy(t => t.d).Take(3)
            .Select(t => $"{sim.Participants[t.i].Id}:{t.d:0.0}");
        Console.WriteLine($"  t={frame.Time:0.0} carrier={sim.Participants[frame.Ball.CarrierIndex].Id} " +
                          $"pos=({carrier.Pos.X:0.0},{carrier.Pos.Y:0.0}) vel={carrier.Vel.Length:0.0} nearest: {string.Join(" ", dists)}");
    }

    return 0;
}

static int DebugPressure(CliArgs opts)
{
    var play = ResolvePlay(opts.Play ?? throw new ArgumentException("--play is required"));
    var defense = DefensiveCall.Parse(opts.Defense ?? "man");
    var offense = SampleRosters.CreateOffense();
    var panicky = new Team(offense.Name, offense.Players
        .Select(p => p.Id == "QB1" ? p with { Attributes = p.Attributes with { Awareness = 30 } } : p)
        .ToArray());

    for (var seed = opts.SeedStart; seed < opts.SeedStart + opts.Sims; seed++)
    {
        var sim = Sim.Run(play, defense, panicky, SampleRosters.CreateDefense(), seed);
        var qbIdx = Enumerable.Range(0, 11).First(i => sim.Participants[i].Id == "QB1");
        var throwEvent = sim.Events.FirstOrDefault(e => e.Type is PlayEventType.ThrowStart or PlayEventType.Sack);
        var endTick = throwEvent?.Tick ?? int.MaxValue;

        var minDist = float.MaxValue;
        var minWho = "";
        var minT = 0f;
        foreach (var frame in sim.Frames.Where(f => f.Tick <= endTick))
        {
            var qb = frame.Players[qbIdx].Pos;
            for (var i = 11; i < 22; i++)
            {
                var d = MathF.Sqrt((frame.Players[i].Pos.X - qb.X) * (frame.Players[i].Pos.X - qb.X)
                    + (frame.Players[i].Pos.Y - qb.Y) * (frame.Players[i].Pos.Y - qb.Y));
                if (d < minDist)
                {
                    minDist = d;
                    minWho = sim.Participants[i].Id;
                    minT = frame.Time;
                }
            }
        }

        Console.WriteLine($"  seed {seed}: {throwEvent?.Type.ToString() ?? "none"} at " +
                          $"{(throwEvent != null ? throwEvent.Time : sim.Result.Duration):0.00}s, " +
                          $"closest defender pre-release: {minWho} {minDist:0.00}yd at {minT:0.00}s -> {sim.Result.Outcome}");
    }

    return 0;
}

static PlayDesign ResolvePlay(string nameOrPath)
{
    if (File.Exists(nameOrPath))
    {
        return PlaybookSerializer.LoadFromFile(nameOrPath);
    }

    return SamplePlays.ByName(nameOrPath);
}

static int Usage()
{
    Console.WriteLine("""
        xso - Xs and Os simulation test tool

        usage:
          xso run --play <name|file.json> [--defense man|cover2|cover3] [--seed N] [-v]
          xso batch --play <name|file.json> [--defense ...] [--sims N] [--seed-start N]
          xso matrix [--sims N]
          xso list-plays [--dir <plays dir>]
          xso export-samples [--dir <plays dir>]
        """);
    return 1;
}

internal sealed record CliArgs(
    string Verb, string? Play, string? Defense, int Seed, int SeedStart, int Sims, bool Verbose, string PlaysDir)
{
    public static CliArgs Parse(string[] args)
    {
        var verb = args.Length > 0 ? args[0] : "";
        string? play = null, defense = null;
        var seed = 42;
        var seedStart = 1;
        var sims = 500;
        var verbose = false;
        var playsDir = "samples/plays";

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--play":
                    play = Next(args, ref i);
                    break;
                case "--defense":
                    defense = Next(args, ref i);
                    break;
                case "--seed":
                    seed = int.Parse(Next(args, ref i));
                    break;
                case "--seed-start":
                    seedStart = int.Parse(Next(args, ref i));
                    break;
                case "--sims":
                    sims = int.Parse(Next(args, ref i));
                    break;
                case "-v" or "--verbose":
                    verbose = true;
                    break;
                case "--dir":
                    playsDir = Next(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"unknown option '{args[i]}'");
            }
        }

        return new CliArgs(verb, play, defense, seed, seedStart, sims, verbose, playsDir);
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"option '{args[i]}' needs a value");
        }

        return args[++i];
    }
}
