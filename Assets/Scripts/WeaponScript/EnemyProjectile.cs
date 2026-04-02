using System.Collections;
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float Speed = 200f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 50f;

    [Header("Target Settings")]
    [SerializeField] private LayerMask targetLayers;

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
    }

    private void OnEnable()
    {
        hasHit = false;

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

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || !IsValidTarget(other.gameObject)) return;
        hasHit = true;

        other.GetComponent<IDamageable>()
            ?.TakeDamage(damage, other.ClosestPoint(transform.position), transform.forward);

        SpawnImpactVFX(transform.position);
        ReturnToPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || !IsValidTarget(collision.gameObject)) return;
        hasHit = true;

        collision.gameObject.GetComponent<IDamageable>()
            ?.TakeDamage(damage, collision.contacts[0].point, transform.forward);

        SpawnImpactVFX(collision.contacts[0].point);
        ReturnToPool();
    }

    private bool IsValidTarget(GameObject target) =>
        ((1 << target.layer) & targetLayers) != 0;

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

