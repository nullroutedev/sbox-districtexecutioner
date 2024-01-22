using System;
using Sandbox;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Weapon Pickup" )]
public class WeaponPickup : PickupComponent
{
	[Property] public PrefabScene WeaponPrefab { get; set; }
	
	[Broadcast]
	protected override void OnPickup( Guid pickerId )
	{
		var picker = Scene.Directory.FindByGuid( pickerId );
		if ( !picker.IsValid() ) return;

		var player = picker.Components.GetInDescendantsOrSelf<PlayerController>();
		if ( !player.IsValid() ) return;

		if ( player.IsProxy )
			return;

		if ( player.Weapons.Has( WeaponPrefab ) )
		{
			var template = WeaponPrefab.Clone();
			var templateComponent = template.Components.GetInDescendantsOrSelf<WeaponComponent>();
			
			player.Ammo.Give( templateComponent.AmmoType, templateComponent.DefaultAmmo );
			
			template.DestroyImmediate();
		}
		else
		{
			player.Weapons.Give( WeaponPrefab, true );
		}
	}
}
