using UnityEngine;

/// <summary>
/// Increases the maximum damage of every ChargeWeapon the player carries.
/// Rewards players who hold the charge to full.
/// </summary>
[CreateAssetMenu(fileName = "OverchargeEffect", menuName = "Buff System/Effects/Overcharge")]
public class OverchargeEffect : BuffEffect
{
    [Tooltip("Flat damage added to maxDamage on every ChargeWeapon.")]
    public float bonusDamage = 50f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        ChargeWeapon[] weapons = player.GetComponentsInChildren<ChargeWeapon>(true);
        foreach (ChargeWeapon weapon in weapons)
            weapon.MaxDamage += bonusDamage;
    }

    public override string GetDescription() =>
        $"Charge weapon max damage +{bonusDamage:F0}";
}
