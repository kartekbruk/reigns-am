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
    private Label _titleLabel = null!;
    private Label _textLabel = null!;

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

    private void BuildVisuals()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.13f, 0.18f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.55f),
            ShadowSize = 24,
        };
        AddThemeStyleboxOverride("panel", style);

        _bgRect = new TextureRect();
        _bgRect.SetAnchorsPreset(LayoutPreset.FullRect);
        _bgRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _bgRect.Modulate = new Color(0.5f, 0.5f, 0.5f);
        AddChild(_bgRect);

        // Gradient-like dark overlay at bottom for readability
        var overlay = new ColorRect();
        overlay.SetAnchorsPreset(LayoutPreset.BottomWide);
        overlay.AnchorTop = 0.25f;
        overlay.Color = new Color(0f, 0f, 0f, 0.6f);
        AddChild(overlay);

        _titleLabel = new Label();
        _titleLabel.SetAnchorsPreset(LayoutPreset.TopWide);
        _titleLabel.OffsetLeft = 20;
        _titleLabel.OffsetRight = -20;
        _titleLabel.OffsetTop = 24;
        _titleLabel.OffsetBottom = 84;
        _titleLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        AddChild(_titleLabel);

        _textLabel = new Label();
        _textLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _textLabel.OffsetLeft = 24;
        _textLabel.OffsetRight = -24;
        _textLabel.OffsetTop = 100;
        _textLabel.OffsetBottom = -20;
        _textLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _textLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _textLabel.VerticalAlignment = VerticalAlignment.Center;
        _textLabel.AddThemeFontSizeOverride("font_size", 16);
        _textLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.92f));
        AddChild(_textLabel);
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

        _titleLabel.Text = ev.Title;
        _textLabel.Text = ev.Text;

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
