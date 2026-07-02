namespace XsAndOs.Core;

/// <summary>All ratings are 0-100.</summary>
public sealed record PlayerAttributes(
    int Speed,
    int Acceleration,
    int Agility,
    int Strength,
    int Awareness,
    int Catching,
    int ThrowPower,
    int ThrowAccuracy,
    int Blocking,
    int Tackling);
