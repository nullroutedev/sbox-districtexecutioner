using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;

namespace Facepunch.Arena;

public abstract class WeaponComponent : Component
{
	[Property] public string DisplayName { get; set; }
	[Property] public float ReloadTime { get; set; } = 2f;
	[Property] public float DeployTime { get; set; } = 0.5f;
	[Property] public float FireRate { get; set; } = 3f;
	[Property] public float Spread { get; set; } = 0.01f;
	[Property] public Angles Recoil { get; set; }
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.Pistol;
	[Property] public SoundEvent DeploySound { get; set; }
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundSequenceData ReloadSoundSequence { get; set; }
	[Property] public ParticleSystem MuzzleFlash { get; set; }
	
	[Sync, Property] public bool IsReloading { get; set; }
	[Sync, Property] public bool IsDeployed { get; set; }
	
	private SkinnedModelRenderer ModelRenderer { get; set; }
	private SoundSequence ReloadSound { get; set; }
	private TimeUntil ReloadFinishTime { get; set; }
	private TimeUntil NextAttackTime { get; set; }
	private bool WasDeployed { get; set; }
	
	public virtual bool DoPrimaryAttack()
	{
		if ( !NextAttackTime ) return false;
		if ( IsReloading ) return false;
		
		var renderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		var attachment = renderer.GetAttachment( "muzzle" );
		var startPos = Scene.Camera.Transform.Position;
		var direction = Scene.Camera.Transform.Rotation.Forward;
		direction += Vector3.Random * Spread;
		
		var endPos = startPos + direction * 10000f;
		var trace = Scene.Trace.Ray( startPos, endPos )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.UsePhysicsWorld()
			.WithAnyTags( "solid" )
			.Run();
		
		var origin = attachment?.Position ?? startPos;

		SendAttackMessage( origin, endPos, trace.Distance );
		NextAttackTime = 1f / FireRate;

		var player = Components.GetInAncestors<PlayerController>();
		if ( player.IsValid() )
		{
			player.ApplyRecoil( Recoil );
		}

		return true;
	}

	public virtual bool DoReload()
	{
		ReloadFinishTime = ReloadTime;
		IsReloading = true;
		SendReloadMessage();
		
		return true;
	}

	protected virtual void OnDeployed()
	{
		var player = Components.GetInAncestors<PlayerController>();

		if ( player.IsValid() )
		{
			foreach ( var animator in player.Animators )
			{
				animator.TriggerDeploy();
			}
		}
		
		if ( ModelRenderer.IsValid() )
		{
			ModelRenderer.Enabled = true;
		}
		
		if ( DeploySound is not null )
		{
			Sound.Play( DeploySound, Transform.Position );
		}
		
		NextAttackTime = DeployTime;
	}

	protected virtual void OnHolstered()
	{
		if ( ModelRenderer.IsValid() )
		{
			ModelRenderer.Enabled = false;
		}

		ReloadSound?.Stop();
	}

	protected override void OnEnabled()
	{
		ModelRenderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		base.OnEnabled();
	}

	protected override void OnStart()
	{
		if ( IsDeployed && !WasDeployed )
		{
			OnDeployed();
			WasDeployed = true;
		}

		if ( !IsDeployed )
		{
			OnHolstered();
			WasDeployed = false;
		}
		
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if ( IsDeployed && !WasDeployed )
		{
			WasDeployed = true;
			OnDeployed();
		}

		if ( !IsDeployed && WasDeployed )
		{
			WasDeployed = false;
			OnHolstered();
		}

		if ( !IsProxy && ReloadFinishTime )
		{
			IsReloading = false;
		}

		ReloadSound?.Update( Transform.Position );

		base.OnUpdate();
	}

	protected override void OnDestroy()
	{
		if ( IsDeployed )
		{
			OnHolstered();
			WasDeployed = false;
		}
		
		base.OnDestroy();
	}

	[Broadcast]
	private void SendReloadMessage()
	{
		if ( ReloadSoundSequence is null )
			return;
		
		ReloadSound?.Stop();
		
		ReloadSound = new( ReloadSoundSequence );
		ReloadSound.Start( Transform.Position );
	}

	[Broadcast]
	private void SendAttackMessage( Vector3 startPos, Vector3 endPos, float distance )
	{
		Scene.SceneWorld.OneShotParticle( Task, "particles/tracer/trail_smoke.vpcf", p =>
		{
			p.SetControlPoint( 0, startPos );
			p.SetControlPoint( 1, endPos );
			p.SetControlPoint( 2, distance );
		} );

		if ( MuzzleFlash is not null )
		{
			Scene.SceneWorld.OneShotParticle( Task, MuzzleFlash.ResourcePath, p =>
			{
				var transform = ModelRenderer.SceneModel.GetAttachment( "muzzle" );

				if ( transform.HasValue )
				{
					p.SetControlPoint( 0, transform.Value );
				}
			} );
		}

		if ( FireSound is not null )
		{
			Sound.Play( FireSound, startPos );
		}
	}
}
