using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using XsAndOs.Core;

namespace XsAndOs.Viewer;

/// <summary>
/// Replays a <see cref="SimResult"/>: interpolated player motion, ball trail,
/// position labels, play-art overlay, and a soft follow camera.
/// </summary>
public sealed class FieldControl : Control
{
    public static readonly StyledProperty<SimResult?> ResultProperty =
        AvaloniaProperty.Register<FieldControl, SimResult?>(nameof(Result));

    /// <summary>Fractional frame index; the renderer lerps between sim frames.</summary>
    public static readonly StyledProperty<double> CurrentFrameProperty =
        AvaloniaProperty.Register<FieldControl, double>(nameof(CurrentFrame));

    public static readonly StyledProperty<PlayArtModel?> ArtProperty =
        AvaloniaProperty.Register<FieldControl, PlayArtModel?>(nameof(Art));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<FieldControl, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<bool> FollowCameraProperty =
        AvaloniaProperty.Register<FieldControl, bool>(nameof(FollowCamera), defaultValue: true);

    public SimResult? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public double CurrentFrame
    {
        get => GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    public PlayArtModel? Art
    {
        get => GetValue(ArtProperty);
        set => SetValue(ArtProperty, value);
    }

    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public bool FollowCamera
    {
        get => GetValue(FollowCameraProperty);
        set => SetValue(FollowCameraProperty, value);
    }

    static FieldControl()
    {
        AffectsRender<FieldControl>(ResultProperty, CurrentFrameProperty, ArtProperty,
            LabelsProperty, FollowCameraProperty);
    }

    private static readonly IBrush OffenseBrush = Brushes.White;
    private static readonly IBrush DefenseBrush = new SolidColorBrush(Color.FromRgb(220, 60, 50));
    private static readonly IPen PlayerOutline = new Pen(Brushes.Black, 1);
    private static readonly IPen CarrierRing = new Pen(Brushes.Gold, 2);
    private static readonly IBrush BallBrush = new SolidColorBrush(Color.FromRgb(130, 70, 20));
    private static readonly IBrush OffenseLabelBrush = Brushes.Black;
    private static readonly IBrush DefenseLabelBrush = Brushes.White;

    // --- Camera state (smoothed between renders) ---
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastRenderSeconds;
    private Vec2 _camCenter = new(Field.CenterX, Field.Length / 2f);
    private double _camZoom = 1.0;
    private SimResult? _lastResult;
    private double _lastFrame;

    private const double FollowZoom = 2.1;
    private const int TrailFrames = 45;
    private const int TrailStep = 3;

    public override void Render(DrawingContext context)
    {
        var result = Result;
        var geometry = ComputeCamera(result);

        FieldSurface.Draw(context, geometry, result?.LosY);

        if (Art is { } art)
        {
            PlayArtRenderer.Draw(context, geometry, art, opacity: 0.35);
        }

        if (result == null || result.Frames.Count == 0)
        {
            return;
        }

        var frame = CurrentFrame;
        var i = Math.Clamp((int)frame, 0, result.Frames.Count - 1);
        var next = Math.Min(i + 1, result.Frames.Count - 1);
        var t = (float)Math.Clamp(frame - i, 0, 1);
        var a = result.Frames[i];
        var b = result.Frames[next];

        DrawTrail(context, geometry, result, i);

        var radius = Math.Max(3.0, 0.55 * geometry.Scale);
        var labels = Labels;
        for (var p = 0; p < a.Players.Length; p++)
        {
            var pos = Vec2.Lerp(a.Players[p].Pos, b.Players[p].Pos, t);
            var screen = geometry.ToScreen(pos);
            var isOffense = p < 11;
            context.DrawEllipse(isOffense ? OffenseBrush : DefenseBrush, PlayerOutline,
                screen, radius, radius);

            if (p == a.Ball.CarrierIndex)
            {
                context.DrawEllipse(null, CarrierRing, screen, radius + 2, radius + 2);
            }

            if (radius >= 7 && labels != null && p < labels.Count)
            {
                DrawLabel(context, labels[p], screen, radius,
                    isOffense ? OffenseLabelBrush : DefenseLabelBrush);
            }
        }

        var ballPos = Vec2.Lerp(a.Ball.Pos, b.Ball.Pos, t);
        var ballScreen = geometry.ToScreen(ballPos);
        var ballR = Math.Max(2.0, 0.3 * geometry.Scale);
        context.DrawEllipse(BallBrush, PlayerOutline, ballScreen, ballR, ballR * 0.7);
    }

    private FieldGeometry ComputeCamera(SimResult? result)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastRenderSeconds, 0.0, 0.1);
        _lastRenderSeconds = now;

        var follow = FollowCamera && result is { Frames.Count: > 0 };
        var targetZoom = follow ? FollowZoom : 1.0;
        var targetCenter = new Vec2(Field.CenterX, Field.Length / 2f);
        if (follow)
        {
            var idx = Math.Clamp((int)CurrentFrame, 0, result!.Frames.Count - 1);
            targetCenter = result.Frames[idx].Ball.Pos;
        }

        // Snap instead of glide on a new sim or a timeline jump (seek/scrub).
        var jumped = !ReferenceEquals(result, _lastResult)
            || Math.Abs(CurrentFrame - _lastFrame) > Tuning.TicksPerSecond * 1.5;
        _lastResult = result;
        _lastFrame = CurrentFrame;

        if (jumped)
        {
            _camCenter = targetCenter;
            _camZoom = targetZoom;
        }
        else
        {
            var alpha = (float)(1 - Math.Exp(-3.0 * dt));
            _camCenter = Vec2.Lerp(_camCenter, targetCenter, alpha);
            _camZoom += (targetZoom - _camZoom) * alpha;
        }

        return new FieldGeometry(Bounds, _camCenter, _camZoom);
    }

    /// <summary>Fading breadcrumb of recent ball positions — trails the carrier or the flight.</summary>
    private void DrawTrail(DrawingContext context, FieldGeometry g, SimResult result, int frameIdx)
    {
        for (var back = TrailFrames; back > 0; back -= TrailStep)
        {
            var j = frameIdx - back;
            if (j < 0)
            {
                continue;
            }

            var ball = result.Frames[j].Ball;
            if (ball.State == BallStateKind.Dead)
            {
                continue;
            }

            var alpha = (byte)(110 * (1.0 - back / (double)TrailFrames));
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 255, 215, 90));
            var r = Math.Max(1.5, 0.16 * g.Scale);
            context.DrawEllipse(brush, null, g.ToScreen(ball.Pos), r, r);
        }
    }

    private readonly Dictionary<(string Label, int SizeBucket), FormattedText> _labelCache = [];

    private void DrawLabel(DrawingContext context, string label, Point center, double radius, IBrush brush)
    {
        var bucket = (int)(radius / 2) * 2;
        if (!_labelCache.TryGetValue((label, bucket), out var text))
        {
            text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Inter", weight: FontWeight.SemiBold), Math.Max(7.0, bucket * 0.75), brush);
            _labelCache[(label, bucket)] = text;
        }

        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }
}
