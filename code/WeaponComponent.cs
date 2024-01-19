using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

public abstract class WeaponComponent : Component
{
	public virtual void OnPrimaryAttack()
	{
		var renderer = Components.Get<SkinnedModelRenderer>();
		var attachment = renderer.GetAttachment( "muzzle", true );
		var startPos = Scene.Camera.Transform.Position;
		var endPos = startPos + Scene.Camera.Transform.Rotation.Forward * 10000f;
		var trace = Scene.Trace.Ray( startPos, endPos )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.UsePhysicsWorld()
			.WithAnyTags( "solid" )
			.Run();

		// Don't think GetAttachment is working right.
		var origin = attachment?.Position ?? startPos;
		origin = Transform.Position + Transform.Rotation.Forward * 30f + Transform.Rotation.Up * 7f;

		DoNetworkedTracerEffect( origin, endPos, trace.Distance );
	}

	[Broadcast]
	private void DoNetworkedTracerEffect( Vector3 startPos, Vector3 endPos, float distance )
	{
		DoTracerEffect( "particles/tracer/trail_smoke.vpcf", startPos, endPos, distance );
	}

	private async void DoTracerEffect( string effectPath, Vector3 startPos, Vector3 endPos, float distance )
	{
		var particles = new SceneParticles( Scene.SceneWorld, effectPath );
		particles.SetControlPoint( 0, startPos );
		particles.SetControlPoint( 1, endPos );
		particles.SetControlPoint( 2, distance );

		try
		{
			while ( !particles.Finished )
			{
				await Task.Frame();
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
