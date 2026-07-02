namespace XsAndOs.Core;

/// <summary>
/// 2D vector in field coordinates (yards). Deliberately not System.Numerics.Vector2:
/// avoids UnityEngine.Vector2 ambiguity and SIMD-dependent codegen differences.
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly float X;
    public readonly float Y;

    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Vec2 Zero = new(0f, 0f);

    public float Length => (float)global::System.Math.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;

    public Vec2 Normalized
    {
        get
        {
            var len = Length;
            return len < 1e-6f ? Zero : new Vec2(X / len, Y / len);
        }
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(float s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, float s) => new(a.X / s, a.Y / s);

    public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    public static float Distance(Vec2 a, Vec2 b) => (b - a).Length;
    public static float DistanceSquared(Vec2 a, Vec2 b) => (b - a).LengthSquared;

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

    /// <summary>Moves from <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
    public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDelta)
    {
        var delta = target - current;
        var dist = delta.Length;
        if (dist <= maxDelta || dist < 1e-6f)
        {
            return target;
        }

        return current + delta / dist * maxDelta;
    }

    public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);
    public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
    public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
    public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}
