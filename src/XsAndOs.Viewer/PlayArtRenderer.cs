using Avalonia;
using Avalonia.Media;
using XsAndOs.Core;

namespace XsAndOs.Viewer;

public enum ArtLineKind
{
    Route,
    CarrierLane,
}

public sealed record ArtRoute(IReadOnlyList<Vec2> Points, ArtLineKind Kind, int? ProgressionNumber);

public sealed record ArtBlock(Vec2 Anchor, BlockType Type);

/// <summary>Render-agnostic play diagram: world-coordinate routes, lanes, and block ticks.</summary>
public sealed class PlayArtModel
{
    public List<ArtRoute> Routes { get; } = [];
    public List<ArtBlock> Blocks { get; } = [];
}

/// <summary>Draws a play the way a coach would: crisp routes with arrowheads,
/// numbered progression, and blocking "T" ticks.</summary>
public static class PlayArtRenderer
{
    public static PlayArtModel BuildFromDesign(PlayDesign play, float losY)
    {
        var formation = Formations.ByName(play.FormationName);
        var snap = new Vec2(Field.CenterX, losY);
        var model = new PlayArtModel();

        Vec2? SlotPos(string slotId)
        {
            foreach (var slot in formation.Slots)
            {
                if (slot.SlotId == slotId)
                {
                    return snap + slot.Offset;
                }
            }

            return null;
        }

        var number = 1;
        foreach (var route in play.Routes)
        {
            if (SlotPos(route.SlotId) is not { } origin)
            {
                continue;
            }

            var points = new List<Vec2> { origin };
            points.AddRange(route.Waypoints.Select(w => origin + w));
            model.Routes.Add(new ArtRoute(points, ArtLineKind.Route,
                play.Kind == PlayKind.Pass ? number : null));
            number++;
        }

        if (play.RunLane is { Count: > 0 } lane && play.BallCarrierSlotId != null
            && SlotPos(play.BallCarrierSlotId) is { } carrierOrigin)
        {
            var points = new List<Vec2> { carrierOrigin };
            points.AddRange(lane.Select(w => carrierOrigin + w));
            model.Routes.Add(new ArtRoute(points, ArtLineKind.CarrierLane, null));
        }

        foreach (var block in play.Blocking)
        {
            if (SlotPos(block.SlotId) is { } anchor)
            {
                model.Blocks.Add(new ArtBlock(anchor, block.Type));
            }
        }

        return model;
    }

    public static void Draw(DrawingContext context, FieldGeometry g, PlayArtModel model, double opacity)
    {
        var routeColor = Color.FromArgb((byte)(230 * opacity), 255, 235, 100);
        var laneColor = Color.FromArgb((byte)(240 * opacity), 255, 190, 40);
        var blockColor = Color.FromArgb((byte)(200 * opacity), 235, 235, 235);
        var routePen = new Pen(new SolidColorBrush(routeColor), 2);
        var lanePen = new Pen(new SolidColorBrush(laneColor), 3);
        var blockPen = new Pen(new SolidColorBrush(blockColor), 2);

        foreach (var route in model.Routes)
        {
            var pen = route.Kind == ArtLineKind.CarrierLane ? lanePen : routePen;
            for (var i = 1; i < route.Points.Count; i++)
            {
                context.DrawLine(pen, g.ToScreen(route.Points[i - 1]), g.ToScreen(route.Points[i]));
            }

            if (route.Points.Count >= 2)
            {
                DrawArrowhead(context, g, route.Points[^2], route.Points[^1],
                    ((SolidColorBrush)pen.Brush!).Color);
            }

            if (route.ProgressionNumber is { } n)
            {
                DrawProgressionNumber(context, g, route.Points[^1], n, opacity);
            }
        }

        foreach (var block in model.Blocks)
        {
            DrawBlockTick(context, g, block, blockPen);
        }
    }

    private static void DrawArrowhead(DrawingContext context, FieldGeometry g, Vec2 from, Vec2 to, Color color)
    {
        var dir = (to - from).Normalized;
        if (dir == Vec2.Zero)
        {
            return;
        }

        var right = new Vec2(dir.Y, -dir.X);
        const float size = 1.1f;
        var tip = to + dir * (size * 0.4f);
        var baseA = to - dir * size + right * (size * 0.6f);
        var baseB = to - dir * size - right * (size * 0.6f);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(g.ToScreen(tip), isFilled: true);
            ctx.LineTo(g.ToScreen(baseA));
            ctx.LineTo(g.ToScreen(baseB));
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }

    /// <summary>The classic play-diagram blocking symbol: a stem ending in a perpendicular "T".</summary>
    private static void DrawBlockTick(DrawingContext context, FieldGeometry g, ArtBlock block, IPen pen)
    {
        var dir = block.Type switch
        {
            BlockType.PassProtect => new Vec2(0f, -0.9f),
            BlockType.RunBlockZoneLeft => new Vec2(-0.8f, 0.8f),
            BlockType.RunBlockZoneRight => new Vec2(0.8f, 0.8f),
            BlockType.PullLeft => new Vec2(-1.1f, 0f),
            BlockType.PullRight => new Vec2(1.1f, 0f),
            _ => new Vec2(0f, 1.1f),
        };

        var start = block.Anchor;
        var end = block.Anchor + dir * 1.4f;
        context.DrawLine(pen, g.ToScreen(start), g.ToScreen(end));

        var n = dir.Normalized;
        var perp = new Vec2(n.Y, -n.X) * 0.7f;
        context.DrawLine(pen, g.ToScreen(end + perp), g.ToScreen(end - perp));
    }

    private static void DrawProgressionNumber(DrawingContext context, FieldGeometry g, Vec2 at, int number,
        double opacity)
    {
        var center = g.ToScreen(at + new Vec2(1.6f, 0.6f));
        var radius = Math.Max(7.0, g.Scale * 0.75);
        var bg = new SolidColorBrush(Color.FromArgb((byte)(210 * opacity), 20, 20, 20));
        context.DrawEllipse(bg, null, center, radius, radius);

        var text = new FormattedText(number.ToString(),
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Inter", weight: FontWeight.Bold), radius * 1.2,
            new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 255, 235, 100)));
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }
}
