using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Control
{
    private GameState _state = null!;
    private List<EventData> _events = new();
    private EventData? _currentEvent;
    private int _eventIndex = 0;

    private CardController _card = null!;
    private readonly Dictionary<string, ProgressBar> _bars = new();
    private readonly Dictionary<string, Label> _barValues = new();
    private readonly Dictionary<string, StyleBoxFlat> _barFillStyles = new();
    private readonly Dictionary<string, Label> _attrNameLabels = new();

    private static readonly Color ColGrey     = new(0.40f, 0.40f, 0.40f);
    private static readonly Color ColPositive = new(0.35f, 1.00f, 0.45f);
    private static readonly Color ColNegative = new(1.00f, 0.32f, 0.32f);
    private Control _gameOverScreen = null!;
    private Label _gameOverLabel = null!;

    public override void _Ready()
    {
        _state = GetNode<GameState>("/root/GameState");
        _state.AttributeChanged += OnAttributeChanged;
        _state.GameOver += OnGameOver;

        SetAnchorsPreset(LayoutPreset.FullRect);

        BuildBackground();
        BuildAttributeBars();
        BuildCard();
        BuildGameOverScreen();

        _events = EventLoader.Load("res://events/events.xml");
        Shuffle(_events);

        ShowNextEvent();
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildBackground()
    {
        var bg = new ColorRect
        {
            Color = new Color(0.07f, 0.07f, 0.10f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);
    }

    private void BuildAttributeBars()
    {
        var vp = GetViewportRect().Size;

        var row = new HBoxContainer();
        row.Position = new Vector2(16f, 12f);
        row.Size = new Vector2(vp.X - 32f, 80f);
        row.AddThemeConstantOverride("separation", 16);
        AddChild(row);

        var cfg = new (string key, Color color)[]
        {
            ("wellbeing",  new Color(0.35f, 0.85f, 0.50f)),
            ("morale",     new Color(0.40f, 0.65f, 1.00f)),
            ("prosperity", new Color(1.00f, 0.80f, 0.25f)),
            ("codebase",   new Color(0.80f, 0.40f, 1.00f)),
        };

        foreach (var (key, color) in cfg)
        {
            var col = new VBoxContainer();
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var nameLabel = new Label { Text = GameState.DisplayNames[key] };
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            nameLabel.AddThemeColorOverride("font_color", ColGrey);

            var fillStyle = RoundedBox(ColGrey, 4);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 50, ShowPercentage = false };
            bar.CustomMinimumSize = new Vector2(0f, 18f);
            bar.AddThemeStyleboxOverride("fill",       fillStyle);
            bar.AddThemeStyleboxOverride("background", RoundedBox(new Color(0.18f, 0.18f, 0.22f), 4));

            var valLabel = new Label { Text = "50" };
            valLabel.HorizontalAlignment = HorizontalAlignment.Center;
            valLabel.AddThemeFontSizeOverride("font_size", 11);
            valLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.60f, 0.60f));

            col.AddChild(nameLabel);
            col.AddChild(bar);
            col.AddChild(valLabel);
            row.AddChild(col);

            _bars[key]            = bar;
            _barValues[key]       = valLabel;
            _barFillStyles[key]   = fillStyle;
            _attrNameLabels[key]  = nameLabel;
        }
    }

    private void BuildCard()
    {
        var vp    = GetViewportRect().Size;
        float cardW = Mathf.Min(380f, vp.X * 0.80f);
        float cardH = Mathf.Min(520f, vp.Y * 0.64f);
        float cardX = (vp.X - cardW) / 2f;
        float cardY = 100f + (vp.Y - 100f - cardH) / 2.8f;

        _card = new CardController
        {
            Position        = new Vector2(cardX, cardY),
            Size            = new Vector2(cardW, cardH),
            SwipeThreshold  = 130f,
        };
        _card.ChoiceMade          += OnChoiceMade;
        _card.DragDirectionChanged += OnDragDirectionChanged;

        var leftHint  = MakeHintLabel(
            new Vector2(16f, cardY + cardH + 18f),
            new Vector2(vp.X / 2f - 24f, 64f),
            new Color(1.00f, 0.40f, 0.40f),
            HorizontalAlignment.Left);

        var rightHint = MakeHintLabel(
            new Vector2(vp.X / 2f + 8f, cardY + cardH + 18f),
            new Vector2(vp.X / 2f - 24f, 64f),
            new Color(0.40f, 1.00f, 0.55f),
            HorizontalAlignment.Right);

        AddChild(_card);
        AddChild(leftHint);
        AddChild(rightHint);

        _card.LeftHintLabel  = leftHint;
        _card.RightHintLabel = rightHint;
    }

    private void BuildGameOverScreen()
    {
        _gameOverScreen = new Control { Visible = false };
        _gameOverScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        _gameOverScreen.MouseFilter = MouseFilterEnum.Stop;

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.82f) };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);

        var vbox = new VBoxContainer();
        vbox.CustomMinimumSize = new Vector2(420f, 0f);
        vbox.AddThemeConstantOverride("separation", 28);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;

        var heading = new Label { Text = "GAME OVER" };
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.AddThemeFontSizeOverride("font_size", 32);
        heading.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.35f));

        _gameOverLabel = new Label();
        _gameOverLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _gameOverLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _gameOverLabel.AddThemeFontSizeOverride("font_size", 18);
        _gameOverLabel.AddThemeColorOverride("font_color", Colors.White);

        var btn = new Button { Text = "Try Again" };
        btn.CustomMinimumSize = new Vector2(180f, 52f);
        btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        btn.Pressed += OnRestart;

        vbox.AddChild(heading);
        vbox.AddChild(_gameOverLabel);
        vbox.AddChild(btn);
        center.AddChild(vbox);

        _gameOverScreen.AddChild(overlay);
        _gameOverScreen.AddChild(center);
        AddChild(_gameOverScreen);
    }

    // ── Game Logic ───────────────────────────────────────────────────────────

    private void ShowNextEvent()
    {
        if (_events.Count == 0) return;
        _currentEvent = _events[_eventIndex % _events.Count];
        _eventIndex++;
        _card.LoadEvent(_currentEvent);
    }

    private void OnChoiceMade(bool isRight)
    {
        if (_currentEvent == null) return;
        var effects = isRight ? _currentEvent.RightEffects : _currentEvent.LeftEffects;
        _state.Apply(effects);

        if (!_gameOverScreen.Visible)
            ShowNextEvent();
    }

    private void OnDragDirectionChanged(bool active, bool isRight)
    {
        if (!active || _currentEvent == null)
        {
            SetAllAttrColor(ColGrey);
            return;
        }

        var effects = isRight ? _currentEvent.RightEffects : _currentEvent.LeftEffects;
        foreach (var key in GameState.Keys)
        {
            Color c = ColGrey;
            if (effects.TryGetValue(key, out int delta) && delta != 0)
                c = delta > 0 ? ColPositive : ColNegative;
            SetAttrColor(key, c);
        }
    }

    private void SetAllAttrColor(Color c)
    {
        foreach (var key in GameState.Keys)
            SetAttrColor(key, c);
    }

    private void SetAttrColor(string key, Color c)
    {
        if (_barFillStyles.TryGetValue(key, out var style))   style.BgColor = c;
        if (_attrNameLabels.TryGetValue(key, out var lbl))    lbl.AddThemeColorOverride("font_color", c);
    }

    private void OnAttributeChanged(string key, int value)
    {
        if (_bars.TryGetValue(key, out var bar))
        {
            var tw = CreateTween();
            tw.TweenProperty(bar, "value", (double)value, 0.35f)
              .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
        if (_barValues.TryGetValue(key, out var lbl))
            lbl.Text = value.ToString();
    }

    private void OnGameOver(string message, bool isWin)
    {
        _gameOverLabel.Text = message;
        _gameOverScreen.Visible = true;
    }

    private void OnRestart()
    {
        _state.Reset();
        _eventIndex = 0;
        Shuffle(_events);
        _gameOverScreen.Visible = false;

        foreach (var key in GameState.Keys)
            OnAttributeChanged(key, 50);

        ShowNextEvent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Label MakeHintLabel(Vector2 pos, Vector2 size, Color color, HorizontalAlignment align)
    {
        var lbl = new Label();
        lbl.Position = pos;
        lbl.Size = size;
        lbl.HorizontalAlignment = align;
        lbl.AutowrapMode = TextServer.AutowrapMode.Word;
        lbl.AddThemeFontSizeOverride("font_size", 14);
        lbl.AddThemeColorOverride("font_color", color);
        lbl.Modulate = new Color(1f, 1f, 1f, 0f);
        return lbl;
    }

    private static StyleBoxFlat RoundedBox(Color color, int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft    = radius,
            CornerRadiusTopRight   = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };
    }

    private static void Shuffle<T>(List<T> list)
    {
        var rng = new Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
