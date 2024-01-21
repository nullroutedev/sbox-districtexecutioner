using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Player Controller" )]
public class PlayerController : Component
{
	[Property] public Vector3 Gravity { get; set; } = new ( 0f, 0f, 800f );
	
	public SkinnedModelRenderer ModelRenderer { get; private set; }
	public List<CitizenAnimationHelper> Animators { get; private set; } = new();
	public Vector3 WishVelocity { get; private set; }
	
	[Property] private CitizenAnimationHelper ShadowAnimator { get; set; }
	[Property] public WeaponContainer Weapons { get; set; }
	[Property] public GameObject Head { get; set; }
	[Property] public GameObject Eye { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public bool SicknessMode { get; set; }
	
	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public bool IsRunning { get; set; }

	private Angles Recoil { get; set; }

	public void ApplyRecoil( Angles recoil )
	{
		if ( !IsProxy )
		{
			Recoil += recoil;
		}
	}

	public void ResetViewAngles()
	{
		var rotation = Rotation.Identity;
		EyeAngles = rotation.Angles().WithRoll( 0f );
	}
	
	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		ModelRenderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();

		ResetViewAngles();
	}

	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			var weapons = WeaponManager.Instance.Weapons;

			foreach ( var weapon in weapons )
			{
				Weapons.Give( weapon );
			}
		}

		Animators.Add( ShadowAnimator );
		Animators.Add( AnimationHelper );
			
		base.OnStart();
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( IsProxy || !Scene.IsValid() )
			return;
		
		if ( Eye.IsValid() )
		{
			var idealEyePos = Eye.Transform.Position;
			
			var trace = Scene.Trace.Ray( Head.Transform.Position, idealEyePos )
				.UsePhysicsWorld()
				.IgnoreGameObjectHierarchy( GameObject )
				.WithAnyTags( "solid" )
				.Radius( 2f )
				.Run();

			Scene.Camera.Transform.Position = trace.Hit ? trace.EndPosition : idealEyePos;

			if ( SicknessMode )
				Scene.Camera.Transform.Rotation = Rotation.LookAt( Eye.Transform.Rotation.Left ) * Rotation.FromPitch( -10f );
			else
				Scene.Camera.Transform.Rotation = EyeAngles.ToRotation() * Rotation.FromPitch( -10f );
		}

		if ( ModelRenderer.IsValid() )
		{
			ModelRenderer.SetBodyGroup( "head", 1 );
		}
		
		base.OnPreRender();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			var angles = EyeAngles.Normal;
			angles += Input.AnalogLook * 0.5f;
			angles += Recoil * Time.Delta;
			angles.pitch = angles.pitch.Clamp( -60f, 80f );
			
			EyeAngles = angles.WithRoll( 0f );
			IsRunning = Input.Down( "Run" );
			Recoil = Recoil.LerpTo( Angles.Zero, Time.Delta * 8f );
		}

		var cc = GameObject.Components.Get<CharacterController>();
		if ( cc is null ) return;
		
		var weapon = Weapons.Deployed;

		foreach ( var animator in Animators )
		{
			animator.HoldType = weapon.IsValid() ? weapon.HoldType : CitizenAnimationHelper.HoldTypes.None;
			animator.WithVelocity( cc.Velocity );
			animator.WithWishVelocity( WishVelocity );
			animator.IsGrounded = cc.IsOnGround;
			animator.FootShuffle = 0f;
			animator.WithLook( EyeAngles.Forward );
			animator.MoveStyle = IsRunning ? CitizenAnimationHelper.MoveStyles.Run : CitizenAnimationHelper.MoveStyles.Walk;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		BuildWishVelocity();

		var cc = GameObject.Components.Get<CharacterController>();

		if ( cc.IsOnGround && Input.Down( "Jump" ) )
		{
			var groundFactor = 1.0f;
			var multiplier = 268.3281572999747f * 1.2f;
			cc.Punch( Vector3.Up * multiplier * groundFactor );
			SendJumpMessage();
		}

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			cc.Accelerate( WishVelocity );
			cc.ApplyFriction( 4.0f );
		}
		else
		{
			cc.Velocity -= Gravity * Time.Delta * 0.5f;
			cc.Accelerate( WishVelocity.ClampLength( 50 ) );
			cc.ApplyFriction( 0.1f );
		}

		cc.Move();

		if ( !cc.IsOnGround )
		{
			cc.Velocity -= Gravity * Time.Delta * 0.5f;
		}
		else
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
		}

		Transform.Rotation = Rotation.FromYaw( EyeAngles.ToRotation().Yaw() );

		if ( Input.MouseWheel.y > 0 )
			Weapons.Next();
		else if ( Input.MouseWheel.y < 0 )
			Weapons.Previous();

		var weapon = Weapons.Deployed;
		if ( !weapon.IsValid() ) return;

		if ( Input.Down( "Attack1" ) )
		{
			if ( weapon.DoPrimaryAttack() )
			{
				SendAttackMessage();
			}
		}

		if ( Input.Released( "Reload" ) )
		{
			if ( weapon.DoReload() )
			{
				SendReloadMessage();
			}
		}
	}

	private void BuildWishVelocity()
	{
		var rotation = EyeAngles.ToRotation();

		WishVelocity = rotation * Input.AnalogMove;
		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength )
			WishVelocity = WishVelocity.Normal;

		if ( Input.Down( "Run" ) )
			WishVelocity *= 320.0f;
		else
			WishVelocity *= 110.0f;
	}
	
	[Broadcast]
	private void SendReloadMessage()
	{
		foreach ( var animator in Animators )
		{
			var renderer = animator.Components.Get<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants );
			renderer?.Set( "b_reload", true );
		}
	}

	[Broadcast]
	private void SendAttackMessage()
	{
		foreach ( var animator in Animators )
		{
			var renderer = animator.Components.Get<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants );
			renderer?.Set( "b_attack", true );
		}
	}
	
	[Broadcast]
	private void SendJumpMessage()
	{
		foreach ( var animator in Animators )
		{
			animator.TriggerJump();
		}
	}
}
