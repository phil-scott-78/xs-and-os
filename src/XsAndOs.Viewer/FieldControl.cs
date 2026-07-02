using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using XsAndOs.Core;

namespace XsAndOs.Viewer;

/// <summary>
/// Draws the field and one frame of a <see cref="SimResult"/>. The field is drawn
/// horizontally: screen X = field Y (length), screen Y = field X (width).
/// </summary>
public sealed class FieldControl : Control
{
    public static readonly StyledProperty<SimResult?> ResultProperty =
        AvaloniaProperty.Register<FieldControl, SimResult?>(nameof(Result));

    public static readonly StyledProperty<int> CurrentFrameProperty =
        AvaloniaProperty.Register<FieldControl, int>(nameof(CurrentFrame));

    /// <summary>Designed route polylines in field coordinates, for the faint overlay.</summary>
    public static readonly StyledProperty<IReadOnlyList<IReadOnlyList<Vec2>>?> RouteOverlayProperty =
        AvaloniaProperty.Register<FieldControl, IReadOnlyList<IReadOnlyList<Vec2>>?>(nameof(RouteOverlay));

    public SimResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public int CurrentFrame
    {
        get => GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    public IReadOnlyList<IReadOnlyList<Vec2>>? RouteOverlay
    {
        get => GetValue(RouteOverlayProperty);
        set => SetValue(RouteOverlayProperty, value);
    }

    static FieldControl()
    {
        AffectsRender<FieldControl>(ResultProperty, CurrentFrameProperty, RouteOverlayProperty);
    }

    private static readonly IBrush Grass = new SolidColorBrush(Color.FromRgb(28, 108, 46));
    private static readonly IBrush EndZone = new SolidColorBrush(Color.FromRgb(18, 78, 34));
    private static readonly IPen YardLine = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1);
    private static readonly IPen FiveYardLine = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
    private static readonly IPen LosPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 160, 255)), 2);
    private static readonly IBrush OffenseBrush = Brushes.White;
    private static readonly IBrush DefenseBrush = new SolidColorBrush(Color.FromRgb(220, 60, 50));
    private static readonly IPen PlayerOutline = new Pen(Brushes.Black, 1);
    private static readonly IPen CarrierRing = new Pen(Brushes.Gold, 2);
    private static readonly IBrush BallBrush = new SolidColorBrush(Color.FromRgb(130, 70, 20));
    private static readonly IPen RoutePen = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 0)), 1.5);

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;

        // Letterbox the 120 x 53.33 yd field into the control.
        var scale = Math.Min(bounds.Width / Field.Length, bounds.Height / Field.Width);
        var w = Field.Length * scale;
        var h = Field.Width * scale;
        var ox = (bounds.Width - w) / 2;
        var oy = (bounds.Height - h) / 2;

        Point Map(Vec2 fieldPos) => new(ox + fieldPos.Y * scale, oy + (Field.Width - fieldPos.X) * scale);

        context.FillRectangle(Grass, new Rect(ox, oy, w, h));
        context.FillRectangle(EndZone, new Rect(ox, oy, Field.OwnGoalLineY * scale, h));
        context.FillRectangle(EndZone,
            new Rect(ox + Field.TargetGoalLineY * scale, oy, (Field.Length - Field.TargetGoalLineY) * scale, h));

        for (var yard = 10; yard <= 110; yard += 5)
        {
            var pen = yard % 10 == 0 ? YardLine : FiveYardLine;
            var x = ox + yard * scale;
            context.DrawLine(pen, new Point(x, oy), new Point(x, oy + h));
        }

        var result = Result;
        if (result != null)
        {
            var losX = ox + result.LosY * scale;
            context.DrawLine(LosPen, new Point(losX, oy), new Point(losX, oy + h));
        }

        if (RouteOverlay is { } routes)
        {
            foreach (var route in routes)
            {
                for (var i = 1; i < route.Count; i++)
                {
                    context.DrawLine(RoutePen, Map(route[i - 1]), Map(route[i]));
                }
            }
        }

        if (result == null || result.Frames.Count == 0)
        {
            return;
        }

        var frame = result.Frames[Math.Clamp(CurrentFrame, 0, result.Frames.Count - 1)];
        var radius = Math.Max(3.0, 0.55 * scale);

        for (var i = 0; i < frame.Players.Length; i++)
        {
            var pos = Map(frame.Players[i].Pos);
            var brush = i < 11 ? OffenseBrush : DefenseBrush;
            context.DrawEllipse(brush, PlayerOutline, pos, radius, radius);
            if (i == frame.Ball.CarrierIndex)
            {
                context.DrawEllipse(null, CarrierRing, pos, radius + 2, radius + 2);
            }
        }

        var ballPos = Map(frame.Ball.Pos);
        var ballR = Math.Max(2.0, 0.3 * scale);
        context.DrawEllipse(BallBrush, PlayerOutline, ballPos, ballR, ballR * 0.7);
    }
}
