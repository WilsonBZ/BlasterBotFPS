using UnityEngine;

[CreateAssetMenu(fileName = "MaxHealthEffect", menuName = "Buff System/Effects/Max Health")]
public class MaxHealthEffect : BuffEffect
{
    public float healthIncrease = 20f;
    public bool healOnGain = true;
    
    public override void Apply(PlayerManager player)
    {
        if (player == null) return;
        
        player.IncreaseMaxHealth(healthIncrease);
        
        if (healOnGain)
        {
            player.Heal(healthIncrease);
        }
    }
    
    public override string GetDescription()
    {
        return $"+{healthIncrease:F0} max health" + (healOnGain ? " and heal" : "");
    }
}
