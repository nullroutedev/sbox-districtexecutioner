using System;
using Sandbox;

namespace Facepunch.Arena;

[Hide]
public abstract class PickupComponent : Component
{
	[Property] public Collider Collider { get; set; }
	[Property] public SoundEvent PickupSound { get; set; }
	
	private Vector3 StartPosition { get; set; }
	
	public void Pickup( GameObject picker )
	{
		if ( !Network.IsOwner )
			return;
		
		if ( !GameObject.IsValid() )
			return;

		PlayPickupSound();
		OnPickup( picker.Id );
		
		GameObject.Destroy();
	}
	
	protected virtual void OnPickup( Guid pickerId )
	{
		
	}

	protected override void OnStart()
	{
		StartPosition = Transform.Position;
		Collider.IsTrigger = true;
		
		base.OnStart();
	}

	protected override void OnFixedUpdate()
	{
		Transform.Position = StartPosition + Vector3.Up * MathF.Sin( Time.Now ) * 8f;
		Transform.Rotation = Transform.Rotation.RotateAroundAxis( Vector3.Up, 90f * Time.Delta );
		
		base.OnFixedUpdate();
	}

	[Broadcast]
	private void PlayPickupSound()
	{
		if ( PickupSound is null )
			return;
		
		Sound.Play( PickupSound, Transform.Position );
	}
}
