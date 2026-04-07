using UnityEngine;

[CreateAssetMenu(fileName = "FireRateEffect", menuName = "Buff System/Effects/Fire Rate")]
public class FireRateEffect : BuffEffect
{
    public float fireRateIncrease = 2f;

    /// <summary>Maximum allowed fire rate to prevent projectiles from spawning inside geometry.</summary>
    private const float MaxFireRate = 20f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        ModularWeapon[] weapons = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (ModularWeapon weapon in weapons)
        {
            weapon.fireRate = Mathf.Min(weapon.fireRate + fireRateIncrease, MaxFireRate);
        }
    }

    public override string GetDescription()
    {
        return $"+{fireRateIncrease:F1} fire rate";
    }
}
