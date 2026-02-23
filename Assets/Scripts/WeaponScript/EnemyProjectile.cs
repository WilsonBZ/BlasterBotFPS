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
    [SerializeField] private GameObject spawnEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private TrailRenderer trail;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    private Rigidbody rb;

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

        SpawnEffect(spawnEffect, transform.position, transform.forward);
    }

    public void SetPitch(float pitch)
    {
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other.gameObject))
        {
            return;
        }

        IDamageable damageable = other.GetComponent<IDamageable>();

        if (damageable != null)
        {
            Vector3 hitDirection = transform.forward;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(damage, hitPoint, hitDirection);
            SpawnEffect(impactEffect, hitPoint, -hitDirection);
        }

        StartCoroutine(DestroyTrail());
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsValidTarget(collision.gameObject))
        {
            return;
        }

        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();

        if (damageable != null)
        {
            Vector3 hitDirection = transform.forward;
            Vector3 hitPoint = collision.contacts[0].point;
            damageable.TakeDamage(damage, hitPoint, hitDirection);
            SpawnEffect(impactEffect, hitPoint, collision.contacts[0].normal);
        }

        StartCoroutine(DestroyTrail());
        Destroy(gameObject);
    }

    private bool IsValidTarget(GameObject target)
    {
        int targetLayer = target.layer;
        return ((1 << targetLayer) & targetLayers) != 0;
    }

    private IEnumerator DestroyTrail()
    {
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, trail.time);
        }
        yield return null;
    }

    private void SpawnEffect(GameObject effect, Vector3 position, Vector3 normal)
    {
        if (!effect) return;

        GameObject vfx = Instantiate(effect, position, Quaternion.LookRotation(normal));

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(vfx, 2f);
        }
    }
}
