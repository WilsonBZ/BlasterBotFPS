using UnityEngine;

/// <summary>
/// Grants the player one additional air jump per application.
/// </summary>
[CreateAssetMenu(fileName = "ExtraJumpEffect", menuName = "Buff System/Effects/Extra Jump")]
public class ExtraJumpEffect : BuffEffect
{
    [Tooltip("How many extra air jumps to add.")]
    public int extraJumps = 1;

    public override void Apply(PlayerManager player)
    {
        if (player == null) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.maxAirJumps += extraJumps;
    }

    public override string GetDescription() =>
        $"+{extraJumps} air jump{(extraJumps > 1 ? "s" : "")}";
}
