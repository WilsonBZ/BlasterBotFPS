using UnityEngine;

public static class GameConstants
{
    public const float GROUND_STICK_VELOCITY = -2f;
    public const float DASH_KNOCKBACK_MULTIPLIER = 1.2f;
    public const float DASH_DAMAGE = 20f;
    public const float DASH_KNOCKBACK_DURATION = 0.6f;
    public const float DASH_KNOCKBACK_VERTICAL_LIFT = 0.5f;
    public const float KNOCKBACK_VELOCITY_REDUCTION = 0.15f;
}

public static class LayerMasks
{
    private static LayerMask? _ground;
    private static LayerMask? _player;
    private static LayerMask? _enemy;

    public static LayerMask Ground => _ground ??= LayerMask.GetMask("Ground");
    public static LayerMask Player => _player ??= LayerMask.GetMask("Player");
    public static LayerMask Enemy => _enemy ??= LayerMask.GetMask("Enemy");
}
