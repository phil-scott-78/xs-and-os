namespace XsAndOs.Core;

public enum CoverageShell
{
    Man,
    Cover2,
    Cover3,
}

/// <summary>
/// The called defense. Deliberately a record with room to grow
/// (blitz packages, fronts, disguises are roadmap items).
/// </summary>
public sealed record DefensiveCall(CoverageShell Shell)
{
    public static IReadOnlyList<DefensiveCall> All =>
        [new(CoverageShell.Man), new(CoverageShell.Cover2), new(CoverageShell.Cover3)];

    public static DefensiveCall Parse(string name) => name.ToLowerInvariant() switch
    {
        "man" or "cover0" or "cover1" => new DefensiveCall(CoverageShell.Man),
        "cover2" or "c2" => new DefensiveCall(CoverageShell.Cover2),
        "cover3" or "c3" => new DefensiveCall(CoverageShell.Cover3),
        _ => throw new ArgumentException($"Unknown defense '{name}'. Known: man, cover2, cover3"),
    };

    public override string ToString() => Shell.ToString();
}
