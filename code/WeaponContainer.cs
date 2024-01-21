using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Weapon Container" )]
public sealed class WeaponContainer : Component
{
	[Property] public GameObject WeaponBone { get; set; }
	[Property] public AmmoContainer Ammo { get; set; }
	
	public WeaponComponent Deployed => Components.GetAll<WeaponComponent>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault( c => c.IsDeployed );
	public IEnumerable<WeaponComponent> All => Components.GetAll<WeaponComponent>( FindMode.EverythingInSelfAndDescendants );
	public bool HasAny => All.Any();
	
	public void Give( WeaponComponent template )
	{
		if ( IsProxy ) return;

		var weaponGo = template.GameObject.Clone();
		weaponGo.SetParent( WeaponBone );
		weaponGo.Transform.Position = WeaponBone.Transform.Position;
		weaponGo.Transform.Rotation = WeaponBone.Transform.Rotation;

		var weapon = weaponGo.Components.GetInDescendantsOrSelf<WeaponComponent>( true );
		weapon.AmmoInClip = weapon.ClipSize;
		weapon.IsDeployed = !Deployed.IsValid();

		Ammo.Give( weapon.AmmoType, weapon.DefaultAmmo );
		
		weaponGo.NetworkSpawn();
	}

	[Broadcast]
	public void Next()
	{
		if ( !HasAny ) return;
		
		var weapons = All.ToList();
		var currentIndex = -1;
		var deployed = Deployed;

		if ( deployed.IsValid() )
		{
			currentIndex = weapons.IndexOf( Deployed );
		}

		var nextIndex = currentIndex + 1;
		if ( nextIndex >= weapons.Count )
			nextIndex = 0;
		
		var nextWeapon = weapons[nextIndex];
		if ( nextWeapon == deployed )
			return;

		foreach ( var weapon in weapons )
		{
			weapon.IsDeployed = false;
		}
		
		nextWeapon.IsDeployed = true;
	}

	[Broadcast]
	public void Previous()
	{
		if ( !HasAny ) return;
		
		var weapons = All.ToList();
		var currentIndex = -1;
		var deployed = Deployed;

		if ( deployed.IsValid() )
		{
			currentIndex = weapons.IndexOf( Deployed );
		}

		var previousIndex = currentIndex - 1;
		if ( previousIndex < 0 )
			previousIndex = weapons.Count - 1;

		var previousWeapon = weapons[previousIndex];
		if ( previousWeapon == deployed )
			return;
		
		foreach ( var weapon in weapons )
		{
			weapon.IsDeployed = false;
		}
		
		previousWeapon.IsDeployed = true;
	}
}
