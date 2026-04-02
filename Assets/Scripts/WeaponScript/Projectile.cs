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

    private Rigidbody rb;
    private int nonPlayerLayerMask;
    private int playerLayer;
    private int ringLayer;
    private Poolable poolable;
    private bool hasHit;
    private Coroutine lifetimeCoroutine;

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

        rb.angularVelocity = Vector3.zero;

        // Velocity is set by the weapon spawner (ModularWeapon / ChargeWeapon)
        // immediately after Get(). Do NOT set it here — the transform rotation
        // is not yet updated when OnEnable runs, so self-applying transform.forward
        // would fire in whichever direction the pooled object happened to face last frame.

        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }

        lifetimeCoroutine = StartCoroutine(LifetimeExpire());
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
        // Keep speed constant — correct magnitude without redirecting.
        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > 0.01f && currentSpeed < Speed * 0.9f)
            rb.linearVelocity = rb.linearVelocity.normalized * Speed;
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
        // Cyan = transform.forward (what SphereCast uses — should match velocity).
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            // White = actual Rigidbody velocity direction.
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 1.5f);

            // Magenta dot if transform.forward and velocity disagree by more than 10°.
            float dot = Vector3.Dot(transform.forward, rb.linearVelocity.normalized);
            if (dot < 0.98f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, 0.15f);
            }
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
