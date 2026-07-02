namespace XsAndOs.Core;

public enum DefensiveRole
{
    PassRush,
    ManCover,
    ZoneCover,
}

/// <summary>
/// One defender's derived job for the play, produced by <see cref="DefensiveAligner"/>.
/// </summary>
/// <param name="DefenderId">Roster id of the defender.</param>
/// <param name="AlignmentOffset">Pre-snap spot relative to the ball (+Y is the defense's side).</param>
/// <param name="TargetSlotId">Man coverage: offensive slot to cover.</param>
/// <param name="ZoneLandmark">Zone coverage: drop spot relative to the ball at snap.</param>
/// <param name="ZoneHalfWidth">Zone coverage: how far (yd, X) the zone extends either side of the landmark.</param>
/// <param name="IsDeepZone">Deep zones stay over the top of the deepest threat.</param>
public sealed record DefensiveAssignment(
    string DefenderId,
    DefensiveRole Role,
    Vec2 AlignmentOffset,
    string? TargetSlotId = null,
    Vec2 ZoneLandmark = default,
    float ZoneHalfWidth = 0f,
    bool IsDeepZone = false);
