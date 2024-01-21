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
	[Property] public AmmoType AmmoType { get; set; } = AmmoType.Pistol;
	[Property] public int DefaultAmmo { get; set; } = 60;
	[Property] public int ClipSize { get; set; } = 30;
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.Pistol;
	[Property] public SoundEvent DeploySound { get; set; }
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundEvent EmptyClipSound { get; set; }
	[Property] public SoundSequenceData ReloadSoundSequence { get; set; }
	[Property] public ParticleSystem MuzzleFlash { get; set; }
	
	[Sync, Property] public bool IsReloading { get; set; }
	[Sync, Property, Change( nameof( OnIsDeployedChanged ) )] public bool IsDeployed { get; set; }
	[Sync] public int AmmoInClip { get; set; }
	
	private SkinnedModelRenderer ModelRenderer { get; set; }
	private SoundSequence ReloadSound { get; set; }
	private TimeUntil ReloadFinishTime { get; set; }
	private TimeUntil NextAttackTime { get; set; }
	
	public virtual bool DoPrimaryAttack()
	{
		if ( !NextAttackTime ) return false;
		if ( IsReloading ) return false;

		if ( AmmoInClip <= 0 )
		{
			SendEmptyClipMessage();
			NextAttackTime = 1f / FireRate;
			return false;
		}
		
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
		AmmoInClip--;

		var player = Components.GetInAncestors<PlayerController>();
		if ( player.IsValid() )
		{
			player.ApplyRecoil( Recoil );
		}

		return true;
	}

	public virtual bool DoReload()
	{
		var ammoToTake = ClipSize - AmmoInClip;
		if ( ammoToTake <= 0 )
			return false;
		
		var player = Components.GetInAncestors<PlayerController>();
		if ( !player.IsValid() )
			return false;

		if ( !player.Ammo.TryTake( AmmoType, ammoToTake, out var taken ) )
			return false;

		ReloadFinishTime = ReloadTime;
		IsReloading = true;
		AmmoInClip = taken;
			
		SendReloadMessage();
			
		return true;
	}

	protected override void OnStart()
	{
		if ( !IsDeployed )
			OnHolstered();
		
		base.OnStart();
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

	protected override void OnUpdate()
	{
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
		}
		
		base.OnDestroy();
	}
	
	private void OnIsDeployedChanged( bool oldValue, bool newValue )
	{
		if ( oldValue == newValue )
			return;
		
		if ( newValue )
			OnDeployed();
		else
			OnHolstered();
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
	private void SendEmptyClipMessage()
	{
		if ( EmptyClipSound is not null )
		{
			Sound.Play( EmptyClipSound, Transform.Position );
		}
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
