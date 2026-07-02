using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using XsAndOs.Core;
using XsAndOs.Playbook;

namespace XsAndOs.Viewer;

/// <summary>
/// The sandbox chalkboard: draw routes with clicks (live preview), drag waypoints,
/// cycle blocking badges. Draw order = QB progression. Owns all pointer/keyboard
/// editing over a <see cref="PlayDraft"/>.
/// </summary>
public sealed class ChalkboardControl : Control
{
    public PlayDraft? Draft { get; set; }
    public float LosY { get; set; } = 50f;

    /// <summary>Raised whenever the draft content changes (for status text).</summary>
    public event Action? DraftChanged;

    private enum EditState
    {
        Idle,
        DrawingRoute,
        DraggingWaypoint,
    }

    private EditState _state = EditState.Idle;
    private string? _selectedSlot;
    private string? _activeSlot;
    private readonly List<Vec2> _pendingWps = [];
    private List<Vec2>? _routeBackup;
    private Vec2? _hover;
    private string? _dragSlot;
    private int _dragIndex = -1;

    // Hit caches, recorded during Render.
    private readonly Dictionary<string, Point> _dotCenters = [];
    private readonly Dictionary<string, Rect> _badgeRects = [];
    private readonly List<(string Slot, int Index, Point P)> _wpHandles = [];

    private FieldGeometry _geometry;

    public ChalkboardControl()
    {
        Focusable = true;
    }

    public void Refresh() => InvalidateVisual();

    /// <summary>Abandon any in-progress drawing (used when the draft is reset).</summary>
    public void CancelEditing()
    {
        _state = EditState.Idle;
        _selectedSlot = null;
        _activeSlot = null;
        _pendingWps.Clear();
        _routeBackup = null;
        InvalidateVisual();
    }

    // --- Rendering ---

