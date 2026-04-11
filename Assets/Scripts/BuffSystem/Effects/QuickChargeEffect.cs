using UnityEngine;

/// <summary>
/// Reduces the minimum and maximum charge time on every ChargeWeapon the player carries,
/// letting them fire a fully-charged shot faster.
/// </summary>
[CreateAssetMenu(fileName = "QuickChargeEffect", menuName = "Buff System/Effects/Quick Charge")]
public class QuickChargeEffect : BuffEffect
{
    [Tooltip("Seconds subtracted from both minChargeTime and maxChargeTime.")]
    public float chargeTimeReduction = 0.4f;

    [Tooltip("Minimum value either charge time is allowed to reach.")]
    public float minChargeTimeFloor = 0.1f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        ChargeWeapon[] weapons = player.GetComponentsInChildren<ChargeWeapon>(true);
        foreach (ChargeWeapon weapon in weapons)
        {
            weapon.MinChargeTime -= chargeTimeReduction;
            weapon.MaxChargeTime -= chargeTimeReduction;
        }
    }

    public override string GetDescription() =>
        $"Charge time -{chargeTimeReduction:F1}s on all charge weapons";
}
