using Sandbox;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "View Model")]
public sealed class ViewModel : Component
{
	[Property] public SkinnedModelRenderer ModelRenderer { get; set; }
	[Property] public bool UseSprintAnimation { get; set; }
	
	/// <summary>
	/// Looks up the tree to find the player controller.
	/// </summary>
	private PlayerController PlayerController => Weapon.Components.GetInAncestors<PlayerController>();
	private CameraComponent Camera { get; set; }
	private WeaponComponent Weapon { get; set; }

	public void SetWeaponComponent( WeaponComponent weapon )
	{
		Weapon = weapon;
	}
	
	public void SetCamera( CameraComponent camera )
	{
		Camera = camera;
	}

	protected override void OnStart()
	{
		ModelRenderer.Set( "b_deploy", true );
		
		if ( PlayerController.IsValid() )
		{
			PlayerController.OnJump += OnPlayerJumped;
		}
	}

	protected override void OnDestroy()
	{
		if ( PlayerController.IsValid() )
		{
			PlayerController.OnJump -= OnPlayerJumped;
		}
		
		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		LocalRotation = Rotation.Identity;
		LocalPosition = Vector3.Zero;

		ApplyVelocity();
		ApplyStates();
		ApplyAnimationParameters();

		LerpedLocalRotation = Rotation.Lerp( LerpedLocalRotation, LocalRotation, Time.Delta * 10f );
		LerpedLocalPosition = LerpedLocalPosition.LerpTo( LocalPosition, Time.Delta * 10f );

		Camera.Transform.Position = Scene.Camera.Transform.Position;
		Camera.Transform.Rotation = Scene.Camera.Transform.Rotation;
		
		Transform.LocalRotation = LerpedLocalRotation;
		Transform.LocalPosition = LerpedLocalPosition;
	}

	private void OnPlayerJumped()
	{
		ModelRenderer.Set( "b_jump", true );
	}

	private Vector3 LerpedWishLook { get; set; }
	private Vector3 LocalPosition { get; set; }
	private Rotation LocalRotation { get; set; }
	private Vector3 LerpedLocalPosition { get; set; }
	private Rotation LerpedLocalRotation { get; set; }

	private void ApplyVelocity()
	{
		var moveVel = PlayerController.CharacterController.Velocity;
		var moveLen = moveVel.Length;
		if ( PlayerController.Tags.Has( "slide" ) ) moveLen = 0;

		var wishLook = PlayerController.WishVelocity.Normal * 1f;
		if ( PlayerController.IsAiming ) wishLook = 0;

		LerpedWishLook = LerpedWishLook.LerpTo( wishLook, Time.Delta * 5.0f );

		LocalRotation *= Rotation.From( 0, -LerpedWishLook.y * 3f, 0 );
		LocalPosition += -LerpedWishLook;

		ModelRenderer.Set( "move_groundspeed", moveLen );
	}

	private void ApplyStates()
	{
		if ( !PlayerController.Tags.Has( "slide" ) )
		{
			return;
		}

		LocalPosition += Vector3.Backward * 2f;
		LocalRotation *= Rotation.From( 10f, 25f, -5f );
	}

	private void ApplyAnimationParameters()
	{
		ModelRenderer.Set( "b_sprint", UseSprintAnimation && PlayerController.IsRunning );
		ModelRenderer.Set( "b_grounded", PlayerController.CharacterController.IsOnGround );

		// Ironsights
		ModelRenderer.Set( "ironsights", PlayerController.IsAiming ? 2 : 0 );
		ModelRenderer.Set( "ironsights_fire_scale", PlayerController.IsAiming ? 0.3f : 0f );
		
		ModelRenderer.Set( "b_empty", Weapon.AmmoInClip == 0 );
	}
}