    private static readonly IBrush OffenseBrush = Brushes.White;
    private static readonly IPen PlayerOutline = new Pen(Brushes.Black, 1);
    private static readonly IPen SelectedRing = new Pen(Brushes.Gold, 2.5);
    private static readonly IPen CarrierRing = new Pen(new SolidColorBrush(Color.FromRgb(255, 190, 40)), 2);
    private static readonly IPen PendingPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 235, 100)), 2);
    private static readonly IPen PreviewPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 235, 100)), 2)
    {
        DashStyle = new DashStyle([4, 3], 0),
    };
    private static readonly IBrush HandleBrush = Brushes.Gold;
    private static readonly IBrush BadgeBrush = new SolidColorBrush(Color.FromArgb(210, 25, 25, 25));
    private static readonly IBrush BadgeText = new SolidColorBrush(Color.FromRgb(235, 235, 235));

    public override void Render(DrawingContext context)
    {
        // Frame the drawing area: from just behind the formation to deep downfield.
        _geometry = new FieldGeometry(Bounds, new Vec2(Field.CenterX, LosY + 8f), 1.35);
        FieldSurface.Draw(context, _geometry, LosY);

        _dotCenters.Clear();
        _badgeRects.Clear();
        _wpHandles.Clear();

        var draft = Draft;
        if (draft == null)
        {
            return;
        }

        PlayArtRenderer.Draw(context, _geometry, BuildArt(draft), opacity: 1.0);
        DrawPending(context);
        DrawPlayers(context, draft);
        DrawHandles(context, draft);
    }

    private PlayArtModel BuildArt(PlayDraft draft)
    {
        var model = new PlayArtModel();
        var number = 1;
        foreach (var slotId in draft.RouteOrder)
        {
            if (!draft.Routes.TryGetValue(slotId, out var wps) || wps.Count == 0)
            {
                continue;
            }

            var isCarrier = slotId == draft.BallCarrierSlotId;
            var points = new List<Vec2> { draft.SlotWorldPos(slotId, LosY) };
            points.AddRange(wps);
            model.Routes.Add(new ArtRoute(points,
                isCarrier ? ArtLineKind.CarrierLane : ArtLineKind.Route,
                draft.Kind == PlayKind.Pass ? number : null));
            number++;
        }

        foreach (var (slotId, block) in draft.Blocks)
        {
            if (block is { } type && !draft.Routes.ContainsKey(slotId))
            {
                model.Blocks.Add(new ArtBlock(draft.SlotWorldPos(slotId, LosY), type));
            }
        }

        return model;
    }

    private void DrawPending(DrawingContext context)
    {
        if (_state != EditState.DrawingRoute || _activeSlot == null || Draft == null)
        {
            return;
        }

        var prev = _geometry.ToScreen(Draft.SlotWorldPos(_activeSlot, LosY));
        foreach (var wp in _pendingWps)
        {
            var p = _geometry.ToScreen(wp);
            context.DrawLine(PendingPen, prev, p);
            prev = p;
        }

        if (_hover is { } hover)
        {
            context.DrawLine(PreviewPen, prev, _geometry.ToScreen(hover));
        }
    }

    private void DrawPlayers(DrawingContext context, PlayDraft draft)
    {
        var radius = Math.Max(5.0, 0.55 * _geometry.Scale);
        foreach (var slot in draft.Formation.Slots)
        {
            var world = draft.SlotWorldPos(slot.SlotId, LosY);
            var center = _geometry.ToScreen(world);
            _dotCenters[slot.SlotId] = center;

            context.DrawEllipse(OffenseBrush, PlayerOutline, center, radius, radius);
            if (slot.SlotId == _selectedSlot || slot.SlotId == _activeSlot)
            {
                context.DrawEllipse(null, SelectedRing, center, radius + 2.5, radius + 2.5);
            }

            if (slot.SlotId == draft.BallCarrierSlotId)
            {
                context.DrawEllipse(null, CarrierRing, center, radius + 5, radius + 5);
            }

            DrawText(context, ShortSlotLabel(slot), center, Math.Max(8.0, radius * 0.95), Brushes.Black);

            // Blocking badge under non-routed, non-QB players.
            if (slot.Position != PlayerPosition.QB && !draft.Routes.ContainsKey(slot.SlotId))
            {
                var code = BadgeCode(draft.Blocks.TryGetValue(slot.SlotId, out var b) ? b : null);
                var w = Math.Max(24.0, radius * 2.4);
                var h = Math.Max(14.0, radius * 1.3);
                var rect = new Rect(center.X - w / 2, center.Y + radius + 3, w, h);
                _badgeRects[slot.SlotId] = rect;
                context.DrawRectangle(BadgeBrush, null, rect, 3, 3);
                DrawText(context, code, rect.Center, h * 0.62, BadgeText);
            }
        }
    }

    private void DrawHandles(DrawingContext context, PlayDraft draft)
    {
        if (_selectedSlot == null || !draft.Routes.TryGetValue(_selectedSlot, out var wps))
        {
            return;
        }

        for (var i = 0; i < wps.Count; i++)
        {
            var p = _geometry.ToScreen(wps[i]);
            _wpHandles.Add((_selectedSlot, i, p));
            context.DrawRectangle(HandleBrush, PlayerOutline, new Rect(p.X - 4, p.Y - 4, 8, 8));
        }
    }

    private static string ShortSlotLabel(FormationSlot slot) => slot.Position switch
    {
        PlayerPosition.QB => "QB",
        PlayerPosition.RB => "RB",
        PlayerPosition.TE => "TE",
        PlayerPosition.WR => "W" + slot.SlotId[^1],
        _ => slot.SlotId,
    };

    private static string BadgeCode(BlockType? type) => type switch
    {
        BlockType.PassProtect => "PP",
        BlockType.RunBlockZoneLeft => "ZL",
        BlockType.RunBlockZoneRight => "ZR",
        BlockType.PullLeft => "PL",
        BlockType.PullRight => "PR",
        BlockType.LeadBlock => "LD",
        _ => "—",
    };

    private readonly Dictionary<(string, int), FormattedText> _textCache = [];

    private void DrawText(DrawingContext context, string text, Point center, double size, IBrush brush)
    {
        var key = (text, (int)size);
        if (!_textCache.TryGetValue(key, out var ft))
        {
            ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Inter", weight: FontWeight.SemiBold), size, brush);
            _textCache[key] = ft;
        }

        context.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }

    // --- Input ---

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var draft = Draft;
        if (draft == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var pos = point.Position;

        if (point.Properties.IsRightButtonPressed)
        {
            OnRightClick(draft, pos);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_state == EditState.DrawingRoute)
        {
            var field = ClampToDrawable(_geometry.ToField(pos));
            _pendingWps.Add(field);
            if (e.ClickCount >= 2)
            {
                // Double-click: the point just placed is the route's end.
                CommitPending(draft);
            }

            InvalidateVisual();
            return;
        }

        // Idle: handles, badges, then dots.
        foreach (var (slot, idx, p) in _wpHandles)
        {
            if (Distance(p, pos) <= 7)
            {
                _state = EditState.DraggingWaypoint;
                _dragSlot = slot;
                _dragIndex = idx;
                return;
            }
        }

        foreach (var (slot, rect) in _badgeRects)
        {
            if (rect.Contains(pos))
            {
                draft.CycleBlock(slot);
                NotifyChanged();
                return;
            }
        }

        var hitSlot = HitTestDot(pos);
        if (hitSlot != null)
        {
            OnPlayerClicked(draft, hitSlot, e.ClickCount);
            return;
        }

        // Empty field: deselect.
        _selectedSlot = null;
        InvalidateVisual();
    }

    private void OnPlayerClicked(PlayDraft draft, string slotId, int clickCount)
    {
        var slot = draft.Formation.Slots.First(s => s.SlotId == slotId);
        if (slot.Position == PlayerPosition.QB)
        {
            return;
        }

        if (!draft.CanRunRoute(slotId))
        {
            // Linemen: clicking the dot cycles the assignment, same as the badge.
            draft.CycleBlock(slotId);
            NotifyChanged();
            return;
        }

        if (draft.Routes.ContainsKey(slotId))
        {
            if (clickCount >= 2)
            {
                // Double-click a routed player: redraw from scratch.
                StartDrawing(draft, slotId, backupExisting: true);
            }
            else
            {
                _selectedSlot = slotId;
            }

            InvalidateVisual();
            return;
        }

        StartDrawing(draft, slotId, backupExisting: false);
        InvalidateVisual();
    }

    private void StartDrawing(PlayDraft draft, string slotId, bool backupExisting)
    {
        _routeBackup = backupExisting && draft.Routes.TryGetValue(slotId, out var existing)
            ? [.. existing]
            : null;
        if (backupExisting)
        {
            draft.ClearRoute(slotId);
        }

        _state = EditState.DrawingRoute;
        _activeSlot = slotId;
        _selectedSlot = slotId;
        _pendingWps.Clear();
    }

    private void OnRightClick(PlayDraft draft, Point pos)
    {
        if (_state == EditState.DrawingRoute)
        {
            // Step back one waypoint; none left = cancel.
            if (_pendingWps.Count > 0)
            {
                _pendingWps.RemoveAt(_pendingWps.Count - 1);
            }
            else
            {
                CancelDrawing(draft);
            }

            InvalidateVisual();
            return;
        }

        var hitSlot = HitTestDot(pos);
        if (hitSlot != null && draft.Routes.ContainsKey(hitSlot))
        {
            draft.ClearRoute(hitSlot);
            NotifyChanged();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;

        if (_state == EditState.DrawingRoute)
        {
            _hover = ClampToDrawable(_geometry.ToField(pos));
            InvalidateVisual();
        }
        else if (_state == EditState.DraggingWaypoint && _dragSlot != null && Draft != null
                 && Draft.Routes.TryGetValue(_dragSlot, out var wps) && _dragIndex < wps.Count)
        {
            wps[_dragIndex] = ClampToDrawable(_geometry.ToField(pos));
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_state == EditState.DraggingWaypoint)
        {
            _state = EditState.Idle;
            _dragSlot = null;
            _dragIndex = -1;
            NotifyChanged();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var draft = Draft;
        if (draft == null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter when _state == EditState.DrawingRoute:
                CommitPending(draft);
                e.Handled = true;
                break;

            case Key.Escape when _state == EditState.DrawingRoute:
                CancelDrawing(draft);
                e.Handled = true;
                break;

            case Key.Escape:
                _selectedSlot = null;
                InvalidateVisual();
                break;

            case Key.Delete or Key.Back when _selectedSlot != null && _state == EditState.Idle:
                draft.ClearRoute(_selectedSlot);
                NotifyChanged();
                e.Handled = true;
                break;
        }
    }

    private void CommitPending(PlayDraft draft)
    {
        if (_activeSlot != null && _pendingWps.Count > 0)
        {
            draft.SetRoute(_activeSlot, [.. _pendingWps]);
        }
        else if (_activeSlot != null && _routeBackup != null)
        {
            draft.SetRoute(_activeSlot, _routeBackup);
        }

        _state = EditState.Idle;
        _activeSlot = null;
        _pendingWps.Clear();
        _routeBackup = null;
        _hover = null;
        NotifyChanged();
    }

    private void CancelDrawing(PlayDraft draft)
    {
        if (_activeSlot != null && _routeBackup != null)
        {
            draft.SetRoute(_activeSlot, _routeBackup);
        }

        _state = EditState.Idle;
        _activeSlot = null;
        _pendingWps.Clear();
        _routeBackup = null;
        _hover = null;
        NotifyChanged();
    }

    private string? HitTestDot(Point pos)
    {
        string? best = null;
        var bestDist = 14.0;
        foreach (var (slot, center) in _dotCenters)
        {
            var d = Distance(center, pos);
            if (d < bestDist)
            {
                best = slot;
                bestDist = d;
            }
        }

        return best;
    }

    /// <summary>Waypoints stay on the field and out of the offensive backfield's dead zone.</summary>
    private Vec2 ClampToDrawable(Vec2 field) => new(
        Math.Clamp(field.X, 0.5f, Field.Width - 0.5f),
        Math.Clamp(field.Y, LosY - 9f, Field.Length - 0.5f));

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void NotifyChanged()
    {
        InvalidateVisual();
        DraftChanged?.Invoke();
    }
}
