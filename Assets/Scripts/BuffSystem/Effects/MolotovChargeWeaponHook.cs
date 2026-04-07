using UnityEngine;

/// <summary>
/// Runtime component added to a ChargeWeapon by MolotovExplosionEffect.
/// Patches every ChargeProjectile the weapon fires so molotovEnabled = true.
/// Called by ChargeWeapon.OnProjectileFired immediately after spawning the projectile.
/// </summary>
[DisallowMultipleComponent]
public class MolotovChargeWeaponHook : MonoBehaviour
{
    public GameObject molotovZonePrefab;

    /// <summary>Called by ChargeWeapon immediately after instantiating a ChargeProjectile.</summary>
    public void OnProjectileFired(GameObject projectileGO)
    {
        if (projectileGO == null) return;

        ChargeProjectile proj = projectileGO.GetComponent<ChargeProjectile>();
        if (proj == null) return;

        proj.molotovEnabled    = true;
        proj.molotovZonePrefab = molotovZonePrefab;
    }
}
