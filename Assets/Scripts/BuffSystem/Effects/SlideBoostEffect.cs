using UnityEngine;

/// <summary>
/// Increases the player's slide speed, making slides cover more ground and deal
/// more knockback to enemies on collision (since knockback uses velocity magnitude).
/// </summary>
[CreateAssetMenu(fileName = "SlideBoostEffect", menuName = "Buff System/Effects/Slide Boost")]
public class SlideBoostEffect : BuffEffect
{
    [Tooltip("Flat increase to slideSpeed.")]
    public float speedIncrease = 8f;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.slideSpeed += speedIncrease;
    }

    public override string GetDescription() =>
        $"Slide speed +{speedIncrease:F0} (also boosts slide knockback)";
}
