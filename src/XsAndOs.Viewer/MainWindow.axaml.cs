using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XsAndOs.Core;
using XsAndOs.Playbook;

namespace XsAndOs.Viewer;

public partial class MainWindow : Window
{
    private readonly List<PlayDesign> _plays = [];
    private readonly DispatcherTimer _timer;
    private SimResult? _sim;
    private double _frame;
    private bool _playing;
    private bool _seeking;
    private readonly Random _seedRandom = new();

    private static readonly (string Label, double Factor)[] Speeds =
    [
        ("0.25x", 0.25), ("0.5x", 0.5), ("1x", 1.0), ("2x", 2.0),
    ];

    public MainWindow()
    {
        InitializeComponent();

        LoadPlays();
        PlayBox.ItemsSource = _plays.Select(p => $"{p.Name} ({p.FormationName}, {p.Kind})").ToList();
        PlayBox.SelectedIndex = 0;
        DefenseBox.ItemsSource = DefensiveCall.All.Select(c => c.ToString()).ToList();
        DefenseBox.SelectedIndex = 0;
        SpeedBox.ItemsSource = Speeds.Select(s => s.Label).ToList();
        SpeedBox.SelectedIndex = 2;

        // 60 fps playback stepping through 60 Hz sim frames: one frame per tick at 1x.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16.6) };
        _timer.Tick += (_, _) => Advance();
    }

