using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class HoverwaspAI : BaseEnemy, IDamageable
{
    [Header("Core Settings")]
    [SerializeField] private EnemyConfig config;

    [Header("Hover Settings")]
    [SerializeField] private float hoverHeight = 3f;
    [SerializeField] private float hoverForce = 15f;
    [SerializeField] private float hoverDamping = 5f;
    [SerializeField] private float heightCheckDistance = 10f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float verticalVariationSpeed = 1f;
    [SerializeField] private float verticalVariationAmount = 1.5f;
    [SerializeField] private float verticalChangeInterval = 3f;
    
    [Header("Proximity Avoidance")]
    [SerializeField] private float proximityCheckRadius = 3f;
    [SerializeField] private float verticalSeparationForce = 5f;
    [SerializeField] private float minVerticalSeparation = 2f;

    [Header("Combat")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private LayerMask playerLayer;
    [HideInInspector] public WaveSpawner spawner;

    [Header("Strafe Settings")]
    [SerializeField] private float strafeSpeed = 4f;
    [SerializeField] private float strafeChangeInterval = 2f;
    [SerializeField] private float strafeRadius = 2f;

    [Header("Health Settings")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2f, 0);

    [Header("Visuals")]
    [SerializeField] private GameObject deathEffect;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 numberOffset = new Vector3(0, 1f, 0);

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForceMultiplier = 2f;
    [SerializeField] private float knockbackRecoveryTime = 0.4f;
    [SerializeField] private float maxKnockbackSpeed = 12f;

    private Slider healthSlider;
    private GameObject healthBarInstance;
    private float currentHealth;
    private PlayerManager player;
    private Rigidbody rb;

    private HoverwaspState currentState;
    private float lastFireTime;
    private bool isExploded;
    private bool isKnockedBack;

    private Vector3 strafeDirection;
    private float strafeTimer;
    private float preferredDistance;
    private HitFlashEffect hitFlashEffect;
    private float targetHoverHeight;
    private float verticalChangeTimer;
    private int fireSoundIndex;

    private void Awake()
    {
        InitializeComponents();
        InitializeRigidbody();
        InitializeFromConfig();
    }

    private void InitializeComponents()
    {
        hitFlashEffect = GetComponent<HitFlashEffect>();
        
        if (hitFlashEffect == null)
        {
            hitFlashEffect = gameObject.AddComponent<HitFlashEffect>();
        }

        player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
        {
            player = FindObjectOfType<PlayerManager>();
        }

        if (player == null)
        {
            Debug.LogWarning("HoverwaspAI: PlayerManager not found in scene.");
        }
    }

    private void InitializeRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
    }

    private void InitializeFromConfig()
    {
        if (config == null)
        {
            Debug.LogWarning("HoverwaspAI: config is null.");
            currentHealth = 50f;
            preferredDistance = 10f;
        }
        else
        {
            currentHealth = config.maxHealth;
            preferredDistance = config.isRanged ? config.preferredDistance : 8f;
        }
    }

    private void Start()
    {
        currentHealth = config != null ? config.maxHealth : 50f;
        CreateHealthBar();
        ChooseNewStrafeDirection();
        targetHoverHeight = hoverHeight + Random.Range(-verticalVariationAmount, verticalVariationAmount);
    }

    private void Update()
    {
        if (player == null || isExploded) return;

        UpdateStateMachine();
        UpdateStrafeTimer();
        UpdateVerticalVariation();
        UpdateHealthBarPosition();
    }

    private void FixedUpdate()
    {
        if (player == null || isExploded || isKnockedBack) return;

        ApplyHoverForce();
        ApplyProximityAvoidance();

        if (currentState == HoverwaspState.Combat)
        {
            MoveTowardsPreferredDistance();
            ApplyStrafeMovement();
        }

        LookAtPlayer();
    }

    private void ApplyHoverForce()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, heightCheckDistance, groundMask))
        {
            float currentHeight = hit.distance;
            float heightError = targetHoverHeight - currentHeight;

            float upForce = heightError * hoverForce;
            upForce -= rb.linearVelocity.y * hoverDamping;

            rb.AddForce(Vector3.up * upForce, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.up * hoverForce * 0.5f, ForceMode.Acceleration);
        }
    }

    private void ApplyProximityAvoidance()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, proximityCheckRadius);
        
        foreach (Collider col in nearbyEnemies)
        {
            if (col.gameObject == gameObject) continue;
            
            HoverwaspAI otherWasp = col.GetComponent<HoverwaspAI>();
            if (otherWasp != null && !otherWasp.isExploded)
            {
                float verticalDistance = transform.position.y - otherWasp.transform.position.y;
                
                if (Mathf.Abs(verticalDistance) < minVerticalSeparation)
                {
                    float separationDirection = verticalDistance >= 0 ? 1f : -1f;
                    rb.AddForce(Vector3.up * separationDirection * verticalSeparationForce, ForceMode.Acceleration);
                }
            }
        }
    }

    private void UpdateVerticalVariation()
    {
        verticalChangeTimer += Time.deltaTime;
        
        if (verticalChangeTimer >= verticalChangeInterval)
        {
            targetHoverHeight = hoverHeight + Random.Range(-verticalVariationAmount, verticalVariationAmount);
            verticalChangeTimer = 0f;
        }
    }

    private void MoveTowardsPreferredDistance()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0;
        float distanceToPlayer = toPlayer.magnitude;

        float distanceError = distanceToPlayer - preferredDistance;

        if (Mathf.Abs(distanceError) > 1f)
        {
            Vector3 moveDirection = toPlayer.normalized;
            
            if (distanceError < 0)
            {
                moveDirection = -moveDirection;
            }

            float moveSpeed = config != null ? config.moveSpeed : 3.5f;
            rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);
        }
    }

    private void ApplyStrafeMovement()
    {
        Vector3 strafeForce = strafeDirection * strafeSpeed;
        rb.AddForce(strafeForce, ForceMode.Acceleration);
    }

    private void UpdateStrafeTimer()
    {
        strafeTimer += Time.deltaTime;
        if (strafeTimer >= strafeChangeInterval)
        {
            ChooseNewStrafeDirection();
            strafeTimer = 0f;
        }
    }

    private void ChooseNewStrafeDirection()
    {
        if (player == null) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0;
        toPlayer.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, toPlayer);

        float randomChoice = Random.value;
        if (randomChoice < 0.4f)
        {
            strafeDirection = right;
        }
        else if (randomChoice < 0.8f)
        {
            strafeDirection = -right;
        }
        else
        {
            strafeDirection = Vector3.zero;
        }
    }

    private void LookAtPlayer()
    {
        if (player == null) return;

        Vector3 direction = (player.transform.position - transform.position);
        direction.y = 0;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 8f);
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
        if (healthSlider != null)
        {
            float maxHealth = config != null ? config.maxHealth : 50f;
            healthSlider.value = currentHealth / maxHealth;
        }
    }

    private void UpdateHealthBarPosition()
    {
        if (healthBarInstance && Camera.main != null)
        {
            healthBarInstance.transform.position = transform.position + healthBarOffset;
            healthBarInstance.transform.rotation = Camera.main.transform.rotation;
        }
    }

    private void UpdateStateMachine()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        switch (currentState)
        {
            case HoverwaspState.Idle:
                HandleIdleState(distanceToPlayer);
                break;
            case HoverwaspState.Combat:
                HandleCombatState(distanceToPlayer);
                break;
            case HoverwaspState.Dead:
                break;
        }
    }

    private void HandleIdleState(float distanceToPlayer)
    {
        float detectionRange = config != null ? config.detectionRange : 15f;
        if (distanceToPlayer <= detectionRange && HasLineOfSightToPlayer())
        {
            currentState = HoverwaspState.Combat;
        }
    }

    private void HandleCombatState(float distanceToPlayer)
    {
        float chaseRange = config != null ? config.chaseRange : 20f;
        if (distanceToPlayer > chaseRange || !HasLineOfSightToPlayer())
        {
            currentState = HoverwaspState.Idle;
            return;
        }

        float fireCooldown = config != null ? config.fireCooldown : 1.5f;
        if (Time.time >= lastFireTime + fireCooldown)
        {
            FireProjectile();
            lastFireTime = Time.time;
        }
    }

    private void FireProjectile()
    {
        if (config == null || config.projectilePrefab == null || firePoint == null) return;

        Vector3 directionToPlayer = (player.transform.position - firePoint.position).normalized;
        Quaternion projectileRotation = Quaternion.LookRotation(directionToPlayer);

        GameObject projectile = Instantiate(config.projectilePrefab, firePoint.position, projectileRotation);
        
        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            float pitch = fireSoundIndex % 2 == 0 ? 0.9f : 1.1f;
            proj.SetPitch(pitch);
            fireSoundIndex++;
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;

        Vector3 origin = transform.position;
        Vector3 directionToPlayer = (player.transform.position - origin).normalized;
        float maxDistance = config != null ? config.chaseRange : 20f;

        if (Physics.Raycast(origin, directionToPlayer, out RaycastHit hit, maxDistance))
        {
            if (hit.collider != null)
            {
                if (hit.collider.transform.root == player.transform.root || 
                    hit.collider.GetComponent<PlayerManager>() != null)
                {
                    return true;
                }
            }
            return false;
        }
        return false;
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

        ShowDamageNumber(damage);
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

        ShowDamageNumber(damage);
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

        rb.linearVelocity *= 0.5f;
        isKnockedBack = false;

        if (!isExploded)
        {
            currentState = HoverwaspState.Combat;
        }
    }

    private void Die()
    {
        if (isExploded) return;
        isExploded = true;

        if (healthBarInstance)
        {
            healthBarInstance.SetActive(false);
        }

        HandleDeath();

        foreach (var collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        float cleanupTime = config != null ? config.deathCleanupTime : 3f;
        Destroy(gameObject, cleanupTime);
    }

    private void ShowDamageNumber(float damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + numberOffset + Random.insideUnitSphere * 0.3f;
        GameObject number = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

        if (Camera.main != null)
        {
            number.transform.LookAt(Camera.main.transform);
            number.transform.Rotate(0, 180, 0);
        }

        DamageNumber dn = number.GetComponent<DamageNumber>();
        if (dn) dn.Initialize(damage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float detectionRange = config != null ? config.detectionRange : 15f;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        float chaseRange = config != null ? config.chaseRange : 20f;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        if (firePoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(firePoint.position, 0.3f);
        }

        Gizmos.color = Color.green;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, heightCheckDistance, groundMask))
        {
            Gizmos.DrawLine(transform.position, hit.point);
            Gizmos.DrawWireSphere(hit.point, 0.2f);
        }
    }
}

public enum HoverwaspState
{
    Idle,
    Combat,
    Dead
}
