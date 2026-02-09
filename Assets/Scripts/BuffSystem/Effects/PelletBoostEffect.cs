using UnityEngine;

[CreateAssetMenu(fileName = "PelletBoostEffect", menuName = "Buff System/Effects/Pellet Boost")]
public class PelletBoostEffect : BuffEffect
{
    public int additionalPellets = 1;
    
    public override void Apply(PlayerManager player)
    {
        if (player == null) return;
        
        ModularWeapon[] weapons = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (ModularWeapon weapon in weapons)
        {
            weapon.AddPellets(additionalPellets);
        }
    }
    
    public override string GetDescription()
    {
        return $"+{additionalPellets} projectile{(additionalPellets > 1 ? "s" : "")} per shot";
    }
}
