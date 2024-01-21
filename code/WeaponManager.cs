using System.Collections.Generic;
using Sandbox;

namespace Facepunch.Arena;

[Group( "Arena" )]
[Title( "Weapon Manager")]
public class WeaponManager : Component
{
	public static WeaponManager Instance { get; private set; }

	public List<WeaponComponent> Weapons { get; set; } = new();
	
	[Property] public List<PrefabScene> Prefabs { get; set; }

	protected override void OnAwake()
	{
		Instance = this;

		foreach ( var prefab in Prefabs )
		{
			var template = prefab.Clone();
			var component = template.Components.GetInDescendantsOrSelf<WeaponComponent>();
			
			if ( component.IsValid() )
			{
				Weapons.Add( component );
			}

			template.Destroy();
		}
		
		base.OnAwake();
	}

	protected override void OnDestroy()
	{
		Instance = null;
		base.OnDestroy();
	}
}
