using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

public abstract class WeaponComponent : Component
{
	[Property] public string DisplayName { get; set; }
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.Pistol;
	
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

		SendTracerEffectMessage( origin, endPos, trace.Distance );
	}

	public virtual bool DoReload()
	{
		return false;
	}

	protected override void OnStart()
	{
		var player = Components.GetInAncestors<PlayerController>();

		if ( player.IsValid() )
		{
			player.AnimationHelper?.TriggerDeploy();
		}
		
		base.OnStart();
	}

	[Broadcast]
	private void SendTracerEffectMessage( Vector3 startPos, Vector3 endPos, float distance )
	{
		Scene.SceneWorld.OneShotParticle( Task, "particles/tracer/trail_smoke.vpcf", p =>
		{
			p.SetControlPoint( 0, startPos );
			p.SetControlPoint( 1, endPos );
			p.SetControlPoint( 2, distance );
		} );
	}
}
