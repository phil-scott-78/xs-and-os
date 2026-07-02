namespace XsAndOs.Core;

/// <summary>
/// Field geometry, all in yards. X spans the width (0 = offense's left sideline),
/// Y spans the length (0 = back of the offense's own end zone). The offense always
/// drives toward +Y; renderers flip if they want a different orientation.
/// </summary>
public static class Field
{
    public const float Width = 53.33f;
    public const float Length = 120f;

    /// <summary>Y of the offense's own goal line.</summary>
    public const float OwnGoalLineY = 10f;

    /// <summary>Y of the goal line the offense is attacking.</summary>
    public const float TargetGoalLineY = 110f;

    public const float CenterX = Width / 2f;

    /// <summary>NFL hash marks are 70'9" (23.58 yd) from each sideline.</summary>
    public const float LeftHashX = 23.58f;
    public const float RightHashX = Width - 23.58f;

    public static bool Contains(Vec2 p, float margin = 0f) =>
        p.X >= -margin && p.X <= Width + margin && p.Y >= -margin && p.Y <= Length + margin;

    /// <summary>True when X is outside the field of play (out of bounds).</summary>
    public static bool IsOutOfBoundsX(float x) => x <= 0f || x >= Width;

    public static Vec2 ClampToField(Vec2 p) => new(
        global::System.Math.Clamp(p.X, 0f, Width),
        global::System.Math.Clamp(p.Y, 0f, Length));
}
