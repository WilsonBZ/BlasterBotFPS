using UnityEngine;

[CreateAssetMenu(fileName = "SpeedBoostEffect", menuName = "Buff System/Effects/Speed Boost")]
public class SpeedBoostEffect : BuffEffect
{
    public float speedIncrease = 2f;
    
    public override void Apply(PlayerManager player)
    {
        if (player == null) return;
        
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.walkSpeed += speedIncrease;
            movement.runSpeed += speedIncrease;
        }
    }
    
    public override string GetDescription()
    {
        return $"+{speedIncrease:F1} movement speed";
    }
}
