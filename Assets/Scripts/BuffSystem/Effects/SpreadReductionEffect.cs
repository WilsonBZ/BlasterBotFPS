using UnityEngine;

[CreateAssetMenu(fileName = "SpreadReductionEffect", menuName = "Buff System/Effects/Spread Reduction")]
public class SpreadReductionEffect : BuffEffect
{
    [Range(0f, 1f)]
    public float reductionPercent = 0.3f;
    
    public override void Apply(PlayerManager player)
    {
        if (player == null) return;
        
        ModularWeapon[] weapons = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (ModularWeapon weapon in weapons)
        {
            weapon.ReduceSpreadPercent(reductionPercent);
        }
    }
    
    public override string GetDescription()
    {
        return $"Reduce weapon spread by {reductionPercent * 100f:F0}%";
    }
}
