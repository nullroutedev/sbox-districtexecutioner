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
	[Property] public float DamageForce { get; set; } = 5f;
	[Property] public float Damage { get; set; } = 10f;
	[Property] public AmmoType AmmoType { get; set; } = AmmoType.Pistol;
	[Property] public int DefaultAmmo { get; set; } = 60;
	[Property] public int ClipSize { get; set; } = 30;
	[Property] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.Pistol;
	[Property] public SoundEvent DeploySound { get; set; }
	[Property] public SoundEvent FireSound { get; set; }
	[Property] public SoundEvent EmptyClipSound { get; set; }
	[Property] public SoundSequenceData ReloadSoundSequence { get; set; }
	[Property] public ParticleSystem MuzzleFlash { get; set; }
	[Property] public ParticleSystem ImpactEffect { get; set; }
	[Property] public ParticleSystem MuzzleSmoke { get; set; }
	
	[Sync, Property] public bool IsReloading { get; set; }
	[Sync, Property, Change( nameof( OnIsDeployedChanged ) )] public bool IsDeployed { get; set; }
	[Sync] public int AmmoInClip { get; set; }
	
	private SkinnedModelRenderer ModelRenderer { get; set; }
	private SoundSequence ReloadSound { get; set; }
	private TimeUntil ReloadFinishTime { get; set; }
	private TimeUntil NextAttackTime { get; set; }
	private bool Initialized { get; set; }
	
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
			.UseHitboxes()
			.Run();

		var damage = Damage;
		var origin = attachment?.Position ?? startPos;

		SendAttackMessage( origin, trace.EndPosition, trace.Distance );

		IHealthComponent damageable = null;
		if ( trace.Component.IsValid() )
			damageable = trace.Component.Components.GetInAncestorsOrSelf<IHealthComponent>();

		if ( damageable is not null )
		{
			if ( trace.Hitbox is not null && trace.Hitbox.Tags.Has( "head" ) )
			{
				Sound.Play( "hitmarker.headshot" );
				damage *= 2f;
			}
			else
			{
				Sound.Play( "hitmarker.hit" );
			}
			
			damageable.TakeDamage( DamageType.Bullet, damage, trace.EndPosition, trace.Direction * DamageForce, GameObject.Id );
		}
		else if ( trace.Hit )
		{
			SendImpactMessage( trace.EndPosition, trace.Normal );
			Sound.Play( "hitmarker.hit" );
		}
		
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
		else
			OnDeployed();

		Initialized = true;
		
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

	protected override void OnAwake()
	{
		ModelRenderer = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>( true );
		base.OnAwake();
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
		if ( !Initialized )
			return;
		
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
	private void SendImpactMessage( Vector3 position, Vector3 normal )
	{
		if ( ImpactEffect is not null )
		{
			Scene.SceneWorld.OneShotParticle( Task, ImpactEffect.ResourcePath, p =>
			{
				p.SetControlPoint( 0, position );
				p.SetControlPoint( 0, Rotation.LookAt( normal ) );
			} );
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
				if ( !ModelRenderer.IsValid() )
					return;
				
				var transform = ModelRenderer.SceneModel.GetAttachment( "muzzle" );

				if ( transform.HasValue )
				{
					p.SetControlPoint( 0, transform.Value );
				}
			} );
		}
		
		if ( MuzzleSmoke is not null )
		{
			Scene.SceneWorld.OneShotParticle( Task, MuzzleSmoke.ResourcePath, p =>
			{
				if ( !ModelRenderer.IsValid() )
					return;
				
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
