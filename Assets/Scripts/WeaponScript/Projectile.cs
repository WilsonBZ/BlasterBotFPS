using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float Speed = 60f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float damage = 15f;

    [Header("Collision Settings")]
    [SerializeField] private Collider projectileCollider;
    [SerializeField] private float collisionCheckRadius = 0.2f;

    [Header("Effects")]
    [SerializeField] private GameObject spawnEffect;
    [SerializeField] private GameObject impactSphere;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float hitStopDuration = 0.03f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    [Header("Homing")]
    [Tooltip("Enables homing toward the nearest enemy. Activated by the Homing Sting buff.")]
    public bool homingEnabled = false;
    [Tooltip("Seconds after firing before the homing sphere activates.")]
    public float homingActivationDelay = 0.15f;
    [Tooltip("Radius of the rolling detection sphere that travels with the projectile.")]
    public float homingSearchRadius = 8f;
    [Tooltip("Degrees per second the projectile can steer toward its target.")]
    public float homingTurnSpeed = 180f;

    private Rigidbody rb;
    private int nonPlayerLayerMask;
    private int playerLayer;
    private int ringLayer;
    private Poolable poolable;
    private bool hasHit;
    private Coroutine lifetimeCoroutine;

    // Homing state
    private Transform homingTarget;
    private bool homingActive;        // true once the activation delay has elapsed
    private Vector3 straightVelocity; // velocity captured at fire time, restored when target is lost

    private static int ignoreRaycastLayer = -1;

    /// <summary>Returns true if the GameObject belongs to the player or ring-mounted equipment.</summary>
    private bool IsPlayer(GameObject go) =>
        go.layer == playerLayer || go.layer == ringLayer || go.CompareTag("Player");

    /// <summary>Set the pitch of the fire audio source.</summary>
    public void SetPitch(float pitch)
    {
        if (audioSource != null)
            audioSource.pitch = pitch;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        poolable = GetComponent<Poolable>();

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        playerLayer        = LayerMask.NameToLayer("Player");
        ringLayer          = LayerMask.NameToLayer("Ring");

        nonPlayerLayerMask = Physics.DefaultRaycastLayers
            & ~(1 << playerLayer)
            & ~(ringLayer >= 0 ? (1 << ringLayer) : 0);

        if (ignoreRaycastLayer < 0)
            ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        Physics.IgnoreLayerCollision(gameObject.layer, playerLayer, true);
        if (ringLayer >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, ringLayer, true);
    }

    private void OnEnable()
    {
        hasHit = false;
        homingTarget = null;
        homingActive = false;

        rb.angularVelocity = Vector3.zero;

        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }

        lifetimeCoroutine = StartCoroutine(LifetimeExpire());

        if (homingEnabled)
            StartCoroutine(HomingRoutine());
    }

    private void OnDisable()
    {
        // Stop ALL coroutines — including any in-flight HitStop — so a pooled
        // object never re-fires logic from a previous use after re-activation.
        StopAllCoroutines();
        lifetimeCoroutine = null;

        if (trail != null)
            trail.Clear();
    }

    private void Update()
    {
        if (homingEnabled && homingActive && homingTarget != null)
            SteerTowardTarget();
        else
        {
            // Keep speed constant — correct magnitude without redirecting.
            float currentSpeed = rb.linearVelocity.magnitude;
            if (currentSpeed > 0.01f && currentSpeed < Speed * 0.9f)
                rb.linearVelocity = rb.linearVelocity.normalized * Speed;
        }
    }

    private void FixedUpdate()
    {
        if (hasHit) return;

        if (Physics.SphereCast(transform.position, collisionCheckRadius,
            transform.forward, out RaycastHit hit, Speed * Time.fixedDeltaTime,
            nonPlayerLayerMask, QueryTriggerInteraction.Ignore))
        {
            HandleRayHit(hit);
        }
    }

    // ─── Homing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Phase 1 — waits homingActivationDelay seconds (straight flight).
    /// Phase 2 — polls OverlapSphere each frame; steers toward the nearest IDamageable.
    ///           If the target leaves the sphere the projectile resumes straight flight.
    /// </summary>
    private IEnumerator HomingRoutine()
    {
        // Phase 1: fly straight, record velocity for later restoration.
        yield return new WaitForSeconds(homingActivationDelay);
        straightVelocity = rb.linearVelocity;   // snapshot direction + speed
        homingActive = true;

        // Phase 2: rolling detection every frame.
        while (!hasHit)
        {
            homingTarget = FindNearestTarget();

            if (homingTarget == null)
            {
                // No target in range — restore straight velocity so the projectile
                // doesn't slow down or drift while waiting.
                float currentSpeed = rb.linearVelocity.magnitude;
                if (currentSpeed < Speed * 0.9f)
                    rb.linearVelocity = straightVelocity.normalized * Speed;
            }

            yield return null;
        }
    }

    /// <summary>Returns the nearest IDamageable inside homingSearchRadius, or null.</summary>
    private Transform FindNearestTarget()
    {
        Collider[] candidates = Physics.OverlapSphere(transform.position, homingSearchRadius);
        float bestDist = float.MaxValue;
        Transform best = null;

        foreach (Collider col in candidates)
        {
            if (IsPlayer(col.gameObject)) continue;
            if (col.GetComponent<IDamageable>() == null &&
                col.GetComponentInParent<IDamageable>() == null) continue;

            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = col.transform; }
        }

        return best;
    }

    /// <summary>Rotates rb.linearVelocity toward homingTarget each frame.</summary>
    private void SteerTowardTarget()
    {
        if (homingTarget == null) return;

        Vector3 toTarget   = (homingTarget.position - transform.position).normalized;
        Vector3 currentDir = rb.linearVelocity.normalized;
        Vector3 newDir = Vector3.RotateTowards(currentDir, toTarget,
            homingTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        rb.linearVelocity  = newDir * Speed;
        transform.rotation = Quaternion.LookRotation(newDir);

        // Keep straight velocity up to date so if we lose the target we restore
        // the current heading rather than the original fire direction.
        straightVelocity = rb.linearVelocity;
    }

    // ─── Hit handling ─────────────────────────────────────────────────────────

    private void HandleRayHit(RaycastHit hit)
    {
        if (IsPlayer(hit.collider.gameObject)) return;

        hit.collider.GetComponent<IDamageable>()
            ?.TakeDamage(damage, hit.point, transform.forward);

        SpawnImpactVFX(hit.point);
        ReturnToPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || IsPlayer(collision.gameObject)) return;
        hasHit = true;

        collision.gameObject.GetComponent<IDamageable>()
            ?.TakeDamage(damage, collision.contacts[0].point, transform.forward);

        SpawnImpactVFX(collision.contacts[0].point);

        if (hitStopDuration > 0f)
            StartCoroutine(HitStop());
        else
            ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (other.gameObject.layer == ignoreRaycastLayer) return;
        if (IsPlayer(other.gameObject)) return;
        hasHit = true;

        other.GetComponent<IDamageable>()
            ?.TakeDamage(damage, other.ClosestPoint(transform.position), transform.forward);

        SpawnImpactVFX(other.ClosestPoint(transform.position));
        ReturnToPool();
    }

    private IEnumerator HitStop()
    {
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic    = true;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        rb.isKinematic = false;
        ReturnToPool();
    }

    private IEnumerator LifetimeExpire()
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    private void SpawnImpactVFX(Vector3 position)
    {
        SpawnVFX(impactSphere, position);
        SpawnVFX(spawnEffect,  position);
    }

    /// <summary>Gets a pooled VFX instance, falling back to Instantiate if no pool exists.</summary>
    private static void SpawnVFX(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;

        if (PoolManager.Instance != null)
            PoolManager.Instance.Get(prefab, position, Quaternion.identity);
        else
            Instantiate(prefab, position, Quaternion.identity);
    }

    /// <summary>Returns this projectile to its pool, or destroys it if not pooled.</summary>
    // ===== Gizmos =====

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 1.5f);

            float dot = Vector3.Dot(transform.forward, rb.linearVelocity.normalized);
            if (dot < 0.98f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, 0.15f);
            }
        }

        if (homingEnabled)
        {
            // Orange = inactive sphere (pre-delay), yellow = active sphere.
            Gizmos.color = homingActive
                ? new Color(1f, 1f, 0f, 0.15f)
                : new Color(1f, 0.6f, 0f, 0.08f);
            Gizmos.DrawWireSphere(transform.position, homingSearchRadius);
        }
    }

    private void ReturnToPool()
    {
        if (poolable != null)
            poolable.Release();
        else
            Destroy(gameObject);
    }
}