    private void LoadPlays()
    {
        _plays.AddRange(SamplePlays.All);

        // Any extra drawn plays dropped into samples/plays alongside the built-ins.
        var dir = FindSamplesDir();
        if (dir == null)
        {
            return;
        }

        try
        {
            foreach (var play in PlaybookSerializer.LoadDirectory(dir))
            {
                if (_plays.All(p => !string.Equals(p.Name, play.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _plays.Add(play);
                }
            }
        }
        catch
        {
            // A malformed play file shouldn't stop the viewer from opening.
        }
    }

    private static string? FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "plays");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private void OnRandomizeSeed(object? sender, RoutedEventArgs e)
    {
        SeedBox.Text = _seedRandom.Next(1, 1_000_000).ToString();
    }

    private void OnRunSim(object? sender, RoutedEventArgs e)
    {
        if (PlayBox.SelectedIndex < 0 || DefenseBox.SelectedIndex < 0)
        {
            return;
        }

        if (!int.TryParse(SeedBox.Text, out var seed))
        {
            ResultText.Text = "Seed must be an integer.";
            return;
        }

        var play = _plays[PlayBox.SelectedIndex];
        var defense = DefensiveCall.All[DefenseBox.SelectedIndex];

        _sim = Sim.Run(play, defense, seed);
        FieldView.Result = _sim;
        FieldView.RouteOverlay = BuildRouteOverlay(play, _sim.LosY);

        var r = _sim.Result;
        ResultText.Text = $"{r.Outcome}: {r.YardsGained:+0.0;-0.0;0.0} yards in {r.Duration:0.00}s";

        EventList.ItemsSource = _sim.Events
            .Where(ev => ev.Type != PlayEventType.PlayDead)
            .Select(ev => $"{ev.Time,5:0.00}s  {Describe(_sim, ev)}")
            .ToList();

        TimeSlider.Maximum = Math.Max(1, _sim.Frames.Count - 1);
        SetFrame(0);
        SetPlaying(true);
    }

    private static string Describe(SimResult sim, PlayEvent ev)
    {
        string Name(int idx) => idx >= 0 && idx < sim.Participants.Count ? sim.Participants[idx].Name : "?";
        return ev.Type switch
        {
            PlayEventType.ProgressionRead => $"read → {Name(ev.TargetIndex)}",
            PlayEventType.ThrowStart => $"THROW → {Name(ev.TargetIndex)}",
            PlayEventType.Catch => $"CATCH {Name(ev.ActorIndex)}",
            PlayEventType.Handoff => $"HANDOFF → {Name(ev.TargetIndex)}",
            PlayEventType.BlockShed => $"{Name(ev.ActorIndex)} sheds block",
            PlayEventType.BrokenTackle => $"{Name(ev.ActorIndex)} breaks tackle",
            PlayEventType.Tackle => $"TACKLE by {Name(ev.ActorIndex)}",
            PlayEventType.Sack => $"SACK by {Name(ev.ActorIndex)}",
            PlayEventType.Interception => $"INTERCEPTED by {Name(ev.ActorIndex)}",
            PlayEventType.Incomplete => "INCOMPLETE",
            PlayEventType.OutOfBounds => $"{Name(ev.ActorIndex)} out of bounds",
            PlayEventType.Scramble => $"{Name(ev.ActorIndex)} escapes the pocket",
            PlayEventType.ThrowAway => "THROWN AWAY",
            PlayEventType.Touchdown => $"TOUCHDOWN {Name(ev.ActorIndex)}",
            _ => ev.Type.ToString(),
        };
    }

    /// <summary>World-space polylines of the designed routes and run lane.</summary>
    private static List<IReadOnlyList<Vec2>> BuildRouteOverlay(PlayDesign play, float losY)
    {
        var formation = Formations.ByName(play.FormationName);
        var snap = new Vec2(Field.CenterX, losY);
        var overlay = new List<IReadOnlyList<Vec2>>();

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

        foreach (var route in play.Routes)
        {
            if (SlotPos(route.SlotId) is not { } origin)
            {
                continue;
            }

            var points = new List<Vec2> { origin };
            points.AddRange(route.Waypoints.Select(w => origin + w));
            overlay.Add(points);
        }

        if (play.RunLane is { Count: > 0 } lane && play.BallCarrierSlotId != null
            && SlotPos(play.BallCarrierSlotId) is { } carrierOrigin)
        {
            var points = new List<Vec2> { carrierOrigin };
            points.AddRange(lane.Select(w => carrierOrigin + w));
            overlay.Add(points);
        }

        return overlay;
    }

    private void Advance()
    {
        if (_sim == null || !_playing)
        {
            return;
        }

        var speed = Speeds[Math.Max(0, SpeedBox.SelectedIndex)].Factor;
        var next = _frame + speed;
        if (next >= _sim.Frames.Count - 1)
        {
            next = _sim.Frames.Count - 1;
            SetPlaying(false);
        }

        SetFrame(next);
    }

    private void SetFrame(double frame)
    {
        _frame = frame;
        var idx = (int)frame;
        FieldView.CurrentFrame = idx;

        _seeking = true;
        TimeSlider.Value = frame;
        _seeking = false;

        if (_sim != null && idx < _sim.Frames.Count)
        {
            TimeText.Text = $"{_sim.Frames[idx].Time:0.00}s";
        }
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        PlayPauseButton.Content = playing ? "Pause" : "Play";
        if (playing)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_sim == null)
        {
            return;
        }

        // Replay from the start when hitting Play at the end.
        if (!_playing && _frame >= _sim.Frames.Count - 1)
        {
            SetFrame(0);
        }

        SetPlaying(!_playing);
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_seeking || _sim == null)
        {
            return;
        }

        SetPlaying(false);
        SetFrame(e.NewValue);
    }

    private void OnEventSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_sim == null || EventList.SelectedIndex < 0)
        {
            return;
        }

        var events = _sim.Events.Where(ev => ev.Type != PlayEventType.PlayDead).ToList();
        if (EventList.SelectedIndex < events.Count)
        {
            SetPlaying(false);
            var tick = events[EventList.SelectedIndex].Tick;
            var frames = _sim.Frames;
            for (var i = 0; i < frames.Count; i++)
            {
                if (frames[i].Tick >= tick)
                {
                    SetFrame(i);
                    break;
                }
            }
        }
    }
}
