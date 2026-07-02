namespace XsAndOs.Core;

/// <summary>
/// The public simulation entry point. Deterministic: the same inputs and seed
/// always produce an identical <see cref="SimResult"/> on the same runtime.
/// </summary>
public static class Sim
{
    /// <param name="losY">Line of scrimmage in field Y (default 50 = the offense's own 40).</param>
    public static SimResult Run(PlayDesign play, DefensiveCall defense, Team offense, Team defenseTeam,
        int seed, float losY = 50f)
    {
        if (losY <= Field.OwnGoalLineY || losY >= Field.TargetGoalLineY)
        {
            throw new ArgumentOutOfRangeException(nameof(losY),
                $"LOS must be strictly between the goal lines ({Field.OwnGoalLineY}, {Field.TargetGoalLineY}).");
        }

        return SimEngine.Run(play, defense, offense, defenseTeam, seed, losY);
    }

    /// <summary>Convenience overload using the built-in sample rosters.</summary>
    public static SimResult Run(PlayDesign play, DefensiveCall defense, int seed, float losY = 50f) =>
        Run(play, defense, SampleRosters.CreateOffense(), SampleRosters.CreateDefense(), seed, losY);
}
