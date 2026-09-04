using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Control
{
	private GameState _state = null!;
	private List<EventData> _events = new();
	private EventData? _currentEvent;
	private readonly HashSet<string> _seenEventIds = new();

	private CardController _card = null!;
	private readonly Dictionary<string, Label> _statIconLabels = new();
	private readonly Dictionary<string, Color> _statIconColors = new();
	private readonly Dictionary<string, Label> _attrNameLabels = new();
	private readonly Dictionary<string, Label> _statDots = new();

	private Font _roboto = null!;

	private static readonly Color ColGrey     = new(0.40f, 0.40f, 0.40f);
	private static readonly Color ColPositive = new(0.35f, 1.00f, 0.45f);
	private static readonly Color ColNegative = new(1.00f, 0.32f, 0.32f);

	private Label _cardTextLabel = null!;
	private Control _gameOverScreen = null!;
	private Label _gameOverLabel = null!;
	private Label _sprintLabel = null!;
	private Label _characterNameLabel = null!;
	private Label _characterRoleLabel = null!;

	// Layout constants shared between BuildCard and BuildCharacterInfo
	private const float StatsBottom  = 116f; // 48 + 68 (compact stats row)
	private const float TextPad      = 8f;
	private const float TextH        = 72f;
	private const float CardGap      = 6f;
	private const float HintAreaH    = 38f;
	private const float CharInfoH    = 80f;
	private const float MaxColWidth  = 520f; // narrow column — black sides appear on wide screens

	public override void _Ready()
	{
		_state = GetNode<GameState>("/root/GameState");
		_state.AttributeChanged += OnAttributeChanged;
		_state.GameOver += OnGameOver;

		_roboto = GD.Load<FontFile>("res://fonts/RobotoMono-Regular.ttf");

		var theme = new Theme();
		theme.DefaultFont = _roboto;
		Theme = theme;

		SetAnchorsPreset(LayoutPreset.FullRect);

		BuildBackground();
		BuildSprintDisplay();
		BuildStatIcons();
		BuildCard();
		BuildCharacterInfo();
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
			Color = new Color(0.04f, 0.04f, 0.04f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);
	}

	private void BuildSprintDisplay()
	{
		var vp = GetViewportRect().Size;

		_sprintLabel = StyledLabel();
		_sprintLabel.Position = new Vector2(0f, 14f);
		_sprintLabel.Size = new Vector2(vp.X, 32f);
		_sprintLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_sprintLabel.VerticalAlignment = VerticalAlignment.Center;
		_sprintLabel.AddThemeFontSizeOverride("font_size", 15);
		_sprintLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
		_sprintLabel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_sprintLabel);

		UpdateSprintDisplay();
	}

	private void BuildStatIcons()
	{
		var vp = GetViewportRect().Size;

		var cfg = new (string key, string icon, Color color, string label)[]
		{
			("wellbeing",  "♥",   new Color(0.88f, 0.22f, 0.38f), "WELLBEING"),
			("morale",     "◉",   new Color(1.00f, 0.58f, 0.15f), "MORALE"),
			("prosperity", "↗",   new Color(0.20f, 0.85f, 0.65f), "COMPANY"),
			("codebase",   "</>", new Color(0.65f, 0.35f, 0.90f), "CODE QUALITY"),
		};

		float colW = ColWidth(vp);
		float colX = ColX(vp);

		var row = new HBoxContainer();
		row.Position = new Vector2(colX, 48f);
		row.Size = new Vector2(colW, 68f);
		row.AddThemeConstantOverride("separation", 0);
		row.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(row);

		foreach (var (key, icon, color, displayLabel) in cfg)
		{
			var col = new VBoxContainer();
			col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			col.Alignment = BoxContainer.AlignmentMode.Center;
			col.AddThemeConstantOverride("separation", 5);
			col.MouseFilter = MouseFilterEnum.Ignore;

			var iconLabel = StyledLabel(); iconLabel.Text = icon;
			iconLabel.HorizontalAlignment = HorizontalAlignment.Center;
			iconLabel.AddThemeFontSizeOverride("font_size", 28);
			iconLabel.AddThemeColorOverride("font_color", color);
			iconLabel.MouseFilter = MouseFilterEnum.Ignore;

			var nameLabel = StyledLabel(); nameLabel.Text = displayLabel;
			nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			nameLabel.AddThemeFontSizeOverride("font_size", 8);
			nameLabel.AddThemeColorOverride("font_color", new Color(0.42f, 0.42f, 0.42f));
			nameLabel.MouseFilter = MouseFilterEnum.Ignore;

			var dot = StyledLabel(); dot.Text = "●";
			dot.HorizontalAlignment = HorizontalAlignment.Center;
			dot.AddThemeFontSizeOverride("font_size", 7);
			dot.AddThemeColorOverride("font_color", new Color(0.58f, 0.58f, 0.58f));
			dot.Modulate = new Color(1f, 1f, 1f, 0f);
			dot.MouseFilter = MouseFilterEnum.Ignore;

			col.AddChild(dot);
			col.AddChild(iconLabel);
			col.AddChild(nameLabel);
			row.AddChild(col);

			_statIconLabels[key] = iconLabel;
			_statIconColors[key] = color;
			_attrNameLabels[key] = nameLabel;
			_statDots[key]        = dot;
		}
	}

	private float ColWidth(Vector2 vp) => Mathf.Min(vp.X - 80f, MaxColWidth);
	private float ColX(Vector2 vp)    => (vp.X - ColWidth(vp)) / 2f;
	private float CardY()             => StatsBottom + TextPad + TextH + CardGap;

	// Square card: size = min(column width, available height)
	private float CardSize(Vector2 vp)
	{
		float maxH = vp.Y - CardY() - HintAreaH - CharInfoH;
		return Mathf.Max(200f, Mathf.Min(ColWidth(vp), maxH));
	}

	private void BuildCard()
	{
		var vp    = GetViewportRect().Size;
		float colW = ColWidth(vp);
		float colX = ColX(vp);
		float size = CardSize(vp);   // square
		float cardX = colX + (colW - size) / 2f; // center within column
		float cardY = CardY();

		// Event text — between stats and card, constrained to column
		_cardTextLabel = StyledLabel();
		_cardTextLabel.Position = new Vector2(colX, StatsBottom + TextPad);
		_cardTextLabel.Size = new Vector2(colW, TextH);
		_cardTextLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_cardTextLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_cardTextLabel.VerticalAlignment = VerticalAlignment.Center;
		_cardTextLabel.AddThemeFontSizeOverride("font_size", 16);
		_cardTextLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.90f, 0.90f));
		_cardTextLabel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_cardTextLabel);

		// Square card centered in column
		_card = new CardController
		{
			Position       = new Vector2(cardX, cardY),
			Size           = new Vector2(size, size),
			SwipeThreshold = 120f,
		};
		_card.ChoiceMade          += OnChoiceMade;
		_card.DragDirectionChanged += OnDragDirectionChanged;

		// Choice text hints below card
		float hintY = cardY + size + 6f;
		float halfCol = colW / 2f;

		var leftHint = MakeHintLabel(
			new Vector2(colX, hintY),
			new Vector2(halfCol - 8f, 38f),
			new Color(1.00f, 0.40f, 0.40f),
			HorizontalAlignment.Left);

		var rightHint = MakeHintLabel(
			new Vector2(colX + halfCol + 8f, hintY),
			new Vector2(halfCol - 8f, 38f),
			new Color(0.40f, 1.00f, 0.55f),
			HorizontalAlignment.Right);

		AddChild(_card);
		AddChild(leftHint);
		AddChild(rightHint);

		_card.LeftHintLabel  = leftHint;
		_card.RightHintLabel = rightHint;

	}

	private void BuildCharacterInfo()
	{
		var vp    = GetViewportRect().Size;
		float infoY = CardY() + CardSize(vp) + HintAreaH;

		var vbox = new VBoxContainer();
		vbox.Position = new Vector2(0f, infoY);
		vbox.Size = new Vector2(vp.X, CharInfoH);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddThemeConstantOverride("separation", 8);
		vbox.MouseFilter = MouseFilterEnum.Ignore;

		_characterNameLabel = StyledLabel(); _characterNameLabel.Text = "Kryz";
		_characterNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_characterNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_characterNameLabel.AddThemeFontSizeOverride("font_size", 20);
		_characterNameLabel.AddThemeColorOverride("font_color", Colors.White);
		_characterNameLabel.MouseFilter = MouseFilterEnum.Ignore;

		_characterRoleLabel = StyledLabel(); _characterRoleLabel.Text = "Manager";
		_characterRoleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_characterRoleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_characterRoleLabel.AddThemeFontSizeOverride("font_size", 12);
		_characterRoleLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
		_characterRoleLabel.MouseFilter = MouseFilterEnum.Ignore;

		vbox.AddChild(_characterNameLabel);
		vbox.AddChild(_characterRoleLabel);
		AddChild(vbox);
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

		var heading = StyledLabel(); heading.Text = "GAME OVER";
		heading.HorizontalAlignment = HorizontalAlignment.Center;
		heading.AddThemeFontSizeOverride("font_size", 32);
		heading.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.35f));

		_gameOverLabel = StyledLabel();
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
		var unseen = pool.FindAll(e => !_seenEventIds.Contains(e.Id));
		if (unseen.Count == 0)
		{
			_seenEventIds.Clear();
			unseen = pool;
		}
		if (unseen.Count == 0) return;
		_currentEvent = unseen[(int)(GD.Randi() % (uint)unseen.Count)];
		_seenEventIds.Add(_currentEvent.Id);
		_card.LoadEvent(_currentEvent);
		_cardTextLabel.Text = _currentEvent.Text;

		string charName = string.IsNullOrEmpty(_currentEvent.CharacterName) ? "Kryz" : _currentEvent.CharacterName;
		string charRole = string.IsNullOrEmpty(_currentEvent.CharacterRole) ? "Manager" : _currentEvent.CharacterRole;
		_characterNameLabel.Text = charName;
		_characterRoleLabel.Text = charRole;
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

		var lbl = StyledLabel(); lbl.Text = $"Promoted to {_state.LevelName}!";
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
		if (_statDots.TryGetValue(key, out var dot))
		{
			bool affected = c == ColPositive || c == ColNegative;
			dot.Modulate = affected ? Colors.White : new Color(1f, 1f, 1f, 0f);
		}
	}

	private void OnAttributeChanged(string key, int value)
	{
		// Stat values tracked in GameState; no bar animation needed
	}

	private void OnGameOver(string message, bool isWin)
	{
		_gameOverLabel.Text = message;
		_gameOverScreen.Visible = true;
	}

	private void OnRestart()
	{
		_state.Reset();
		_seenEventIds.Clear();
		_gameOverScreen.Visible = false;

		foreach (var key in GameState.Keys)
			SetAttrColor(key, ColGrey); // restores original icon colors

		UpdateSprintDisplay();
		ShowNextEvent();
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private void UpdateSprintDisplay()
	{
		_sprintLabel.Text = $"Sprint {_state.CurrentSprint}  ·  {_state.CurrentDate:MMM yyyy}";
	}

	private Label StyledLabel() { var l = new Label(); l.AddThemeFontOverride("font", _roboto); return l; }

	private Label MakeHintLabel(Vector2 pos, Vector2 size, Color color, HorizontalAlignment align)
	{
		var lbl = StyledLabel();
		lbl.Position = pos;
		lbl.Size = size;
		lbl.HorizontalAlignment = align;
		lbl.AutowrapMode = TextServer.AutowrapMode.Word;
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.Modulate = new Color(1f, 1f, 1f, 0f);
		return lbl;
	}

	private Panel MakeArrowCircle(Vector2 pos, string arrow, Color color)
	{
		var panel = new Panel();
		panel.Position = pos;
		panel.Size = new Vector2(48f, 48f);
		panel.MouseFilter = MouseFilterEnum.Ignore;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.10f, 0.14f, 0.75f),
			CornerRadiusTopLeft     = 24,
			CornerRadiusTopRight    = 24,
			CornerRadiusBottomLeft  = 24,
			CornerRadiusBottomRight = 24,
		});

		var lbl = StyledLabel(); lbl.Text = arrow;
		lbl.SetAnchorsPreset(LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		lbl.AddThemeFontSizeOverride("font_size", 20);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.MouseFilter = MouseFilterEnum.Ignore;
		panel.AddChild(lbl);
		return panel;
	}

	private static StyleBoxFlat RoundedBox(Color color, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft     = radius,
			CornerRadiusTopRight    = radius,
			CornerRadiusBottomLeft  = radius,
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
