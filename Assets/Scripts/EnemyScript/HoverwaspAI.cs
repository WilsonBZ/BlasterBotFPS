using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HoverwaspAI : BaseEnemy, IDamageable
{
    [Header("Core Settings")]
    [SerializeField] private EnemyConfig config;

    [Header("Spawn Delay")]
    [Tooltip("Seconds after spawning before the wasp begins detecting and targeting the player.")]
    [SerializeField] private float spawnActivationDelay = 1.5f;
    private bool isActivated = false;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    private static readonly int AnimIsFlying = Animator.StringToHash("IsFlying");

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

    [Header("Burst Fire")]
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float burstInterval = 0.18f;

    [Header("Aim & Laser")]
    [SerializeField] private float aimDuration = 1.5f;
    [SerializeField] private float laserMaxDistance = 40f;
    [SerializeField] private Color laserColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float laserWidth = 0.04f;
    [SerializeField] private Material laserMaterial;

    [Header("Muzzle Flash")]
    [SerializeField] private MuzzleFlash muzzleFlash;

    [Header("Impact VFX")]
    [SerializeField] private GameObject impactSphere;

    [Header("Strafe Settings")]
    [SerializeField] private float strafeSpeed = 4f;
    [SerializeField] private float strafeChangeInterval = 2f;
    [SerializeField] private float strafeRadius = 2f;

    [Header("Health Settings")]

    [Header("Visuals")]
    [SerializeField] private GameObject deathEffect;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 numberOffset = new Vector3(0, 1f, 0);

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForceMultiplier = 2f;
    [SerializeField] private float knockbackRecoveryTime = 0.4f;
    [SerializeField] private float maxKnockbackSpeed = 12f;

    [Header("Shoot-While-Still Settings")]
    [Tooltip("The wasp will wait until horizontal speed drops below this before locking aim and firing.")]
    [SerializeField] private float stillSpeedThreshold = 0.6f;
    [Tooltip("Max seconds to wait for the wasp to stop before firing anyway.")]
    [SerializeField] private float stillWaitTimeout = 1.2f;
    [Tooltip("How aggressively horizontal velocity is killed when stopping to aim (higher = snappier stop).")]
    [SerializeField] private float brakeDamping = 14f;

    [Header("LOS Repositioning")]
    [Tooltip("How fast the wasp rotates/strafes to find a clear line of sight to the player.")]
    [SerializeField] private float losRepositionSpeed = 4f;
    [Tooltip("Seconds to keep trying to reposition before giving up and returning to Idle.")]
    [SerializeField] private float losRepositionTimeout = 3f;

    private bool isRepositioning = false;
    private float losRepositionTimer = 0f;
    private float currentHealth;
    private PlayerManager player;
    private Rigidbody rb;

    private HoverwaspState currentState;
    private float lastFireTime;
    private bool isExploded;
    private bool isKnockedBack;
    private bool isShooting;

    private Vector3 strafeDirection;
    private float strafeTimer;
    private float preferredDistance;
    private HitFlashEffect hitFlashEffect;
    private float targetHoverHeight;
    private float verticalChangeTimer;
    private int fireSoundIndex;

    private LineRenderer laserRenderer;
    private Vector3 lockedAimDirection;

    // Hover raycast throttle — re-check height every N physics steps.
    private int   hoverPhysicsFrame = 0;
    private const int HoverInterval = 3;
    private bool  cachedHasGround;
    private float cachedGroundDistance;

    // Proximity avoidance throttle — re-check neighbours every N physics steps.
    private int proximityFrame = 0;
    private const int ProximityInterval = 4;

    private void Awake()
    {
        InitializeComponents();
        InitializeRigidbody();
        InitializeLaser();
        InitializeFromConfig();
    }

    private void InitializeComponents()
    {
        // Auto-assign the Enemy layer so OverlapSphere exclusions work even
        // if the prefab was not updated manually.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;

        hitFlashEffect = GetComponent<HitFlashEffect>();

        if (hitFlashEffect == null)
            hitFlashEffect = gameObject.AddComponent<HitFlashEffect>();

        // Animator lives on the Hoverwasp_Avatar child that owns the skeleton.
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
            player = FindObjectOfType<PlayerManager>();

        if (player == null)
            Debug.LogWarning("HoverwaspAI: PlayerManager not found in scene.");
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

    private void InitializeLaser()
    {
        laserRenderer = gameObject.AddComponent<LineRenderer>();
        laserRenderer.positionCount = 2;
        laserRenderer.startWidth = laserWidth;
        laserRenderer.endWidth = laserWidth * 0.5f;
        laserRenderer.useWorldSpace = true;
        laserRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (laserMaterial != null)
        {
            Material mat = new Material(laserMaterial);
            mat.SetColor("_BaseColor", laserColor);
            laserRenderer.material = mat;
        }
        else
        {
            Debug.LogError("HoverwaspAI: laserMaterial is not assigned. Assign VFX_Laser.mat in the Inspector.", this);
        }

        laserRenderer.enabled = false;
    }

    private void Start()
    {
        currentHealth = config != null ? config.maxHealth : 50f;
        ChooseNewStrafeDirection();
        targetHoverHeight = hoverHeight + Random.Range(-verticalVariationAmount, verticalVariationAmount);

        if (animator != null)
            animator.SetBool(AnimIsFlying, true);

        StartCoroutine(ActivateAfterDelay());
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(spawnActivationDelay);
        isActivated = true;
    }

    private void Update()
    {
        if (player == null || isExploded) return;

        if (isActivated)
        {
            UpdateStateMachine();
            UpdateStrafeTimer();
        }

        UpdateVerticalVariation();
        UpdateLaser();
    }

    private void FixedUpdate()
    {
        if (player == null || isExploded || isKnockedBack) return;

        ApplyHoverForce();
        ApplyProximityAvoidance();

        if (!isActivated) return;

        // Freeze horizontal movement while aiming or shooting
        if (currentState == HoverwaspState.Aiming || currentState == HoverwaspState.Shooting)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0f, Time.fixedDeltaTime * brakeDamping);
            vel.z = Mathf.Lerp(vel.z, 0f, Time.fixedDeltaTime * brakeDamping);
            rb.linearVelocity = vel;
            return;
        }

        if (currentState == HoverwaspState.Combat)
        {
            MoveTowardsPreferredDistance();

            if (isRepositioning)
            {
                // Strafe laterally with boosted speed until LOS is clear
                rb.AddForce(strafeDirection * losRepositionSpeed, ForceMode.Acceleration);
            }
            else
            {
                ApplyStrafeMovement();
            }
        }

        LookAtPlayer();
    }

    private void ApplyHoverForce()
    {
        // Throttle the downward raycast — only re-check every HoverInterval physics steps.
        hoverPhysicsFrame++;
        if (hoverPhysicsFrame >= HoverInterval)
        {
            hoverPhysicsFrame  = 0;
            cachedHasGround    = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, heightCheckDistance, groundMask);
            cachedGroundDistance = cachedHasGround ? hit.distance : heightCheckDistance;
        }

        if (cachedHasGround)
        {
            float heightError = targetHoverHeight - cachedGroundDistance;
            float upForce     = heightError * hoverForce - rb.linearVelocity.y * hoverDamping;
            rb.AddForce(Vector3.up * upForce, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.up * hoverForce * 0.5f, ForceMode.Acceleration);
        }
    }

    private void ApplyProximityAvoidance()
    {
        proximityFrame++;
        if (proximityFrame < ProximityInterval) return;
        proximityFrame = 0;

        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, proximityCheckRadius, ~0, QueryTriggerInteraction.Ignore);

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
        if (currentState == HoverwaspState.Aiming || currentState == HoverwaspState.Shooting) return;

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
            case HoverwaspState.Aiming:
            case HoverwaspState.Shooting:
                // Driven by AimAndBurst coroutine
                break;
            case HoverwaspState.Dead:
                break;
        }
    }

    private void HandleIdleState(float distanceToPlayer)
    {
        float detectionRange = config != null ? config.detectionRange : 15f;
        if (distanceToPlayer <= detectionRange && HasLineOfSightToPlayer())
            currentState = HoverwaspState.Combat;
    }

    private void HandleCombatState(float distanceToPlayer)
    {
        float chaseRange = config != null ? config.chaseRange : 20f;
        if (distanceToPlayer > chaseRange)
        {
            isRepositioning = false;
            losRepositionTimer = 0f;
            currentState = HoverwaspState.Idle;
            return;
        }

        if (!HasLineOfSightToPlayer())
        {
            if (!isRepositioning)
            {
                isRepositioning = true;
                losRepositionTimer = 0f;
                // Pick a new lateral strafe dir to reposition around cover
                ChooseNewStrafeDirection();
            }

            losRepositionTimer += Time.deltaTime;
            if (losRepositionTimer >= losRepositionTimeout)
            {
                isRepositioning = false;
                losRepositionTimer = 0f;
                currentState = HoverwaspState.Idle;
            }
            return;
        }

        // LOS restored
        isRepositioning = false;
        losRepositionTimer = 0f;

        float fireCooldown = config != null ? config.fireCooldown : 2f;
        if (!isShooting && Time.time >= lastFireTime + fireCooldown)
            StartCoroutine(AimAndBurst());
    }

    private IEnumerator AimAndBurst()
    {
        if (isShooting || firePoint == null) yield break;

        isShooting   = true;
        currentState = HoverwaspState.Aiming;

        // ── Phase 1: brake to a stop ──────────────────────────────────────────
        float stillTimer = 0f;
        while (stillTimer < stillWaitTimeout)
        {
            Vector3 hVel = rb.linearVelocity;
            hVel.y = 0f;
            if (hVel.magnitude <= stillSpeedThreshold) break;
            stillTimer += Time.deltaTime;
            yield return null;
        }

        // ── Phase 2: laser telegraph — track the player in real time ──────────
        // lockedAimDirection is updated every frame here AND in UpdateLaser,
        // so the red ray honestly shows where the shot will land.
        float aimTimer = 0f;
        while (aimTimer < aimDuration)
        {
            if (player != null && firePoint != null)
            {
                lockedAimDirection = (player.transform.position - firePoint.position).normalized;

                // Keep the body facing the player during the telegraph.
                Vector3 flatDir = player.transform.position - transform.position;
                flatDir.y = 0f;
                if (flatDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(flatDir),
                        Time.deltaTime * 12f);
            }

            aimTimer += Time.deltaTime;
            yield return null;
        }

        // ── Phase 3: snap-lock direction at the exact moment of firing ────────
        if (player != null && firePoint != null)
            lockedAimDirection = (player.transform.position - firePoint.position).normalized;

        // ── Phase 4: burst fire ───────────────────────────────────────────────
        currentState          = HoverwaspState.Shooting;
        laserRenderer.enabled = false;

        for (int i = 0; i < burstCount; i++)
        {
            if (isExploded) break;
            FireSingleShot();
            yield return new WaitForSeconds(burstInterval);
        }

        lastFireTime = Time.time;
        isShooting   = false;
        currentState = HoverwaspState.Combat;
    }

    private void FireSingleShot()
    {
        if (config == null || config.projectilePrefab == null || firePoint == null) return;

        Quaternion rot = Quaternion.LookRotation(lockedAimDirection);
        GameObject projectile = PoolManager.Instance != null
            ? PoolManager.Instance.Get(config.projectilePrefab, firePoint.position, rot)
            : Instantiate(config.projectilePrefab, firePoint.position, rot);

        EnemyProjectile ep = projectile.GetComponent<EnemyProjectile>();
        if (ep != null)
        {
            float pitch = fireSoundIndex % 2 == 0 ? 0.9f : 1.1f;
            ep.SetPitch(pitch);
            fireSoundIndex++;
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();
    }

    private void UpdateLaser()
    {
        if (laserRenderer == null || firePoint == null) return;

        if (currentState != HoverwaspState.Aiming)
        {
            laserRenderer.enabled = false;
            return;
        }

        laserRenderer.enabled = true;

        Vector3 end = firePoint.position + lockedAimDirection * laserMaxDistance;
        if (Physics.Raycast(firePoint.position, lockedAimDirection, out RaycastHit hit, laserMaxDistance))
            end = hit.point;

        laserRenderer.SetPosition(0, firePoint.position);
        laserRenderer.SetPosition(1, end);
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

        if (hitFlashEffect != null)
            hitFlashEffect.Flash();

        if (currentHealth <= 0)
            Die();

        ShowDamageNumber(damage);
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isExploded) return;

        currentHealth -= damage;

        if (hitFlashEffect != null)
            hitFlashEffect.Flash();

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
        isShooting = false;

        if (laserRenderer != null) laserRenderer.enabled = false;

        HandleDeath();

        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        if (deathEffect != null)
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Get(deathEffect, transform.position, Quaternion.identity);
            else
                Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        float cleanupTime = config != null ? config.deathCleanupTime : 3f;
        Destroy(gameObject, cleanupTime);
    }

    private void ShowDamageNumber(float damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + numberOffset + Random.insideUnitSphere * 0.3f;
        GameObject number = PoolManager.Instance != null
            ? PoolManager.Instance.Get(damageNumberPrefab, spawnPos, Quaternion.identity)
            : Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

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
    Aiming,
    Shooting,
    Dead
}
