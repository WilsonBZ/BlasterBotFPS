using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class Enemy : BaseEnemy, IDamageable
{
    [Header("Core Settings")]
    [SerializeField] private EnemyConfig config;
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, -0.9f, 0);
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckRadius = 0.4f;

    [Header("Health Settings")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2f, 0);

    [Header("Combat")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask playerLayer;
    [HideInInspector] public WaveSpawner spawner;

    [Header("Visuals")]
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private ExplosionFeedback explosionFeedback;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 numberOffset = new Vector3(0, 1.5f, 0);

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForceMultiplier = 3f;
    [SerializeField] private float knockbackRecoveryTime = 0.3f;
    [SerializeField] private float maxKnockbackSpeed = 15f;
    [Tooltip("Multiplier applied to move speed when hit (0.5 = 50% speed)")]
    [SerializeField] private float hitSlowMultiplier = 0.5f;
    [Tooltip("How long the slowdown lasts (seconds)")]
    [SerializeField] private float hitSlowDuration = 0.8f;
    [Tooltip("Optional smooth restore time after slowdown ends")]
    [SerializeField] private float hitSlowRestoreSmoothTime = 0.15f;

    private Slider healthSlider;
    private GameObject healthBarInstance;
    private float currentHealth;
    private Animator animator;
    private PlayerManager player;
    private Rigidbody rb;

    private EnemyState currentState;
    private float lastAttackTime;
    private bool isExploded;
    private bool isKnockedBack;
    private bool isGrounded;

    private float originalMoveSpeed;
    private float runtimeMoveSpeed;
    private Coroutine slowCoroutine;
    private HitFlashEffect hitFlashEffect;

    [Header("Obstacle Avoidance")]
    [Tooltip("Number of rays cast in a half-circle to detect obstacles ahead.")]
    [SerializeField] private int steeringRayCount = 9;
    [Tooltip("Half-angle of the steering fan in degrees (90 = full front hemisphere).")]
    [SerializeField] private float steeringFanAngle = 90f;
    [Tooltip("How far each steering ray reaches.")]
    [SerializeField] private float steeringRayLength = 2f;
    [Tooltip("Layers the steering rays collide with.")]
    [SerializeField] private LayerMask steeringObstacleMask = ~0;

    // Cached steering arrays — allocated once to avoid per-frame GC.
    private float[] steeringWeights;
    private Vector3 steeringMoveDir;

    // Steering throttle — only recalculate every N physics steps to halve raycast cost.
    private int steeringPhysicsFrame = 0;
    private const int SteeringInterval = 2;
    private Vector3 cachedBestDir;

    private readonly int animMoveSpeed = Animator.StringToHash("MoveSpeed");
    private readonly int animAttack = Animator.StringToHash("Attack");
    private readonly int animTakeDamage = Animator.StringToHash("TakeDamage");
    private readonly int animDie = Animator.StringToHash("Die");

    private void Awake()
    {
        InitializeComponents();
        InitializeRigidbody();
        InitializeFromConfig();
    }

    private void InitializeComponents()
    {
        // Auto-assign the Enemy layer so OverlapSphere exclusions work even
        // if the prefab was not updated manually.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;

        animator = GetComponent<Animator>();
        hitFlashEffect = GetComponent<HitFlashEffect>();

        if (hitFlashEffect == null)
            hitFlashEffect = gameObject.AddComponent<HitFlashEffect>();

        player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
            player = FindObjectOfType<PlayerManager>();

        if (player == null)
            Debug.LogWarning("Enemy: PlayerManager not found in scene.");

        steeringWeights = new float[steeringRayCount];
    }

    private void InitializeRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.freezeRotation = true;
        rb.isKinematic = false;
    }

    private void Start()
    {
        currentHealth = config.maxHealth;
        CreateHealthBar();
    }

    private void Update()
    {
        if (player == null || isExploded) return;

        // CheckSphere only in FixedUpdate — removed from here to avoid double cost.

        float sqrExplosionRange = config != null
            ? config.explosionTriggerRange * config.explosionTriggerRange
            : 0f;

        if (config != null && config.canExplode &&
            (transform.position - player.transform.position).sqrMagnitude <= sqrExplosionRange)
        {
            StartCoroutine(Explode());
            return;
        }

        UpdateStateMachine();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (player == null || isExploded || isKnockedBack) return;

        isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundMask);

        if (currentState == EnemyState.Chase && isGrounded)
            ApplySteeringMovement();
    }

    /// <summary>
    /// Context steering: casts a fan of rays, scores each direction by obstacle clearance
    /// and alignment with the player, then moves along the best direction.
    /// Raycasts are throttled to every <see cref="SteeringInterval"/> physics frames
    /// to halve raycast cost with multiple enemies in the scene.
    /// </summary>
    private void ApplySteeringMovement()
    {
        Vector3 toPlayer  = player.transform.position - transform.position;
        toPlayer.y = 0f;
        Vector3 desiredDir = toPlayer.normalized;

        // Only re-run the expensive raycast fan on throttled frames.
        steeringPhysicsFrame++;
        if (steeringPhysicsFrame >= SteeringInterval)
        {
            steeringPhysicsFrame = 0;

            float angleStep = steeringRayCount > 1
                ? steeringFanAngle * 2f / (steeringRayCount - 1)
                : 0f;

            float  bestScore = float.MinValue;
            Vector3 bestDir  = desiredDir;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

            for (int i = 0; i < steeringRayCount; i++)
            {
                float angle = -steeringFanAngle + angleStep * i;
                Vector3 candidate = Quaternion.AngleAxis(angle, Vector3.up) * desiredDir;

                float interest = Vector3.Dot(candidate, desiredDir);
                float danger   = 0f;

                if (Physics.Raycast(rayOrigin, candidate, out RaycastHit hit, steeringRayLength, steeringObstacleMask))
                    danger = 1f - (hit.distance / steeringRayLength);

                float score = interest - danger;
                steeringWeights[i] = score;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir   = candidate;
                }
            }

            cachedBestDir = bestDir;
        }

        steeringMoveDir = Vector3.Lerp(steeringMoveDir, cachedBestDir, Time.fixedDeltaTime * 8f);
        Vector3 target  = rb.position + steeringMoveDir * runtimeMoveSpeed * Time.fixedDeltaTime;
        target.y        = rb.position.y;
        rb.MovePosition(target);
    }

    private void CreateHealthBar()
    {
        if (healthBarPrefab == null) return;

        healthBarInstance = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity);
        healthBarInstance.transform.SetParent(transform);
        healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null) healthSlider.value = currentHealth / config.maxHealth;
    }

    private void InitializeFromConfig()
    {
        if (config == null)
        {
            Debug.LogWarning("Enemy: config is null on InitializeFromConfig.");
            originalMoveSpeed = 3.5f;
        }
        else
        {
            currentHealth = config.maxHealth;
            originalMoveSpeed = config.moveSpeed;
        }
        runtimeMoveSpeed = originalMoveSpeed;
    }

    private void UpdateStateMachine()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState(distanceToPlayer);
                break;
            case EnemyState.Chase:
                HandleChaseState(distanceToPlayer);
                break;
            case EnemyState.Attack:
                HandleAttackState(distanceToPlayer);
                break;
            case EnemyState.Dead:
                HandleDeadState();
                break;
        }

        // Always face the player on the XZ plane regardless of state.
        if (currentState != EnemyState.Dead && !isKnockedBack)
        {
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        float move01 = 0f;

        if (currentState == EnemyState.Chase && !isKnockedBack && isGrounded)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            move01 = horizontalVel.magnitude / Mathf.Max(0.01f, originalMoveSpeed);
        }

        animator.SetFloat(animMoveSpeed, move01, 0.1f, Time.deltaTime);
    }

    private void HandleIdleState(float distanceToPlayer)
    {
        if (player == null) return;

        if (config != null && distanceToPlayer <= config.detectionRange)
            TransitionToState(EnemyState.Chase);
    }

    private void HandleChaseState(float distanceToPlayer)
    {
        if (config == null) return;

        if (distanceToPlayer <= config.attackRange)
        {
            TransitionToState(EnemyState.Attack);
            return;
        }

        if (distanceToPlayer > config.chaseRange)
            TransitionToState(EnemyState.Idle);
        // Actual movement is in FixedUpdate via ApplySteeringMovement.
    }

    private void HandleAttackState(float distanceToPlayer)
    {
        if (config == null) return;

        if (distanceToPlayer > config.attackRange * 1.1f)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        if (Time.time >= lastAttackTime + config.attackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }
    }

    private void HandleDeadState()
    {
        if(isExploded) return;
        Die();
        
    }

    private void TransitionToState(EnemyState newState)
    {
        currentState = newState;
    }

    public void TakeDamage(float damage)
    {
        if (isExploded) return;

        currentHealth -= damage;
        UpdateHealthBar();

        if (hitFlashEffect != null)
        {
            hitFlashEffect.Flash();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (config != null && config.fleeOnHit)
            {
                StartCoroutine(FleeBehavior());
            }
        }

        ShowDamageNumber(damage);

        StartHitSlowdown();
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isExploded) return;

        currentHealth -= damage;
        UpdateHealthBar();

        if (hitFlashEffect != null)
        {
            hitFlashEffect.Flash();
        }

        Vector3 knockbackImpulse = hitDirection.normalized * knockbackForceMultiplier;
        ApplyKnockback(knockbackImpulse, knockbackRecoveryTime);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (config != null && config.fleeOnHit)
            {
                StartCoroutine(FleeBehavior());
            }
        }

        ShowDamageNumber(damage);
        StartHitSlowdown();
    }

    private void StartHitSlowdown()
    {
        if (hitSlowMultiplier >= 1f || hitSlowDuration <= 0f) return;

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        slowCoroutine = StartCoroutine(SlowdownCoroutine(hitSlowMultiplier, hitSlowDuration, hitSlowRestoreSmoothTime));
    }

    private IEnumerator SlowdownCoroutine(float multiplier, float duration, float smoothRestoreTime)
    {
        runtimeMoveSpeed = originalMoveSpeed * Mathf.Clamp(multiplier, 0f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (smoothRestoreTime > 0f)
        {
            float startSpeed = runtimeMoveSpeed;
            float t = 0f;
            while (t < smoothRestoreTime)
            {
                t += Time.deltaTime;
                runtimeMoveSpeed = Mathf.Lerp(startSpeed, originalMoveSpeed, Mathf.Clamp01(t / smoothRestoreTime));
                yield return null;
            }
        }

        runtimeMoveSpeed = originalMoveSpeed;
        slowCoroutine = null;
    }

    private void ApplyKnockback(Vector3 impulse, float stunDuration)
    {
        if (isExploded) return;

        if (impulse.magnitude > maxKnockbackSpeed)
        {
            impulse = impulse.normalized * maxKnockbackSpeed;
        }

        StartCoroutine(KnockbackCoroutine(impulse, stunDuration));
    }

    public void ApplyKnockbackWithDamage(Vector3 impulse, float damage, float stunDuration)
    {
        if (isExploded) return;

        TakeDamage(damage);

        if (impulse.magnitude > maxKnockbackSpeed)
        {
            impulse = impulse.normalized * maxKnockbackSpeed;
        }

        StartCoroutine(KnockbackCoroutine(impulse, stunDuration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 impulse, float duration)
    {
        if (isKnockedBack) yield break;
        isKnockedBack = true;

        rb.AddForce(impulse, ForceMode.Impulse);

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isKnockedBack = false;

        if (!isExploded)
            TransitionToState(EnemyState.Chase);
    }

    private void Attack()
    {
        animator.SetTrigger(animAttack);
    }

    public void OnAttackFrame()
    {
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, config.attackRadius, playerLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(config.attackDamage);
            }
        }
    }

    private void Die()
    {
        if (isExploded) return;

        isExploded = true;

        if (healthBarInstance)
            healthBarInstance.SetActive(false);

        HandleDeath();

        if (animator != null)
        {
            animator.SetTrigger(animDie);
        }

        foreach (var collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }

        if (deathEffect != null)
        {
           
        }
        
       

        float cleanupTime = config != null ? config.deathCleanupTime : 3f;
        Destroy(gameObject, cleanupTime);
    }

    private IEnumerator Explode()
    {
        if (isExploded)
        {
            yield break;
        }

        isExploded = true;

        if (animator != null)
        {
            animator.SetTrigger(animDie);
        }

        float delay = config != null && config.canExplode ? 0.5f : 0.5f;
        yield return new WaitForSeconds(delay);

        Vector3 explosionPosition = transform.position;

        if (explosionFeedback != null)
        {
            explosionFeedback.TriggerExplosion(explosionPosition);
        }

        GameObject effect = config != null && config.canExplode ? deathEffect : deathEffect;
        if (effect != null)
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Get(effect, explosionPosition, Quaternion.identity);
            else
                Instantiate(effect, explosionPosition, Quaternion.identity);
        }

        if (config != null && config.canExplode)
        {
            ApplyExplosionDamage();
        }

        HandleDeath();

        foreach (var collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }

        if (healthBarInstance)
        {
            healthBarInstance.SetActive(false);
        }

        float cleanupTime = config != null ? config.deathCleanupTime : 3f;
        Destroy(gameObject, cleanupTime);
    }

    private void ApplyExplosionDamage()
    {
        float range = config.explosionDamageRadius;
        float damage = config.explosionDamage;
        float force = config.explosionForce;

        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float damageMultiplier = 1f - Mathf.Clamp01(distance / range);
                damageable.TakeDamage(damage * damageMultiplier);

                Rigidbody rbHit = hit.GetComponent<Rigidbody>();
                if (rbHit != null)
                {
                    Vector3 direction = (hit.transform.position - transform.position).normalized;
                    rbHit.AddForce(direction * force, ForceMode.Impulse);
                }
            }
        }
    }

    private IEnumerator FleeBehavior()
    {
        Vector3 fleeDirection = (transform.position - player.transform.position).normalized;
        Vector3 dir = new Vector3(fleeDirection.x, 0f, fleeDirection.z).normalized;
        float elapsed = 0f;

        while (elapsed < config.fleeDuration)
        {
            elapsed += Time.deltaTime;
            rb.MovePosition(rb.position + dir * runtimeMoveSpeed * Time.deltaTime);
            yield return null;
        }

        if (!isExploded) TransitionToState(EnemyState.Chase);
    }

    private void ShowDamageNumber(float damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + numberOffset + Random.insideUnitSphere * 0.3f;
        GameObject number = PoolManager.Instance != null
            ? PoolManager.Instance.Get(damageNumberPrefab, spawnPos, Quaternion.identity)
            : Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

        number.transform.LookAt(Camera.main.transform);
        number.transform.Rotate(0, 180, 0);

        DamageNumber dn = number.GetComponent<DamageNumber>();
        if (dn) dn.Initialize(damage);
    }

    private void LateUpdate()
    {
        if (healthBarInstance)
        {
            healthBarInstance.transform.position = transform.position + healthBarOffset;
            healthBarInstance.transform.rotation = Camera.main.transform.rotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, config != null ? config.detectionRange : 1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, config != null ? config.chaseRange : 1f);

        if (attackPoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, config != null ? config.attackRadius : 1f);
        }

        if (config != null && config.canExplode)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, config.explosionTriggerRange);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, config.explosionDamageRadius);
        }

        // Steering ray gizmos
        if (player == null) return;
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return;

        Vector3 desired = toPlayer.normalized;
        float angleStep = steeringRayCount > 1 ? steeringFanAngle * 2f / (steeringRayCount - 1) : 0f;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < steeringRayCount; i++)
        {
            float angle = -steeringFanAngle + angleStep * i;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            float w = steeringWeights != null && steeringWeights.Length == steeringRayCount
                ? Mathf.Clamp01((steeringWeights[i] + 1f) / 2f)
                : 0.5f;
            Gizmos.color = Color.Lerp(Color.red, Color.green, w);
            Gizmos.DrawRay(origin, dir * steeringRayLength);
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        var camTarget = player.transform;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = (camTarget.position - origin).normalized;
        if (Physics.Raycast(origin, dir, out var hit, config != null ? config.chaseRange : 50f))
        {
            if (hit.collider != null)
            {
                if (hit.collider.transform.root == player.transform.root || hit.collider.GetComponent<PlayerManager>() != null) return true;
            }
            return false;
        }
        return false;
    }
}

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

