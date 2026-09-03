using Godot;

public partial class CardController : Panel
{
	[Signal] public delegate void ChoiceMadeEventHandler(bool isRight);
	[Signal] public delegate void DragDirectionChangedEventHandler(bool active, bool isRight);

	public float SwipeThreshold { get; set; } = 130f;
	public float MaxTilt { get; set; } = 0.12f;

	public Label? LeftHintLabel;
	public Label? RightHintLabel;

	private TextureRect _bgRect = null!;

	private bool _dragging;
	private Vector2 _dragStart;
	private Vector2 _dragBasePosition;
	private Vector2 _basePosition;
	private bool _committed;
	private Tween? _activeTween;
	private bool _hinting;
	private bool _hintIsRight;

	private static readonly Color HiddenColor = new(1f, 1f, 1f, 0f);

	public override void _Ready()
	{
		_basePosition = Position;
		ClipContents = true;
		BuildVisuals();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && _bgRect != null)
			_bgRect.Size = Size;
	}

	private void BuildVisuals()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.11f, 0.16f),
			CornerRadiusTopLeft    = 18,
			CornerRadiusTopRight   = 18,
			CornerRadiusBottomLeft = 18,
			CornerRadiusBottomRight = 18,
			BorderWidthTop    = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft   = 1,
			BorderWidthRight  = 1,
			BorderColor = new Color(0.22f, 0.22f, 0.30f),
			ShadowColor = new Color(0f, 0f, 0f, 0.70f),
			ShadowSize = 20,
			ShadowOffset = new Vector2(0, 4),
		};
		AddThemeStyleboxOverride("panel", style);

		_bgRect = new TextureRect();
		_bgRect.Position     = Vector2.Zero;
		_bgRect.Size         = Size;         // fill the card explicitly
		_bgRect.ExpandMode   = TextureRect.ExpandModeEnum.IgnoreSize;
		_bgRect.StretchMode  = TextureRect.StretchModeEnum.KeepAspectCovered;
		_bgRect.Modulate     = Colors.White;
		AddChild(_bgRect);
	}

	public void LoadEvent(EventData ev)
	{
		_activeTween?.Kill();
		_activeTween = null;

		_committed = false;
		_dragging = false;
		Position = _basePosition;
		Rotation = 0f;
		Modulate = Colors.White;

		ClearHintState();

		if (LeftHintLabel != null)  { LeftHintLabel.Text  = $"← {ev.LeftChoiceText}";  LeftHintLabel.Modulate  = HiddenColor; }
		if (RightHintLabel != null) { RightHintLabel.Text = $"{ev.RightChoiceText} →"; RightHintLabel.Modulate = HiddenColor; }

		if (!string.IsNullOrEmpty(ev.BackgroundPath) && ResourceLoader.Exists(ev.BackgroundPath))
			_bgRect.Texture = ResourceLoader.Load<Texture2D>(ev.BackgroundPath);
		else
			_bgRect.Texture = null;
	}

	public override void _Input(InputEvent @event)
	{
		if (_committed) return;

		if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
		{
			if (btn.Pressed)
			{
				var local = btn.GlobalPosition - GlobalPosition;
				if (!new Rect2(Vector2.Zero, Size).HasPoint(local)) return;

				_dragging = true;
				_dragStart = btn.GlobalPosition;
				_dragBasePosition = Position;
			}
			else if (_dragging)
			{
				_dragging = false;
				float dx = Position.X - _dragBasePosition.X;
				if (Mathf.Abs(dx) >= SwipeThreshold)
					Commit(dx > 0);
				else
					SnapBack();
			}
		}
		else if (@event is InputEventMouseMotion motion && _dragging)
		{
			Vector2 delta = motion.GlobalPosition - _dragStart;
			Position = _dragBasePosition + new Vector2(delta.X, delta.Y * 0.2f);
			float norm = Mathf.Clamp(delta.X / SwipeThreshold, -1f, 1f);
			Rotation = norm * MaxTilt;
			UpdateHints(delta.X);
		}
	}

	private void UpdateHints(float dx)
	{
		float alpha = Mathf.Clamp((Mathf.Abs(dx) - 30f) / (SwipeThreshold - 30f), 0f, 1f);
		bool goLeft = dx < 0;
		if (LeftHintLabel  != null) LeftHintLabel.Modulate  = new Color(1f, 1f, 1f, goLeft ? alpha : 0f);
		if (RightHintLabel != null) RightHintLabel.Modulate = new Color(1f, 1f, 1f, goLeft ? 0f : alpha);

		bool nowActive = alpha > 0.05f;
		bool nowRight  = !goLeft;
		if (nowActive != _hinting || (nowActive && nowRight != _hintIsRight))
		{
			_hinting     = nowActive;
			_hintIsRight = nowRight;
			EmitSignal(SignalName.DragDirectionChanged, nowActive, nowRight);
		}
	}

	private void SnapBack()
	{
		ClearHintState();

		_activeTween?.Kill();
		_activeTween = CreateTween().SetParallel(true);
		_activeTween.TweenProperty(this, "position", _basePosition, 0.35f)
			.SetTrans(Tween.TransitionType.Spring).SetEase(Tween.EaseType.Out);
		_activeTween.TweenProperty(this, "rotation", 0f, 0.35f).SetEase(Tween.EaseType.Out);

		if (LeftHintLabel  != null) LeftHintLabel.Modulate  = HiddenColor;
		if (RightHintLabel != null) RightHintLabel.Modulate = HiddenColor;
	}

	private void ClearHintState()
	{
		if (_hinting)
		{
			_hinting = false;
			EmitSignal(SignalName.DragDirectionChanged, false, false);
		}
	}

	private void Commit(bool right)
	{
		_committed = true;
		_dragging  = false;
		ClearHintState();

		var target   = _basePosition + new Vector2(right ? 1300f : -1300f, (float)GD.RandRange(-80.0, 80.0));
		float endRot = right ? MaxTilt * 2f : -MaxTilt * 2f;

		_activeTween?.Kill();
		_activeTween = CreateTween().SetParallel(true);
		_activeTween.TweenProperty(this, "position", target, 0.4f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		_activeTween.TweenProperty(this, "rotation", endRot, 0.4f);
		_activeTween.TweenProperty(this, "modulate:a", 0f, 0.3f).SetDelay(0.1f);
		_activeTween.SetParallel(false);
		_activeTween.TweenCallback(Callable.From(() => EmitSignal(SignalName.ChoiceMade, right)));
	}
}
