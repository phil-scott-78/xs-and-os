using Avalonia;
using XsAndOs.Core;

namespace XsAndOs.Viewer;

/// <summary>
/// Field-to-screen transform shared by the replay and chalkboard controls.
/// The field renders horizontally: screen X = field Y (length), screen Y = field X
/// (width, flipped). Supports a camera (center + zoom) whose viewport is clamped
/// to the field, so ToScreen/ToField always agree.
/// </summary>
public readonly struct FieldGeometry
{
    private readonly double _scale;
    private readonly double _ox;
    private readonly double _oy;

    public FieldGeometry(Rect bounds, Vec2 camCenter, double zoom)
    {
        var baseScale = Math.Min(bounds.Width / Field.Length, bounds.Height / Field.Width);
        _scale = Math.Max(0.01, baseScale * zoom);

        // Viewport extent in field units along each axis.
        var viewLen = bounds.Width / _scale;
        var viewWid = bounds.Height / _scale;

        double cy = viewLen >= Field.Length
            ? Field.Length / 2
            : Math.Clamp(camCenter.Y, viewLen / 2, Field.Length - viewLen / 2);
        double cx = viewWid >= Field.Width
            ? Field.Width / 2
            : Math.Clamp(camCenter.X, viewWid / 2, Field.Width - viewWid / 2);

        _ox = bounds.Width / 2 - cy * _scale;
        _oy = bounds.Height / 2 - (Field.Width - cx) * _scale;
    }

    public double Scale => _scale;

    public Point ToScreen(Vec2 field) => new(_ox + field.Y * _scale, _oy + (Field.Width - field.X) * _scale);

    public Vec2 ToField(Point screen) => new(
        (float)(Field.Width - (screen.Y - _oy) / _scale),
        (float)((screen.X - _ox) / _scale));
}
