using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projectile fired by <see cref="ChaurliAI"/>. Travels forward and detonates
/// on contact, applying radial AoE damage to the player (and anything else
/// IDamageable) — identical in principle to the player's ChargeProjectile.
///
/// Uses a per-frame Raycast sweep as primary detection so the Physics Layer
/// Collision Matrix is never a bottleneck.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ChaurliProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 18f;
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private bool useGravity = true;
    [Tooltip("Gravity scale applied if useGravity is true (1 = normal gravity).")]
    [SerializeField] private float gravityScale = 0.4f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float explosionDamage = 80f;
    [Tooltip("Damage falloff multiplier at the edge of the blast (0.5 = 50%).")]
    [SerializeField] private float edgeDamageMultiplier = 0.4f;
    [SerializeField] private float explosionForce = 400f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private TrailRenderer trail;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip impactSound;

    // ── Private ───────────────────────────────────────────────────────────────

    private Rigidbody rb;
    private bool hasExploded;
    private Vector3 previousPosition;

    private int enemyLayer;
    private int ignoreLayer;

    // ── Init ──────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;            // we apply gravity manually for scale control
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        enemyLayer  = LayerMask.NameToLayer("Enemy");
        ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
    }

    /// <summary>Called by ChaurliAI immediately after instantiating this projectile.</summary>
    public void Initialize(Vector3 aimDirection)
    {
        rb.linearVelocity = aimDirection.normalized * speed;
        previousPosition  = transform.position;
        Destroy(gameObject, lifetime);
    }

    // ── Physics ───────────────────────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (hasExploded) return;

        if (useGravity)
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }

    // ── Hit Detection ─────────────────────────────────────────────────────────

    private void Update()
    {
        if (hasExploded) return;

        Vector3 current = transform.position;
        Vector3 delta   = current - previousPosition;
        float   dist    = delta.magnitude;

        if (dist > 0.001f)
        {
            // Sweep from last frame to this frame — catches tunnelling and ignores
            // the Layer Collision Matrix entirely.
            if (Physics.Raycast(
                previousPosition,
                delta.normalized,
                out RaycastHit hit,
                dist + 0.05f,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide))
            {
                int layer = hit.collider.gameObject.layer;

                // Ignore self-layer, other enemies, and ignore-raycast objects.
                if (layer == gameObject.layer)                   { previousPosition = current; return; }
                if (enemyLayer  >= 0 && layer == enemyLayer)     { previousPosition = current; return; }
                if (ignoreLayer >= 0 && layer == ignoreLayer)    { previousPosition = current; return; }

                Explode(hit.point);
                return;
            }
        }

        previousPosition = current;
    }

    // Fallback for slow-moving phase or trigger volumes.
    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        int layer = collision.gameObject.layer;
        if (layer == gameObject.layer)                  return;
        if (enemyLayer  >= 0 && layer == enemyLayer)    return;
        if (ignoreLayer >= 0 && layer == ignoreLayer)   return;
        Explode(collision.contacts[0].point);
    }

    // ── Explosion ─────────────────────────────────────────────────────────────

    /// <summary>
    /// AoE damage via Physics.OverlapSphere with QueryTriggerInteraction.Collide
    /// so player trigger hurtboxes are always included.
    /// Walks up hierarchy with GetComponentInParent to find IDamageable on the player root.
    /// </summary>
    private void Explode(Vector3 point)
    {
        hasExploded = true;

        PlayImpactAudio();
        SpawnExplosionVFX(point);
        ApplyAoEDamage(point);

        // Detach trail so it fades naturally.
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, trail.time);
        }

        Destroy(gameObject);
    }

    private void ApplyAoEDamage(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(
            point,
            explosionRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        HashSet<GameObject> alreadyDamaged = new HashSet<GameObject>();

        foreach (Collider col in hits)
        {
            int layer = col.gameObject.layer;
            if (layer == gameObject.layer)                  continue;
            if (enemyLayer  >= 0 && layer == enemyLayer)    continue;
            if (ignoreLayer >= 0 && layer == ignoreLayer)   continue;

            // De-duplicate by root so multi-collider characters are hit once.
            GameObject root = col.transform.root.gameObject;
            if (!alreadyDamaged.Add(root)) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            // Linear damage falloff: full damage at center, edgeDamageMultiplier at edge.
            float dist    = Vector3.Distance(point, col.transform.position);
            float falloff = Mathf.Lerp(1f, edgeDamageMultiplier, dist / explosionRadius);
            float dmg     = explosionDamage * falloff;

            Vector3 dir = (col.transform.position - point).normalized;
            damageable.TakeDamage(dmg, point, dir);

            // Physics impulse on rigidbodies (ragdolls, props, etc.).
            Rigidbody hitRb = col.GetComponentInParent<Rigidbody>();
            if (hitRb != null)
                hitRb.AddExplosionForce(explosionForce, point, explosionRadius, 0.5f, ForceMode.Impulse);
        }
    }

    // ── VFX / Audio ───────────────────────────────────────────────────────────

    private void SpawnExplosionVFX(Vector3 point)
    {
        if (explosionPrefab == null) return;

        if (PoolManager.Instance != null)
            PoolManager.Instance.Get(explosionPrefab, point, Quaternion.identity);
        else
            Instantiate(explosionPrefab, point, Quaternion.identity);
    }

    private void PlayImpactAudio()
    {
        if (audioSource == null || impactSound == null) return;
        audioSource.transform.SetParent(null);
        audioSource.PlayOneShot(impactSound);
        Destroy(audioSource.gameObject, impactSound.length + 0.2f);
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
