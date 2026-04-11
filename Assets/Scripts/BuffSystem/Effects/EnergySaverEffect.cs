using UnityEngine;

/// <summary>
/// Reduces the energy cost per shot on every weapon the player carries by a percentage.
/// Stacks multiplicatively — two 30% reductions = 49% total reduction.
/// </summary>
[CreateAssetMenu(fileName = "EnergySaverEffect", menuName = "Buff System/Effects/Energy Saver")]
public class EnergySaverEffect : BuffEffect
{
    [Range(0f, 0.9f)]
    [Tooltip("Fraction to reduce energy cost by. 0.3 = 30% cheaper shots.")]
    public float reductionPercent = 0.3f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        ModularWeapon[] weapons = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (ModularWeapon weapon in weapons)
            weapon.energyCostPerShot *= (1f - reductionPercent);
    }

    public override string GetDescription() =>
        $"Weapon energy cost -{reductionPercent * 100f:F0}%";
}
