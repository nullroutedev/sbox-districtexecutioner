using System.Collections.Generic;
using Sandbox;

namespace Facepunch.Arena;

public class WeaponManager : Component
{
	public static WeaponManager Instance { get; private set; }

	public List<WeaponComponent> Weapons { get; set; } = new();
	
	[Property] public List<GameObject> Prefabs { get; set; }

	protected override void OnAwake()
	{
		Instance = this;

		foreach ( var prefab in Prefabs )
		{
			// Conna: cheeky sort of hack to get a list of available weapons.
			// this lets us read info from the weapons such as their DisplayName.
			
			var clone = prefab.Clone();
			var component = clone.Components.GetInDescendantsOrSelf<WeaponComponent>();
			
			if ( component.IsValid() )
			{
				Weapons.Add( component );
			}

			clone.Destroy();
		}
		
		base.OnAwake();
	}

	protected override void OnDestroy()
	{
		Instance = null;
		base.OnDestroy();
	}
}
