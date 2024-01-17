using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

public class PlayerController : Component
{
	[Property] public Vector3 Gravity { get; set; } = new ( 0f, 0f, 800f );
	
	public Vector3 WishVelocity { get; private set; }

	[Property] public List<GameObject> StartingWeapons { get; set; } = new();
	[Property] public GameObject Body { get; set; }
	[Property] public GameObject Head { get; set; }
	[Property] public GameObject Eye { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public GameObject WeaponBone { get; set; }
	
	[Sync] public Angles EyeAngles { get; set; }

	[Sync] public bool IsRunning { get; set; }

	public WeaponComponent ActiveWeapon => Components.GetInDescendants<WeaponComponent>();

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
			var weaponPrefab = StartingWeapons.FirstOrDefault();
			var weaponGo = weaponPrefab.Clone();
			weaponGo.SetParent( WeaponBone );
			weaponGo.Transform.Position = WeaponBone.Transform.Position;
			weaponGo.Transform.Rotation = WeaponBone.Transform.Rotation;
			weaponGo.Network.Spawn();
		}
			
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			var angles = EyeAngles.Normal;
			angles += Input.AnalogLook * 0.5f;
			angles.pitch = angles.pitch.Clamp( -60f, 80f );
			
			EyeAngles = angles.WithRoll( 0f );

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
			
			Scene.Camera.Transform.Rotation = EyeAngles.ToRotation();
			
			IsRunning = Input.Down( "Run" );
		}

		var cc = GameObject.Components.Get<CharacterController>();
		if ( cc is null ) return;

		if ( AnimationHelper is null ) return;

		AnimationHelper.WithVelocity( cc.Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );
		AnimationHelper.IsGrounded = cc.IsOnGround;
		AnimationHelper.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
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
			var flGroundFactor = 1.0f;
			var flMul = 268.3281572999747f * 1.2f;
			cc.Punch( Vector3.Up * flMul * flGroundFactor );
			OnJump();
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
	private void OnJump()
	{
		AnimationHelper?.TriggerJump();
	}
}
