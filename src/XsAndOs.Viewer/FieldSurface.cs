using Avalonia;
using Avalonia.Media;
using XsAndOs.Core;

namespace XsAndOs.Viewer;

/// <summary>Draws the grass, yard lines, end zones, and LOS. Shared by replay and chalkboard.</summary>
public static class FieldSurface
{
    private static readonly IBrush Grass = new SolidColorBrush(Color.FromRgb(28, 108, 46));
    private static readonly IBrush EndZone = new SolidColorBrush(Color.FromRgb(18, 78, 34));
    private static readonly IPen YardLine = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1);
    private static readonly IPen FiveYardLine = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
    private static readonly IPen LosPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 160, 255)), 2);

    public static void Draw(DrawingContext context, FieldGeometry g, float? losY)
    {
        var topLeft = g.ToScreen(new Vec2(Field.Width, 0f));
        var bottomRight = g.ToScreen(new Vec2(0f, Field.Length));
        context.FillRectangle(Grass, new Rect(topLeft, bottomRight));

        var ownGoal = g.ToScreen(new Vec2(0f, Field.OwnGoalLineY));
        context.FillRectangle(EndZone, new Rect(topLeft, ownGoal));
        var targetGoal = g.ToScreen(new Vec2(Field.Width, Field.TargetGoalLineY));
        context.FillRectangle(EndZone, new Rect(targetGoal, bottomRight));

        for (var yard = 10; yard <= 110; yard += 5)
        {
            var pen = yard % 10 == 0 ? YardLine : FiveYardLine;
            context.DrawLine(pen,
                g.ToScreen(new Vec2(Field.Width, yard)),
                g.ToScreen(new Vec2(0f, yard)));
        }

        if (losY is { } los)
        {
            context.DrawLine(LosPen,
                g.ToScreen(new Vec2(Field.Width, los)),
                g.ToScreen(new Vec2(0f, los)));
        }
    }
}
