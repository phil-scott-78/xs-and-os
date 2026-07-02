namespace XsAndOs.Core;

/// <summary>OL/DL are undifferentiated in v1.</summary>
public enum PlayerPosition
{
    QB,
    RB,
    WR,
    TE,
    OL,
    DL,
    LB,
    CB,
    S,
}

public sealed record PlayerInfo(string Id, string Name, PlayerPosition Position, PlayerAttributes Attributes);

public sealed record Team(string Name, IReadOnlyList<PlayerInfo> Players);
