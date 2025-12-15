using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    [SerializeField] private Renderer[] materialRenderers;
    [SerializeField] private GameObject deathEffect;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 numberOffset = new Vector3(0, 1.5f, 0);

    [Header("Explosion Settings")]
    [SerializeField] private float explosionRange = 2f;
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float selfDestructDelay = 0.5f;

    [Header("Hit Slowdown")]
    [Tooltip("Multiplier applied to move speed when hit (0.5 = 50% speed)")]
    [SerializeField] private float hitSlowMultiplier = 0.5f;
    [Tooltip("How long the slowdown lasts (seconds)")]
    [SerializeField] private float hitSlowDuration = 0.8f;
    [Tooltip("Optional smooth restore time after slowdown ends")]
    [SerializeField] private float hitSlowRestoreSmoothTime = 0.15f;

    private Slider healthSlider;
    private GameObject healthBarInstance;
    private float currentHealth;

    [SerializeField] private Animator animator;
    private PlayerManager player;

    private EnemyState currentState;
    private float lastAttackTime;
    private bool isExploded;

    private readonly int animMoveSpeed = Animator.StringToHash("MoveSpeed");
    private readonly int animAttack = Animator.StringToHash("Attack");
    private readonly int animTakeDamage = Animator.StringToHash("TakeDamage");
    private readonly int animDie = Animator.StringToHash("Die");

    private Rigidbody rb;
    private bool isKnockedBack = false;
    private bool isGrounded;

    [Header("Knockback / Stun")]
    [SerializeField] private float defaultKnockbackRecovery = 0.6f;
    [SerializeField] private float maxAllowedKnockbackSpeed = 15f;

    private MethodInfo mi_FireInternal;
    private FieldInfo fi_lastShotTime;

    public List<GameObject> WeaponSockets;

    private float originalMoveSpeed;
    private float runtimeMoveSpeed;
    private Coroutine slowCoroutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Try multiple lookups for player to be robust in build
        player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
            player = FindObjectOfType<PlayerManager>();
        if (player == null)
            Debug.LogWarning("Enemy: PlayerManager not found in scene. Enemy will be inactive until a PlayerManager is present.");

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.freezeRotation = true;
        rb.isKinematic = false; 

        InitializeFromConfig();

        var mwType = typeof(ModularWeapon);
        mi_FireInternal = mwType.GetMethod("FireInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fi_lastShotTime = mwType.GetField("lastShotTime", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void Start()
    {
        currentHealth = config.maxHealth;
        CreateHealthBar();
    }

    private void Update()
    {
        // if no player reference, do nothing (prevents nullref in build) — log helps debugging
        if (player == null)
        {
            return;
        }

        if (isExploded) return;

        isGrounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundMask);

        if (config != null && config.canExplode && Vector3.Distance(transform.position, player.transform.position) <= config.explosionRange)
        {
            StartCoroutine(Explode());
            return;
        }

        UpdateStateMachine();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        // physics-based movement: only applied during Chase when not knocked back or attacking
        if (player == null || isExploded || isKnockedBack) return;

        if (currentState == EnemyState.Chase)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            Vector3 dir = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;
            if (dir.sqrMagnitude > 0.001f && isGrounded)
            {
                float moveStep = runtimeMoveSpeed * Time.fixedDeltaTime;
                Vector3 target = rb.position + dir * moveStep;
                rb.MovePosition(target);
            }
        }
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

        if (currentState != EnemyState.Dead && !isKnockedBack)
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
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
        if (config != null && distanceToPlayer <= config.detectionRange && HasLineOfSightToPlayer())
        {
            TransitionToState(EnemyState.Chase);
        }
    }

    private void HandleChaseState(float distanceToPlayer)
    {
        if (config == null) return;

        if (distanceToPlayer <= config.attackRange)
        {
            TransitionToState(EnemyState.Attack);
            return;
        }

        if (distanceToPlayer > config.chaseRange || !HasLineOfSightToPlayer())
        {
            TransitionToState(EnemyState.Idle);
        }
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

    private void HandleDeadState() { }

    private void TransitionToState(EnemyState newState)
    {
        currentState = newState;
    }

    public void TakeDamage(float damage)
    {
        if (isExploded) return;

        currentHealth -= damage;
        UpdateHealthBar();

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

    public void ApplyKnockback(Vector3 impulse, float damage = 0f, float stunDuration = -1f)
    {
        if (isExploded) return;

        if (stunDuration <= 0f) stunDuration = defaultKnockbackRecovery;

        if (impulse.magnitude > maxAllowedKnockbackSpeed)
            impulse = impulse.normalized * maxAllowedKnockbackSpeed;

        if (damage > 0f) TakeDamage(damage);

        StartCoroutine(KnockbackCoroutine(impulse, stunDuration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 impulse, float duration)
    {
        if (isKnockedBack) yield break;
        isKnockedBack = true;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;

        rb.AddForce(impulse, ForceMode.Impulse);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.zero;

        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 

        isKnockedBack = false;

        if (!isExploded) TransitionToState(EnemyState.Chase);
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
        isExploded = true;
        if (healthBarInstance) healthBarInstance.SetActive(false);

        HandleDeath();

        animator.SetTrigger(animDie);

        foreach (var collider in GetComponents<Collider>()) collider.enabled = false;

        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);
        Destroy(gameObject, config.deathCleanupTime);
    }

    private IEnumerator Explode()
    {
        if (isExploded) yield break;
        isExploded = true;
        animator.SetTrigger(animDie);

        yield return new WaitForSeconds(selfDestructDelay);

        if (explosionEffect) Instantiate(explosionEffect, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRange);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float damageMultiplier = 1 - Mathf.Clamp01(distance / explosionRange);
                damageable.TakeDamage(explosionDamage * damageMultiplier);

                Rigidbody rbHit = hit.GetComponent<Rigidbody>();
                if (rbHit) rbHit.AddForce((hit.transform.position - transform.position).normalized * explosionForce, ForceMode.Impulse);
            }
        }

        HandleDeath();
        Die();
    }

    private IEnumerator FleeBehavior()
    {
        Vector3 fleeDirection = (transform.position - player.transform.position).normalized;
        Vector3 fleePosition = transform.position + fleeDirection * config.fleeDistance;

        Vector3 dir = (fleePosition - transform.position).normalized;
        float elapsed = 0f;
        float fleeTime = config.fleeDuration;
        while (elapsed < fleeTime)
        {
            elapsed += Time.deltaTime;
            if (isGrounded)
            {
                rb.MovePosition(rb.position + dir * runtimeMoveSpeed * Time.deltaTime);
            }
            yield return null;
        }

        if (!isExploded) TransitionToState(EnemyState.Chase);
    }

    private void ShowDamageNumber(float damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + numberOffset + Random.insideUnitSphere * 0.3f;
        GameObject number = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

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

