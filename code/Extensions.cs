using System;
using System.Threading.Tasks;
using Sandbox;

namespace Facepunch.Arena;

public static class Extensions
{
	
	
	public static async void OneShotParticle( this SceneWorld world, TaskSource ts, string effectPath, Action<SceneParticles> callback = null )
	{
		var particles = new SceneParticles( world, effectPath );

		callback?.Invoke( particles );

		try
		{
			while ( !particles.Finished )
			{
				await ts.Frame();
				particles.Simulate( Time.Delta );
			}
		}
		catch ( TaskCanceledException )
		{
			// Do nothing.
		}

		particles.Delete();
	}
}
