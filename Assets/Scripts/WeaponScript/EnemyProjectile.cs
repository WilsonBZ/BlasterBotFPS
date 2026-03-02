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
    private bool hasHit = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * Speed;
        }

        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }
    }

    /// <summary>
    /// Sets the audio pitch for variation between shots.
    /// </summary>
    public void SetPitch(float pitch)
    {
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || !IsValidTarget(other.gameObject)) return;

        hasHit = true;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(damage, hitPoint, transform.forward);
        }

        SpawnImpactSphere(transform.position);
        StartCoroutine(DestroyAfterTrail());
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit || !IsValidTarget(collision.gameObject)) return;

        hasHit = true;

        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            Vector3 hitPoint = collision.contacts[0].point;
            damageable.TakeDamage(damage, hitPoint, transform.forward);
        }

        SpawnImpactSphere(collision.contacts[0].point);
        StartCoroutine(DestroyAfterTrail());
        Destroy(gameObject);
    }

    private bool IsValidTarget(GameObject target)
    {
        return ((1 << target.layer) & targetLayers) != 0;
    }

    private void SpawnImpactSphere(Vector3 position)
    {
        if (impactSphere != null)
        {
            Instantiate(impactSphere, position, Quaternion.identity);
        }
    }

    private IEnumerator DestroyAfterTrail()
    {
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, trail.time);
        }
        yield return null;
    }
}

