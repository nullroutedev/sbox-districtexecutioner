using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

public class PlayerController : Component
{
	[Property] public Vector3 Gravity { get; set; } = new ( 0f, 0f, 800f );
	
	public Vector3 WishVelocity { get; private set; }

	[Property] public GameObject Body { get; set; }
	[Property] public GameObject Head { get; set; }
	[Property] public GameObject Eye { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public GameObject WeaponBone { get; set; }
	
	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public bool IsRunning { get; set; }

	public WeaponComponent ActiveWeapon => Components.GetInDescendantsOrSelf<WeaponComponent>();
	public IEnumerable<WeaponComponent> Weapons => Components.GetAll<WeaponComponent>( FindMode.EverythingInSelfAndDescendants );

	public void GiveWeapon( WeaponComponent template )
	{
		if ( IsProxy )
		{
			Log.Error( "Only the owner can give a weapon to the player!" );
			return;
		}

		var weaponGo = template.GameObject.Clone();
		weaponGo.SetParent( WeaponBone );
		weaponGo.Transform.Position = WeaponBone.Transform.Position;
		weaponGo.Transform.Rotation = WeaponBone.Transform.Rotation;
		weaponGo.NetworkSpawn();
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
				GiveWeapon( weapon );
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
			angles.pitch = angles.pitch.Clamp( -60f, 80f );
			
			EyeAngles = angles.WithRoll( 0f );
			IsRunning = Input.Down( "Run" );
		}

		var cc = GameObject.Components.Get<CharacterController>();
		if ( cc is null ) return;

		if ( AnimationHelper is null ) return;

		var weapon = ActiveWeapon;

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

		var weapon = ActiveWeapon;
		if ( !weapon.IsValid() ) return;

		if ( Input.Released( "Attack1" ) )
		{
			weapon.OnPrimaryAttack();
			SendAttackMessage();
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
