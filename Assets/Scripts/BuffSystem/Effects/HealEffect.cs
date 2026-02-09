using UnityEngine;

[CreateAssetMenu(fileName = "HealEffect", menuName = "Buff System/Effects/Heal")]
public class HealEffect : BuffEffect
{
    [Range(0f, 1f)]
    public float healPercentage = 0.2f;
    
    public override void Apply(PlayerManager player)
    {
        if (player == null) return;
        
        float healAmount = player.MaxHealth * healPercentage;
        player.Heal(healAmount);
    }
    
    public override string GetDescription()
    {
        return $"Restore {healPercentage * 100f:F0}% health";
    }
}
