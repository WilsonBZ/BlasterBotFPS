using UnityEngine;

/// <summary>
/// Runtime component added to a ModularWeapon by HomingStingEffect.
/// Patches every Projectile that the weapon fires so that homingEnabled is true.
/// Works by subscribing to ModularWeapon.OnProjectileSpawned if available,
/// or falling back to a polling approach via FixedUpdate.
/// </summary>
[DisallowMultipleComponent]
public class HomingProjectileSpawnerHook : MonoBehaviour
{
    public float turnSpeed       = 180f;
    public float searchRadius    = 8f;
    public float activationDelay = 0.15f;

    private ModularWeapon weapon;

    private void Awake()
    {
        weapon = GetComponent<ModularWeapon>();
    }

    /// <summary>
    /// Called by ModularWeapon immediately after it instantiates / pools a projectile.
    /// Injects homing settings before OnEnable runs its target search.
    /// </summary>
    public void OnProjectileFired(GameObject projectileGO)
    {
        if (projectileGO == null) return;

        Projectile proj = projectileGO.GetComponent<Projectile>();
        if (proj == null) return;

        proj.homingEnabled         = true;
        proj.homingTurnSpeed       = turnSpeed;
        proj.homingSearchRadius    = searchRadius;
        proj.homingActivationDelay = activationDelay;
    }
}
