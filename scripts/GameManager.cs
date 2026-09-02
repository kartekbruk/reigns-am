using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Control
{
	private GameState _state = null!;
	private List<EventData> _events = new();
	private EventData? _currentEvent;

	private CardController _card = null!;
	private readonly Dictionary<string, ProgressBar> _bars = new();
	private readonly Dictionary<string, Label> _barValues = new();
	private readonly Dictionary<string, StyleBoxFlat> _barFillStyles = new();
	private readonly Dictionary<string, Label> _attrNameLabels = new();

	private static readonly Color ColGrey     = new(0.40f, 0.40f, 0.40f);
	private static readonly Color ColPositive = new(0.35f, 1.00f, 0.45f);
	private static readonly Color ColNegative = new(1.00f, 0.32f, 0.32f);
	private Label _cardTitleLabel = null!;
	private Label _cardTextLabel = null!;
	private Control _gameOverScreen = null!;
	private Label _gameOverLabel = null!;
	private Label _sprintLabel = null!;
	private Label _dateLabel = null!;

	public override void _Ready()
	{
		_state = GetNode<GameState>("/root/GameState");
		_state.AttributeChanged += OnAttributeChanged;
		_state.GameOver += OnGameOver;

		SetAnchorsPreset(LayoutPreset.FullRect);

		BuildBackground();
		BuildAttributeBars();
		BuildSprintDisplay();
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

	private void BuildSprintDisplay()
	{
		var vp = GetViewportRect().Size;

		var vbox = new VBoxContainer();
		vbox.Position = new Vector2(0f, 580f);
		vbox.Size = new Vector2(vp.X, 52f);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddThemeConstantOverride("separation", 2);
		vbox.MouseFilter = MouseFilterEnum.Ignore;

		_sprintLabel = new Label();
		_sprintLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sprintLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_sprintLabel.AddThemeFontSizeOverride("font_size", 18);
		_sprintLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.88f));

		_dateLabel = new Label();
		_dateLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_dateLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_dateLabel.AddThemeFontSizeOverride("font_size", 12);
		_dateLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));

		vbox.AddChild(_sprintLabel);
		vbox.AddChild(_dateLabel);
		AddChild(vbox);

		UpdateSprintDisplay();
	}

	private void UpdateSprintDisplay()
	{
		_sprintLabel.Text = $"Sprint {_state.CurrentSprint}";
		_dateLabel.Text = $"{_state.LevelName}  ·  {_state.CurrentDate:MMM yyyy}";
	}

	private void BuildCard()
	{
		var vp    = GetViewportRect().Size;
		const float topUI  = 220f; // bars (12+80) + sprint (96+52) + gap
		const float hintH  =  82f; // 18px gap + 64px hint label

		float cardW   = Mathf.Min(380f, vp.X * 0.90f);
		float availH  = vp.Y - topUI - hintH;
		float cardH   = Mathf.Min(520f, availH);
		float cardX   = (vp.X - cardW) / 2f;
		float cardY   = topUI + (availH - cardH) / 2f;

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

		_cardTextLabel = new Label();
		_cardTextLabel.Position = new Vector2(cardX + 24f, cardY - 200);
		_cardTextLabel.Size = new Vector2(cardW - 48f, cardH - 120f);
		_cardTextLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_cardTextLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_cardTextLabel.VerticalAlignment = VerticalAlignment.Center;
		_cardTextLabel.AddThemeFontSizeOverride("font_size", 16);
		_cardTextLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.92f));
		_cardTextLabel.MouseFilter = MouseFilterEnum.Ignore;

		AddChild(_cardTextLabel);
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

	private List<EventData> GetEventPool()
	{
		string key = _state.LevelKey;
		var filtered = _events.FindAll(e => e.Positions.Count == 0 || e.Positions.Contains(key));
		return filtered.Count > 0 ? filtered : _events;
	}

	private void ShowNextEvent()
	{
		var pool = GetEventPool();
		if (pool.Count == 0) return;
		_currentEvent = pool[(int)(GD.Randi() % (uint)pool.Count)];
		_card.LoadEvent(_currentEvent);
		_cardTextLabel.Text = _currentEvent.Text;
	}

	private void OnChoiceMade(bool isRight)
	{
		if (_currentEvent == null) return;
		var effects = isRight ? _currentEvent.RightEffects : _currentEvent.LeftEffects;
		_state.Apply(effects);

		var prevLevel = _state.CurrentLevel;
		_state.AdvanceMonth();
		var newLevel = _state.CurrentLevel;

		UpdateSprintDisplay();

		if (newLevel != prevLevel)
			ShowPromotion();

		if (!_gameOverScreen.Visible)
			ShowNextEvent();
	}

	private void ShowPromotion()
	{
		var panel = new Panel();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical   = GrowDirection.Both;
		panel.CustomMinimumSize = new Vector2(460f, 80f);
		panel.AddThemeStyleboxOverride("panel", RoundedBox(new Color(0.12f, 0.12f, 0.16f), 12));

		var lbl = new Label { Text = $"Promoted to {_state.LevelName}!" };
		lbl.SetAnchorsPreset(LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.20f));
		panel.AddChild(lbl);
		AddChild(panel);

		var tw = CreateTween();
		tw.TweenInterval(2.2f);
		tw.TweenProperty(panel, "modulate:a", 0f, 0.6f);
		tw.TweenCallback(Callable.From(() => panel.QueueFree()));
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
		Shuffle(_events);
		_gameOverScreen.Visible = false;

		foreach (var key in GameState.Keys)
			OnAttributeChanged(key, 50);

		UpdateSprintDisplay();
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
