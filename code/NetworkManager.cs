using System.Linq;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Network Manager")]
public class NetworkManager : Component, Component.INetworkListener
{
	[Property] public PrefabScene PlayerPrefab { get; set; }
	[Property] public GameObject SpawnpointContainer { get; set; }

	protected override void OnStart()
	{
		if ( !GameNetworkSystem.IsActive )
		{
			GameNetworkSystem.CreateLobby();
		}
		
		base.OnStart();
	}

	void INetworkListener.OnActive( Connection connection )
	{
		var player = PlayerPrefab.Clone().Components.Get<PlayerController>();
		var spawnpoints = SpawnpointContainer.Components.GetAll<SpawnPoint>();
		var randomSpawnpoint = Game.Random.FromList( spawnpoints.ToList() );

		player.Transform.Position = randomSpawnpoint.Transform.Position;
		player.Transform.Rotation = Rotation.FromYaw( randomSpawnpoint.Transform.Rotation.Yaw() );
		player.EyeAngles = player.Transform.Rotation;
		
		player.GameObject.NetworkSpawn( connection );
	}
}
