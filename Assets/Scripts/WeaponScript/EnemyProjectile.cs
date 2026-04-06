using System.Collections;
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float Speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 50f;

    [Header("Explosion")]
    [Tooltip("Radius of the OverlapSphere damage check on impact.")]
    [SerializeField] private float explosionRadius = 0.5f;

    [Header("Effects")]
    [SerializeField] private GameObject impactSphere;
    [SerializeField] private TrailRenderer trail;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    private Rigidbody rb;
    private Poolable poolable;
    private bool hasHit;
    private Coroutine lifetimeCoroutine;

    // Cached layer indices — resolved once in Awake.
    private int enemyLayer      = -1;
    private int ignoreLayer     = -1;
    private Vector3 previousPosition;

    /// <summary>Sets the audio pitch for variation between shots.</summary>
    public void SetPitch(float pitch)
    {
        if (audioSource != null)
            audioSource.pitch = pitch;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        poolable = GetComponent<Poolable>();

        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        enemyLayer  = LayerMask.NameToLayer("Enemy");
        ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private void OnEnable()
    {
        hasHit          = false;
        previousPosition = transform.position;

        if (rb != null)
        {
            rb.linearVelocity  = transform.forward * Speed;
            rb.angularVelocity = Vector3.zero;
        }

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
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }

        if (trail != null)
            trail.Clear();
    }

    private void Update()
    {
        if (hasHit) return;

        // Cast a ray from last frame's position to this frame's position.
        // This bypasses the Physics Layer Collision Matrix entirely and catches
        // fast-moving projectiles that would tunnel through thin colliders.
        Vector3 currentPosition = transform.position;
        Vector3 delta           = currentPosition - previousPosition;
        float   travelDistance  = delta.magnitude;

        if (travelDistance > 0.001f)
        {
            if (Physics.Raycast(
                previousPosition,
                delta.normalized,
                out RaycastHit hit,
                travelDistance + 0.05f,  // tiny margin to catch exact-surface hits
                Physics.AllLayers,
                QueryTriggerInteraction.Collide))
            {
                int hitLayer = hit.collider.gameObject.layer;

                // Ignore self-layer, other enemies, and ignore-raycast objects.
                if (hitLayer == gameObject.layer)         { previousPosition = currentPosition; return; }
                if (enemyLayer  >= 0 && hitLayer == enemyLayer)  { previousPosition = currentPosition; return; }
                if (ignoreLayer >= 0 && hitLayer == ignoreLayer) { previousPosition = currentPosition; return; }

                Explode(hit.point);
                return;
            }
        }

        previousPosition = currentPosition;
    }

    // Keep collision callbacks as a secondary safety net for slow-moving projectiles.
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        int layer = other.gameObject.layer;
        if (layer == gameObject.layer)                  return;
        if (enemyLayer  >= 0 && layer == enemyLayer)    return;
        if (ignoreLayer >= 0 && layer == ignoreLayer)   return;
        Explode(transform.position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        int layer = collision.gameObject.layer;
        if (layer == gameObject.layer)                  return;
        if (enemyLayer  >= 0 && layer == enemyLayer)    return;
        if (ignoreLayer >= 0 && layer == ignoreLayer)   return;
        Explode(collision.contacts[0].point);
    }

    /// <summary>
    /// Physics.OverlapSphere damage check at the impact point.
    /// Uses QueryTriggerInteraction.Collide to include the player's trigger hurtbox.
    /// GetComponentInParent walks up to the IDamageable on the player root.
    /// Enemy layer is excluded so projectiles never self-damage.
    /// </summary>
    private void Explode(Vector3 position)
    {
        hasHit = true;

        Collider[] hits = Physics.OverlapSphere(
            position,
            explosionRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            int layer = hit.gameObject.layer;
            if (layer == gameObject.layer)                  continue;
            if (enemyLayer  >= 0 && layer == enemyLayer)    continue;
            if (ignoreLayer >= 0 && layer == ignoreLayer)   continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            Vector3 dir = (hit.transform.position - position).normalized;
            damageable.TakeDamage(damage, hit.ClosestPoint(position), dir);
        }

        SpawnImpactVFX(position);
        ReturnToPool();
    }

    private void SpawnImpactVFX(Vector3 position)
    {
        if (impactSphere == null) return;

        if (PoolManager.Instance != null)
            PoolManager.Instance.Get(impactSphere, position, Quaternion.identity);
        else
            Instantiate(impactSphere, position, Quaternion.identity);
    }

    private IEnumerator LifetimeExpire()
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (poolable != null)
            poolable.Release();
        else
            Destroy(gameObject);
    }
}

