using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Control
{
	private GameState _state = null!;
	private List<EventData> _events = new();
	private List<EventData> _consequences = new();
	private EventData? _currentEvent;
	private readonly HashSet<string> _seenEventIds = new();
	private readonly Queue<string> _pendingConsequences = new();
	private string? _immediateConsequence = null;

	private CardController _card = null!;
	private readonly Dictionary<string, Control> _statFillMasks  = new();
	private readonly Dictionary<string, Label>   _statFillLabels = new();
	private readonly Dictionary<string, Label>   _attrNameLabels = new();
	private readonly Dictionary<string, Label>   _statDots       = new();

	private const float IconWrapperH = 36f;

	private Font _roboto = null!;

	private static readonly Color ColGrey     = new(0.40f, 0.40f, 0.40f);
	private static readonly Color ColPositive = new(0.35f, 1.00f, 0.45f);
	private static readonly Color ColNegative = new(1.00f, 0.32f, 0.32f);

	private Label _cardTextLabel = null!;
	private Control _startScreen = null!;
	private Control _gameOverScreen = null!;
	private Label _gameOverLabel = null!;
	private Label _gameOverStatsLabel = null!;
	private Label _sprintLabel = null!;
	private Label _chancesLabel = null!;
	private Label _goodMomentsLabel = null!;
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
		_state.GameOver         += OnGameOver;
		_state.SavedByChance    += OnSavedByChance;
		_state.GoodMoment       += OnGoodMoment;

		_roboto = GD.Load<FontFile>("res://fonts/RobotoMono-Regular.ttf");

		var theme = new Theme();
		theme.DefaultFont = _roboto;
		Theme = theme;

		SetAnchorsPreset(LayoutPreset.FullRect);

		BuildBackground();
		BuildSprintDisplay();
		BuildChancesDisplay();
		BuildGoodMomentsDisplay();
		BuildStatIcons();
		BuildDeck();
		BuildCard();
		BuildCharacterInfo();
		BuildGameOverScreen();

		_events = EventLoader.Load("res://events/events.xml");
		Shuffle(_events);
		_consequences = EventLoader.Load("res://events/consequences.xml");

		BuildStartScreen();
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

	private void BuildGoodMomentsDisplay()
	{
		var vp = GetViewportRect().Size;

		_goodMomentsLabel = StyledLabel();
		_goodMomentsLabel.Position = new Vector2(10f, 14f);
		_goodMomentsLabel.Size = new Vector2(120f, 32f);
		_goodMomentsLabel.HorizontalAlignment = HorizontalAlignment.Left;
		_goodMomentsLabel.VerticalAlignment = VerticalAlignment.Center;
		_goodMomentsLabel.AddThemeFontSizeOverride("font_size", 13);
		_goodMomentsLabel.AddThemeColorOverride("font_color", new Color(0.30f, 0.30f, 0.30f));
		_goodMomentsLabel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_goodMomentsLabel);

		UpdateGoodMomentsDisplay();
	}

	private void BuildChancesDisplay()
	{
		var vp = GetViewportRect().Size;

		_chancesLabel = StyledLabel();
		_chancesLabel.Position = new Vector2(vp.X - 130f, 14f);
		_chancesLabel.Size = new Vector2(120f, 32f);
		_chancesLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_chancesLabel.VerticalAlignment = VerticalAlignment.Center;
		_chancesLabel.AddThemeFontSizeOverride("font_size", 13);
		_chancesLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.40f));
		_chancesLabel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_chancesLabel);

		UpdateChancesDisplay();
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

			float frac = Mathf.Clamp(_state.Get(key) / 100f, 0f, 1f);

			// Icon wrapper — fixed height, clips children for fill effect
			var iconWrapper = new Control();
			iconWrapper.CustomMinimumSize = new Vector2(0f, IconWrapperH);
			iconWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			iconWrapper.MouseFilter = MouseFilterEnum.Ignore;

			// Background: dim grey icon (the "empty" portion)
			var bgIcon = StyledLabel(); bgIcon.Text = icon;
			bgIcon.SetAnchorsPreset(LayoutPreset.FullRect);
			bgIcon.HorizontalAlignment = HorizontalAlignment.Center;
			bgIcon.VerticalAlignment   = VerticalAlignment.Center;
			bgIcon.AddThemeFontSizeOverride("font_size", 28);
			bgIcon.AddThemeColorOverride("font_color", new Color(0.22f, 0.22f, 0.22f));
			bgIcon.MouseFilter = MouseFilterEnum.Ignore;
			iconWrapper.AddChild(bgIcon);

			// Clip mask anchored to the bottom (fill fraction)
			var fillMask = new Control();
			fillMask.ClipContents = true;
			fillMask.AnchorLeft   = 0f;  fillMask.AnchorRight  = 1f;
			fillMask.AnchorTop    = 1f - frac;  fillMask.AnchorBottom = 1f;
			fillMask.MouseFilter  = MouseFilterEnum.Ignore;
			iconWrapper.AddChild(fillMask);

			// Colored icon inside mask — offset upward so the visible slice aligns with the bg
			var fillLabel = StyledLabel(); fillLabel.Text = icon;
			fillLabel.AnchorLeft   = 0f;  fillLabel.AnchorRight  = 1f;
			fillLabel.AnchorTop    = 0f;  fillLabel.OffsetTop    = -(1f - frac) * IconWrapperH;
			fillLabel.AnchorBottom = 1f;  fillLabel.OffsetBottom = 0f;
			fillLabel.HorizontalAlignment = HorizontalAlignment.Center;
			fillLabel.VerticalAlignment   = VerticalAlignment.Center;
			fillLabel.AddThemeFontSizeOverride("font_size", 28);
			fillLabel.AddThemeColorOverride("font_color", color);
			fillLabel.MouseFilter = MouseFilterEnum.Ignore;
			fillMask.AddChild(fillLabel);

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
			col.AddChild(iconWrapper);
			col.AddChild(nameLabel);
			row.AddChild(col);

			_statFillMasks[key]  = fillMask;
			_statFillLabels[key] = fillLabel;
			_attrNameLabels[key] = nameLabel;
			_statDots[key]       = dot;
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

	private void BuildDeck()
	{
		var vp    = GetViewportRect().Size;
		float colW = ColWidth(vp);
		float colX = ColX(vp);
		float size = CardSize(vp);
		float cardX = colX + (colW - size) / 2f;
		float cardY = CardY();

		var reverse = GD.Load<Texture2D>("res://sprites/fritz-reverse-crop.png");

		var stack = new (float dx, float dy, float rot)[]
		{
			(6f, 5f,   0.022f),
			(3f, 2.5f, 0.011f),
			(0f, 0f,   0f),
		};

		foreach (var (dx, dy, rot) in stack)
		{
			var img = new NinePatchRect();
			img.Texture           = reverse;
			img.Position          = new Vector2(cardX + dx, cardY + dy);
			img.Size              = new Vector2(size, size);
			img.Rotation          = rot;
			img.PatchMarginLeft   = 0;
			img.PatchMarginRight  = 0;
			img.PatchMarginTop    = 0;
			img.PatchMarginBottom = 0;
			img.MouseFilter       = MouseFilterEnum.Ignore;
			AddChild(img);
		}
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

		// Hint labels live inside the card so they rotate/move with it
		var hintColor = new Color(0.75f, 0.60f, 1.00f);
		float hintW = size * 0.4f;
		float hintH = size * 0.35f;

		var leftHint = MakeHintLabel(
			new Vector2(12f, 12f),
			new Vector2(hintW, hintH),
			hintColor,
			HorizontalAlignment.Left);

		var rightHint = MakeHintLabel(
			new Vector2(size - 12f - hintW, 12f),
			new Vector2(hintW, hintH),
			hintColor,
			HorizontalAlignment.Right);

		AddChild(_card);
		_card.AddChild(leftHint);
		_card.AddChild(rightHint);

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

		_gameOverStatsLabel = StyledLabel();
		_gameOverStatsLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_gameOverStatsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_gameOverStatsLabel.AddThemeFontSizeOverride("font_size", 14);
		_gameOverStatsLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));

		var btn = new Button { Text = "Back to Menu" };
		btn.CustomMinimumSize = new Vector2(180f, 52f);
		btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		btn.AddThemeFontOverride("font", _roboto);
		btn.Pressed += OnRestart;

		vbox.AddChild(heading);
		vbox.AddChild(_gameOverLabel);
		vbox.AddChild(_gameOverStatsLabel);
		vbox.AddChild(btn);
		center.AddChild(vbox);

		_gameOverScreen.AddChild(overlay);
		_gameOverScreen.AddChild(center);
		AddChild(_gameOverScreen);
	}

	private void BuildStartScreen()
	{
		_startScreen = new Control { Visible = true };
		_startScreen.SetAnchorsPreset(LayoutPreset.FullRect);
		_startScreen.MouseFilter = MouseFilterEnum.Stop;

		var overlay = new ColorRect { Color = new Color(0.04f, 0.04f, 0.04f, 0.96f) };
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);

		var vbox = new VBoxContainer();
		vbox.CustomMinimumSize = new Vector2(420f, 0f);
		vbox.AddThemeConstantOverride("separation", 24);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;

		var title = StyledLabel(); title.Text = "Reigns AM";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 52);
		title.AddThemeColorOverride("font_color", Colors.White);

		var subtitle = StyledLabel(); subtitle.Text = "Survive the corporate ladder";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AutowrapMode = TextServer.AutowrapMode.Word;
		subtitle.AddThemeFontSizeOverride("font_size", 15);
		subtitle.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));

		// ── Hints panel ──────────────────────────────────────────────────────
		var hintsPanel = new PanelContainer();
		hintsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hintsPanel.AddThemeStyleboxOverride("panel", RoundedBox(new Color(0.10f, 0.10f, 0.13f), 10));

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left",   20);
		margin.AddThemeConstantOverride("margin_right",  20);
		margin.AddThemeConstantOverride("margin_top",    18);
		margin.AddThemeConstantOverride("margin_bottom", 18);

		var hintsVbox = new VBoxContainer();
		hintsVbox.AddThemeConstantOverride("separation", 14);
		margin.AddChild(hintsVbox);
		hintsPanel.AddChild(margin);

		hintsVbox.AddChild(HintRow("← Swipe cards left or right to make choices →",
			new Color(0.75f, 0.60f, 1.00f), 13));

		hintsVbox.AddChild(HintRow("♥  ◉  ↗  </>   Keep all four stats above zero to survive",
			new Color(0.80f, 0.80f, 0.80f), 13));

		hintsVbox.AddChild(HintRow("★  Good Moments  —  top-left corner  —  earned when any stat hits 100",
			new Color(0.40f, 0.90f, 0.60f), 12));

		hintsVbox.AddChild(HintRow("✦  Extra Chances  —  top-right corner  —  one chance per year, saves you at 25% when any stat hits zero",
			new Color(0.95f, 0.80f, 0.40f), 12));

		// ─────────────────────────────────────────────────────────────────────

		var btn = new Button { Text = "New Game" };
		btn.CustomMinimumSize = new Vector2(200f, 56f);
		btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		btn.AddThemeFontOverride("font", _roboto);
		btn.Pressed += OnNewGame;

		vbox.AddChild(title);
		vbox.AddChild(subtitle);
		vbox.AddChild(hintsPanel);
		vbox.AddChild(btn);
		center.AddChild(vbox);

		_startScreen.AddChild(overlay);
		_startScreen.AddChild(center);
		AddChild(_startScreen);
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
		// Instant consequence — guaranteed to fire as the very next card
		if (_immediateConsequence != null)
		{
			string cid = _immediateConsequence;
			_immediateConsequence = null;
			var conseq = _consequences.Find(e => e.Id == cid);
			if (conseq != null)
			{
				ShowEvent(conseq);
				return;
			}
		}

		// With ~35% probability per turn, fire a pending consequence event instead
		if (_pendingConsequences.Count > 0 && GD.Randf() < 0.35f)
		{
			string cid = _pendingConsequences.Dequeue();
			var conseq = _consequences.Find(e => e.Id == cid);
			if (conseq != null)
			{
				ShowEvent(conseq);
				return;
			}
		}

		var pool = GetEventPool();
		var unseen = pool.FindAll(e => !_seenEventIds.Contains(e.Id));
		if (unseen.Count == 0)
		{
			_seenEventIds.Clear();
			unseen = pool;
		}
		if (unseen.Count == 0) return;
		var next = unseen[(int)(GD.Randi() % (uint)unseen.Count)];
		_seenEventIds.Add(next.Id);
		ShowEvent(next);
	}

	private void ShowEvent(EventData ev)
	{
		_currentEvent = ev;
		_card.LoadEvent(ev);
		_cardTextLabel.Text = ev.Text;

		_characterNameLabel.Text = string.IsNullOrEmpty(ev.CharacterName) ? "Kryz" : ev.CharacterName;
		_characterRoleLabel.Text    = ev.CharacterRole ?? "";
		_characterRoleLabel.Visible = !string.IsNullOrEmpty(ev.CharacterRole);
	}

	private void OnChoiceMade(bool isRight)
	{
		if (_currentEvent == null) return;
		var effects = isRight ? _currentEvent.RightEffects : _currentEvent.LeftEffects;

		// Snapshot before Apply so we can detect whether a chance was consumed.
		// If a chance fires, OnSavedByChance already updates the display correctly.
		// We must not overwrite it after AdvanceMonth(), which could cross a year
		// boundary and make AvailableChances jump back to 1.
		int chancesSnapshot = _state.AvailableChances;
		_state.Apply(effects);
		bool chanceConsumed = _state.AvailableChances < chancesSnapshot;

		string cid     = isRight ? _currentEvent.RightConsequenceId    : _currentEvent.LeftConsequenceId;
		bool   instant = isRight ? _currentEvent.RightConsequenceInstant : _currentEvent.LeftConsequenceInstant;
		if (!string.IsNullOrEmpty(cid))
		{
			if (instant)
				_immediateConsequence = cid;
			else
				_pendingConsequences.Enqueue(cid);
			ShowConsequenceWarning();
		}

		var prevLevel = _state.CurrentLevel;
		if (!_currentEvent.NoTimeSkip)
			_state.AdvanceMonth();
		var newLevel = _state.CurrentLevel;

		UpdateSprintDisplay();
		// Only refresh the counter if no chance was spent this sprint.
		// When a chance IS spent, OnSavedByChance already set the display to 0
		// and we don't want AdvanceMonth()'s potential year-boundary unlock to
		// overwrite that before the player even sees it.
		if (!chanceConsumed)
			UpdateChancesDisplay();

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

	private void OnAttributeChanged(string key, int value) => UpdateIconFill(key, value);

	private void UpdateIconFill(string key, int value)
	{
		if (!_statFillMasks.TryGetValue(key, out var mask))   return;
		if (!_statFillLabels.TryGetValue(key, out var label)) return;
		float frac = Mathf.Clamp(value / 100f, 0f, 1f);
		mask.AnchorTop   = 1f - frac;
		label.OffsetTop  = -(1f - frac) * IconWrapperH;
	}

	private void OnGameOver(string message, bool isWin)
	{
		_gameOverLabel.Text = message;

		int months = _state.CurrentSprint - 1;
		int years  = months / 12;
		int rem    = months % 12;
		string timeStr = years > 0
			? $"{years} yr {rem} mo"
			: $"{months} mo";

		_gameOverStatsLabel.Text =
			$"Position · {_state.LevelName}\n" +
			$"Time worked · {timeStr}\n" +
			$"Good Moments · {_state.GoodMoments}";

		_gameOverScreen.Visible = true;
	}

	private void OnGoodMoment(int total)
	{
		UpdateGoodMomentsDisplay();

		var panel = new Panel();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical   = GrowDirection.Both;
		panel.CustomMinimumSize = new Vector2(380f, 70f);
		panel.AddThemeStyleboxOverride("panel", RoundedBox(new Color(0.08f, 0.16f, 0.10f), 12));

		var lbl = StyledLabel(); lbl.Text = $"★  Good Moment!  ×{total}";
		lbl.SetAnchorsPreset(LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.AddThemeColorOverride("font_color", new Color(0.40f, 0.95f, 0.60f));
		panel.AddChild(lbl);
		AddChild(panel);

		var tw = CreateTween();
		tw.TweenInterval(2.2f);
		tw.TweenProperty(panel, "modulate:a", 0f, 0.6f);
		tw.TweenCallback(Callable.From(() => panel.QueueFree()));
	}

	private void ShowConsequenceWarning()
	{
		var panel = new Panel();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical   = GrowDirection.Both;
		panel.CustomMinimumSize = new Vector2(420f, 70f);
		panel.AddThemeStyleboxOverride("panel", RoundedBox(new Color(0.16f, 0.10f, 0.10f), 12));

		var lbl = StyledLabel(); lbl.Text = "This choice will have consequences...";
		lbl.SetAnchorsPreset(LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		lbl.AddThemeFontSizeOverride("font_size", 16);
		lbl.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.20f));
		panel.AddChild(lbl);
		AddChild(panel);

		var tw = CreateTween();
		tw.TweenInterval(2.0f);
		tw.TweenProperty(panel, "modulate:a", 0f, 0.5f);
		tw.TweenCallback(Callable.From(() => panel.QueueFree()));
	}

	private void OnSavedByChance(string message)
	{
		UpdateChancesDisplay();
		var panel = new Panel();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical   = GrowDirection.Both;
		panel.CustomMinimumSize = new Vector2(460f, 80f);
		panel.AddThemeStyleboxOverride("panel", RoundedBox(new Color(0.10f, 0.10f, 0.16f), 12));

		var lbl = StyledLabel(); lbl.Text = message;
		lbl.SetAnchorsPreset(LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		lbl.AutowrapMode = TextServer.AutowrapMode.Word;
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.40f));
		panel.AddChild(lbl);
		AddChild(panel);

		var tw = CreateTween();
		tw.TweenInterval(2.8f);
		tw.TweenProperty(panel, "modulate:a", 0f, 0.6f);
		tw.TweenCallback(Callable.From(() => panel.QueueFree()));
	}

	private void OnRestart()
	{
		_state.Reset();
		_seenEventIds.Clear();
		_pendingConsequences.Clear();
		_immediateConsequence = null;
		_gameOverScreen.Visible = false;

		foreach (var key in GameState.Keys)
		{
			SetAttrColor(key, ColGrey);
			UpdateIconFill(key, _state.Get(key));
		}

		UpdateSprintDisplay();
		UpdateChancesDisplay();
		UpdateGoodMomentsDisplay();
		_startScreen.Visible = true;
	}

	private void OnNewGame()
	{
		_startScreen.Visible = false;
		ShowNextEvent();
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private void UpdateSprintDisplay()
	{
		_sprintLabel.Text = $"Sprint {_state.CurrentSprint}  ·  {_state.CurrentDate:MMM yyyy}";
	}

	private void UpdateGoodMomentsDisplay()
	{
		int n = _state.GoodMoments;
		_goodMomentsLabel.Text = $"★ {n}";
		_goodMomentsLabel.AddThemeColorOverride("font_color", n > 0
			? new Color(0.40f, 0.85f, 0.55f)
			: new Color(0.30f, 0.30f, 0.30f));
	}

	private void UpdateChancesDisplay()
	{
		int n = _state.AvailableChances;
		_chancesLabel.Text = $"✦ {n}";
		_chancesLabel.AddThemeColorOverride("font_color", n > 0
			? new Color(0.95f, 0.80f, 0.40f)
			: new Color(0.30f, 0.30f, 0.30f));
	}

	private Label StyledLabel() { var l = new Label(); l.AddThemeFontOverride("font", _roboto); return l; }

	private Label HintRow(string text, Color color, int fontSize)
	{
		var lbl = StyledLabel();
		lbl.Text = text;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		lbl.AddThemeFontSizeOverride("font_size", fontSize);
		lbl.AddThemeColorOverride("font_color", color);
		return lbl;
	}

	private Label MakeHintLabel(Vector2 pos, Vector2 size, Color color, HorizontalAlignment align)
	{
		var lbl = StyledLabel();
		lbl.Position = pos;
		lbl.Size = size;
		lbl.HorizontalAlignment = align;
		lbl.AutowrapMode = TextServer.AutowrapMode.Word;
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeConstantOverride("outline_size", 6);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
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
