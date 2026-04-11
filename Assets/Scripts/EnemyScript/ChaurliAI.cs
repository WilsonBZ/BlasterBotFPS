using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tank enemy AI. Slowly advances toward the player, rotates its cannon turret
/// to track them, charges a heavy shot with a visual telegraph, then fires an
/// explosive projectile identical in behavior to the player's GazGun charge shot.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ChaurliAI : MonoBehaviour, IDamageable
{
    // ── Core ──────────────────────────────────────────────────────────────────

    [Header("Health")]
    [SerializeField] private float maxHealth = 350f;
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2.8f, 0f);

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [Tooltip("How fast the tank body rotates toward the player (deg/s).")]
    [SerializeField] private float bodyRotateSpeed = 40f;
    [SerializeField] private float detectionRange = 22f;
    [SerializeField] private float stopDistance = 10f;   // preferred firing range — stops here

    // ── Cannon ───────────────────────────────────────────────────────────────

    [Header("Cannon")]
    [Tooltip("The child Transform that is the cannon / turret — rotated to aim at the player.")]
    [SerializeField] private Transform cannonPivot;
    [Tooltip("Muzzle point — projectile spawns here.")]
    [SerializeField] private Transform firePoint;
    [Tooltip("How fast the cannon rotates toward the player on all axes (deg/s).")]
    [SerializeField] private float cannonRotateSpeed = 60f;

    // ── Firing ────────────────────────────────────────────────────────────────

    [Header("Firing")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float fireCooldown = 5f;
    [Tooltip("Seconds the cannon glows and tracks the player before releasing the shot.")]
    [SerializeField] private float chargeTime = 2f;
    [Tooltip("Charge glow light on the cannon muzzle — pulsed during the charge phase.")]
    [SerializeField] private Light chargeLight;
    [SerializeField] private float chargeLightMaxIntensity = 6f;

    // ── Hit Feedback ──────────────────────────────────────────────────────────

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("Death")]
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private float deathCleanupTime = 3f;

    // ── Private State ─────────────────────────────────────────────────────────

    private enum TankState { Idle, Advance, Charge, Dead }

    private float currentHealth;
    private TankState currentState = TankState.Idle;
    private PlayerManager player;
    private Rigidbody rb;
    private HitFlashEffect hitFlash;

    private Slider healthSlider;
    private GameObject healthBarInstance;

    private bool isCharging;
    private float lastFireTime = float.NegativeInfinity;
    private bool isKnockedBack;

    private const float GroundCheckDist = 1.2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        currentHealth = maxHealth;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;

        hitFlash = GetComponent<HitFlashEffect>();
        if (hitFlash == null)
            hitFlash = gameObject.AddComponent<HitFlashEffect>();

        if (chargeLight != null)
            chargeLight.intensity = 0f;
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
            Debug.LogWarning("ChaurliAI: PlayerManager not found in scene.");

        CreateHealthBar();
    }

    private void Update()
    {
        if (currentState == TankState.Dead || player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);

        RotateBodyTowardPlayer();
        RotateCannonTowardPlayer();
        UpdateStateMachine(dist);
    }

    private void FixedUpdate()
    {
        if (currentState != TankState.Advance || isKnockedBack || player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= stopDistance) return;

        Vector3 dir = (player.transform.position - transform.position);
        dir.y = 0f;
        dir.Normalize();

        Vector3 target = rb.position + dir * moveSpeed * Time.fixedDeltaTime;
        target.y       = rb.position.y;
        rb.MovePosition(target);
    }

    // ── State Machine ─────────────────────────────────────────────────────────

    private void UpdateStateMachine(float dist)
    {
        switch (currentState)
        {
            case TankState.Idle:
                if (dist <= detectionRange)
                    currentState = TankState.Advance;
                break;

            case TankState.Advance:
                if (isCharging) break;
                if (CanFire())
                    StartCoroutine(ChargeAndFire());
                break;
        }
    }

    private bool CanFire() =>
        Time.time >= lastFireTime + fireCooldown && !isCharging;

    // ── Rotation ──────────────────────────────────────────────────────────────

    /// <summary>Rotates the tank body on the Y axis toward the player.</summary>
    private void RotateBodyTowardPlayer()
    {
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, bodyRotateSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Rotates the cannon pivot to aim at the player on all axes,
    /// so vertical offset is also tracked.
    /// </summary>
    private void RotateCannonTowardPlayer()
    {
        if (cannonPivot == null) return;

        Vector3 dir = player.transform.position - cannonPivot.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        cannonPivot.rotation = Quaternion.RotateTowards(
            cannonPivot.rotation, target, cannonRotateSpeed * Time.deltaTime);
    }

    // ── Charge & Fire ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phase 1 — charge: pulse the muzzle light and keep tracking the player.
    /// Phase 2 — fire: lock the final aim direction, spawn the projectile.
    /// </summary>
    private IEnumerator ChargeAndFire()
    {
        isCharging = true;

        float elapsed = 0f;
        while (elapsed < chargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / chargeTime;

            // Ease-in glow so it starts dim and builds to full intensity.
            if (chargeLight != null)
                chargeLight.intensity = Mathf.Lerp(0f, chargeLightMaxIntensity, t * t);

            yield return null;
        }

        FireProjectile();

        if (chargeLight != null)
            chargeLight.intensity = 0f;

        lastFireTime = Time.time;
        isCharging   = false;
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;

        // Aim direction is whatever the cannon points at this exact frame.
        Vector3 aimDir = firePoint.forward;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(aimDir));
        ChaurliProjectile cp = proj.GetComponent<ChaurliProjectile>();
        if (cp != null)
            cp.Initialize(aimDir);
    }

    // ── IDamageable ───────────────────────────────────────────────────────────

    /// <summary>Takes damage without knockback (e.g. from AoE at range).</summary>
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position, Vector3.zero);
    }

    /// <summary>Takes damage with optional knockback direction.</summary>
    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (currentState == TankState.Dead) return;

        currentHealth -= damage;
        UpdateHealthBar();
        hitFlash?.Flash();

        if (hitDirection != Vector3.zero)
            StartCoroutine(ApplyKnockback(hitDirection.normalized * knockbackForce));

        if (currentHealth <= 0f)
            Die();
    }

    private IEnumerator ApplyKnockback(Vector3 impulse)
    {
        if (isKnockedBack) yield break;
        isKnockedBack = true;
        rb.AddForce(impulse, ForceMode.Impulse);
        yield return new WaitForSeconds(knockbackDuration);
        rb.linearVelocity = Vector3.zero;
        isKnockedBack = false;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void Die()
    {
        if (currentState == TankState.Dead) return;
        currentState = TankState.Dead;

        StopAllCoroutines();

        if (chargeLight != null)
            chargeLight.intensity = 0f;

        if (healthBarInstance != null)
            healthBarInstance.SetActive(false);

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (deathVFX != null)
            Instantiate(deathVFX, transform.position, Quaternion.identity);

        Destroy(gameObject, deathCleanupTime);
    }

    // ── Health Bar ────────────────────────────────────────────────────────────

    private void CreateHealthBar()
    {
        if (healthBarPrefab == null) return;

        healthBarInstance = Instantiate(
            healthBarPrefab,
            transform.position + healthBarOffset,
            Quaternion.identity);

        healthBarInstance.transform.SetParent(transform);
        healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth / maxHealth;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, stopDistance);
    }
}
