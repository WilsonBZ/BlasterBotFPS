using UnityEngine;

/// <summary>
/// Buff: GazGun (ChargeWeapon) explosions leave a lingering molotov fire zone on the floor.
/// Finds all ChargeProjectile prefabs referenced by ChargeWeapon components on the player
/// and enables molotovEnabled + assigns the molotovZonePrefab at runtime via a hook component.
/// </summary>
[CreateAssetMenu(fileName = "MolotovExplosionEffect", menuName = "Buff System/Effects/Molotov Explosion")]
public class MolotovExplosionEffect : BuffEffect
{
    [Tooltip("The MolotovZone prefab to spawn on each GazGun explosion.")]
    public GameObject molotovZonePrefab;

    public override void Apply(PlayerManager player)
    {
        if (player == null || molotovZonePrefab == null) return;

        ChargeWeapon[] weapons = player.GetComponentsInChildren<ChargeWeapon>(true);
        foreach (ChargeWeapon weapon in weapons)
        {
            if (weapon.GetComponent<MolotovChargeWeaponHook>() != null) continue;

            MolotovChargeWeaponHook hook = weapon.gameObject.AddComponent<MolotovChargeWeaponHook>();
            hook.molotovZonePrefab = molotovZonePrefab;
        }
    }

    public override string GetDescription() =>
        "GazGun explosions leave a molotov fire zone on the floor.";
}
