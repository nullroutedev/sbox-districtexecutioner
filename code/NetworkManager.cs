using System.Linq;
using Sandbox;
using Sandbox.Network;

namespace Facepunch.Arena;

public class NetworkManager : Component, Component.INetworkListener
{
	[Property] public PrefabScene PlayerPrefab { get; set; }

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
		var player = PlayerPrefab.Clone();
		var spawnpoints = Scene.GetAllComponents<SpawnPoint>();
		var randomSpawnpoint = Game.Random.FromList( spawnpoints.ToList() );

		player.Transform.Position = randomSpawnpoint.Transform.Position;
		player.Transform.Rotation = randomSpawnpoint.Transform.Rotation;
		
		player.Network.Spawn( connection );
	}
}
