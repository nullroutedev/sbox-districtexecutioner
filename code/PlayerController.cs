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
	
	public Vector3 WishVelocity { get; private set; }
	
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
	
	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		if ( !Scene.Camera.IsValid() )
			return;
		
		EyeAngles = Scene.Camera.Transform.Rotation.Angles().WithRoll( 0f );
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
			
		base.OnStart();
	}

	protected override void OnPreRender()
	{
		if ( !IsProxy && Scene.IsValid() && Eye.IsValid() )
		{
			var idealEyePos = Eye.Transform.Position + Eye.Transform.Rotation.Forward * 1f;
			
			var trace = Scene.Trace.Ray( Head.Transform.Position, idealEyePos )
				.UsePhysicsWorld()
				.IgnoreGameObjectHierarchy( GameObject )
				.WithAnyTags( "solid" )
				.Run();

			if ( trace.Hit )
				Scene.Camera.Transform.Position = trace.EndPosition - trace.Direction * 1f;
			else
				Scene.Camera.Transform.Position = idealEyePos;

			if ( SicknessMode )
				Scene.Camera.Transform.Rotation = Rotation.LookAt( Eye.Transform.Rotation.Left ) * Rotation.FromPitch( -10f );
			else
				Scene.Camera.Transform.Rotation = EyeAngles.ToRotation() * Rotation.FromPitch( -10f );
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

		if ( AnimationHelper is null ) return;

		var weapon = Weapons.Deployed;

		AnimationHelper.HoldType = weapon.IsValid() ? weapon.HoldType : CitizenAnimationHelper.HoldTypes.None;
		AnimationHelper.WithVelocity( cc.Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );
		AnimationHelper.IsGrounded = cc.IsOnGround;
		AnimationHelper.FootShuffle = 0f;
		AnimationHelper.WithLook( EyeAngles.Forward );
		AnimationHelper.MoveStyle = IsRunning ? CitizenAnimationHelper.MoveStyles.Run : CitizenAnimationHelper.MoveStyles.Walk;
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
		var renderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		renderer?.Set( "b_reload", true );
	}

	[Broadcast]
	private void SendAttackMessage()
	{
		var renderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		renderer?.Set( "b_attack", true );
	}
	
	[Broadcast]
	private void SendJumpMessage()
	{
		AnimationHelper?.TriggerJump();
	}
}
