using Godot;
using System;

public partial class Card : Button
{
	//Effect Vars
    [Export] public float AngleXMax = 15.0f;
    [Export] public float AngleYMax = 15.0f;
    [Export] public float MaxOffsetShadow = 50.0f;
    
    [ExportCategory("Oscillator")]
    [Export] public float Spring = 150.0f;
    [Export] public float Damp = 10.0f;
    [Export] public float VelocityMultiplier = 2.0f;

    private float _displacement = 0.0f;
    private float _oscillatorVelocity = 0.0f;
    
    private Tween _tweenRot;
    private Tween _tweenHover;
    private Tween _tweenDestroy;
    private Tween _tweenHandle;
    private Vector2 _lastPos;
    private Vector2 _velocity;
    private TextureRect _cardTexture;
    private Control _shadow;
    private CollisionShape2D _collisionShape;
	private Label _label;
	
    private CardSlot _hoveredSlot;
 	private bool _followingMouse = false;
	private bool _placed = false;

	private CardData _cardData = null;

	private Vector2 _handPosition;



    public override void _Ready()
{
    AngleXMax = Mathf.DegToRad(AngleXMax);
    AngleYMax = Mathf.DegToRad(AngleYMax);
    _collisionShape = GetNode<CollisionShape2D>("DestroyArea/CollisionShape2D");
    _collisionShape.SetDeferred("disabled", false);

    _cardTexture = GetNode<TextureRect>("CardTexture");
    _shadow = GetNode<Control>("Shadow");
	//ADD card health dmg etc
	_label = GetNode<Label>("Label");

    // Connect the signals to the methods
    MouseEntered += _onMouseEntered;
    MouseExited += _onMouseExited;

	_cardData = Global.Instance.getRandomCard();
	GD.Print(_cardData.ToString());
	UpdateCardAppearance();
}

    public override void _Process(double delta)
    {
        RotateVelocity((float)delta);
        FollowMouse((float)delta);
        HandleShadow((float)delta);
    }

	private void UpdateCardAppearance()
	{
		_cardTexture.Texture = GD.Load<Texture2D>(_cardData.AssetPath);
		_label.Text = _cardData.Name + "\n" + _cardData.Damage + "\n" + _cardData.Health;

	}
    private void RotateVelocity(float delta)
    {
        if (!_followingMouse) return;

        var centerPos = GlobalPosition - (Size / 2.0f);
        _velocity = (Position - _lastPos) / delta;
        _lastPos = Position;
        
        _oscillatorVelocity += _velocity.Normalized().X * VelocityMultiplier;
        float force = -Spring * _displacement - Damp * _oscillatorVelocity;
        _oscillatorVelocity += force * delta;
        _displacement += _oscillatorVelocity * delta;

        Rotation = _displacement;
    }

    private void HandleShadow(float delta)
	//TODO make shadow actually follow card
    {
        var center = GetViewportRect().Size / 2.0f;
        float distance = GlobalPosition.X - center.X;

        _shadow.Position = new Vector2(
            Mathf.Lerp(0.0f, -Mathf.Sign(distance) * MaxOffsetShadow, Mathf.Abs(distance / center.X)),
            _shadow.Position.Y
        );
    }

    private void FollowMouse(float delta)
    {
        if (!_followingMouse) return;
        var mousePos = GetGlobalMousePosition();
        GlobalPosition = mousePos - (Size / 2.0f);
    }
    private void PlaceCardInSlot(CardSlot slot)
        {
            GlobalPosition = slot.GetCenterPosition() - (Size / 2.0f);
            slot.PlaceCard(this);
            _hoveredSlot = null;
        }
    public void Destroy()
    {
        _cardTexture.UseParentMaterial = true;
        if (_tweenDestroy != null && _tweenDestroy.IsRunning())
            _tweenDestroy.Kill();
        
        _tweenDestroy = CreateTween().SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
        _tweenDestroy.TweenProperty(_cardTexture.Material, "shader_parameter/dissolve_value", 0.0f, 2.0f).From(1.0f);
        _tweenDestroy.Parallel().TweenProperty(_shadow, "self_modulate:a", 0.0f, 1.0f);
    }

    public override void _GuiInput(InputEvent @event)
	{
		if (!_placed) {
			//if LMB is clicked, held, or released
			if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
			{
				_followingMouse = mouseEvent.IsPressed();

				if (!_followingMouse)
				{
					if (_hoveredSlot != null)
					{
						GD.Print("Attempting to place card in slot");
						PlaceCardInSlot(_hoveredSlot);
						_placed = true;
					}
					else
					{
						// Not over a slot, return to hand
						ReturnToHand();
					}

					if (_tweenHandle != null && _tweenHandle.IsRunning())
						_tweenHandle.Kill();

					_tweenHandle = CreateTween().SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Cubic);
					_tweenHandle.TweenProperty(this, "rotation", 0.0f, 0.3f);
				}
			}
		}
	}
	public void updateHoverStatus(bool hovering, CardSlot slot) {
		if (hovering) {
			_hoveredSlot = slot;
		}
		else if (!hovering && _hoveredSlot == slot) {
			_hoveredSlot = null;
		}
	}
    private void _onMouseEntered()
    {
        if (_tweenHover != null && _tweenHover.IsRunning())
            _tweenHover.Kill();

        _tweenHover = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
        _tweenHover.TweenProperty(this, "scale", new Vector2(2.2f, 2.2f), 0.5f);
    }

    private void _onMouseExited()
    {
        if (_tweenRot != null && _tweenRot.IsRunning())
            _tweenRot.Kill();

        _tweenRot = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back).SetParallel(true);

        // Reset shader rotation values to 0
        if (_cardTexture.Material is ShaderMaterial shaderMaterial)
        {
            _tweenRot.TweenProperty(shaderMaterial, "shader_parameter/x_rot", 0.0f, 0.5f);
            _tweenRot.TweenProperty(shaderMaterial, "shader_parameter/y_rot", 0.0f, 0.5f);
        }

        // Reset shadow rotation to 0
        _tweenRot.TweenProperty(_shadow, "rotation", 0.0f, 0.5f);

        if (_tweenHover != null && _tweenHover.IsRunning())
            _tweenHover.Kill();

        _tweenHover = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
        _tweenHover.TweenProperty(this, "scale", new Vector2(2f, 2f), 0.55f);
    }

	public void SetHandPosition(Vector2 position)
	{
		_handPosition = position;
	}

	private void ReturnToHand()
	{
		Position = _handPosition;
	}
}
