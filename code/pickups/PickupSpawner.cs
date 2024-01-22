using Sandbox;
using Sandbox.Network;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Pickup Spawner" )]
public class PickupSpawner : Component
{
	[Property] public GameObject PickupPrefab { get; set; }
	[Property] public float RespawnTime { get; set; } = 30f;
	
	private TimeUntil? TimeUntilRespawn { get; set; }
	private PickupComponent Pickup { get; set; }

	protected override void OnStart()
	{
		TimeUntilRespawn = 0f;
		base.OnStart();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		
		if ( !GameNetworkSystem.IsHost )
			return;
		
		if ( Pickup.IsValid() )
			return;

		if ( !TimeUntilRespawn.HasValue )
		{
			TimeUntilRespawn = RespawnTime;
			return;
		}

		if ( !TimeUntilRespawn.Value )
			return;

		var go = PickupPrefab.Clone();
			
		Pickup = go.Components.Get<PickupComponent>();
			
		go.Transform.Position = Transform.Position;
		go.Transform.Rotation = Transform.Rotation;
		go.NetworkSpawn();

		TimeUntilRespawn = null;
	}
}
