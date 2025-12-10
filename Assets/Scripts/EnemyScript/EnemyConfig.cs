using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Ranged Combat")]
    public bool isRanged = true;
    public float preferredDistance = 10f;
    public float fireCooldown = 1.5f;
    public GameObject projectilePrefab;

    [Header("Explosion Settings")]
    public bool canExplode = true;
    public float explosionRange = 2f;
    public float explosionDamage = 50f;
    public float explosionForce = 10f;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float acceleration = 8f;
    public float detectionRange = 15f;
    public float chaseRange = 20f;
    public float fleeDistance = 10f;
    public float fleeDuration = 3f;
    public bool fleeOnHit = true;

    [Header("Combat")]
    public float attackDamage = 15f;
    public float attackRange = 2f;
    public float attackRadius = 1.5f;
    public float attackCooldown = 2f;

    [Header("Death")]
    public float deathCleanupTime = 3f;
}
