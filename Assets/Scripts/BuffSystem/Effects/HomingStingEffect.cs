using UnityEngine;

/// <summary>
/// Buff: StingGun projectiles home in on the nearest enemy after firing.
/// Adds a MonoBehaviour hook (HomingProjectileSpawnerHook) to the weapon so every
/// new Projectile instance gets homingEnabled = true the moment it is fired.
/// </summary>
[CreateAssetMenu(fileName = "HomingStingEffect", menuName = "Buff System/Effects/Homing Sting")]
public class HomingStingEffect : BuffEffect
{
    [Tooltip("Degrees per second the homing projectile turns toward its target.")]
    public float turnSpeed = 180f;
    [Tooltip("Search radius (world units) of the rolling detection sphere.")]
    public float searchRadius = 8f;
    [Tooltip("Seconds of straight flight before the homing sphere activates.")]
    public float activationDelay = 0.15f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        // Find all ModularWeapon components on the player's ring / children.
        ModularWeapon[] weapons = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (ModularWeapon weapon in weapons)
        {
            // Avoid double-adding.
            if (weapon.GetComponent<HomingProjectileSpawnerHook>() != null) continue;

            HomingProjectileSpawnerHook hook = weapon.gameObject.AddComponent<HomingProjectileSpawnerHook>();
            hook.turnSpeed       = turnSpeed;
            hook.searchRadius    = searchRadius;
            hook.activationDelay = activationDelay;
        }
    }

    public override string GetDescription() =>
        "StingGun projectiles home in on the nearest enemy.";
}
