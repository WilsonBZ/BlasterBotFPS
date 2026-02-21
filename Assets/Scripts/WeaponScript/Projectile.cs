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
    [SerializeField] private GameObject nonDamageEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float hitStopDuration = 0.03f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        rb.linearVelocity = transform.forward * Speed;

        // Spawn muzzle/launch VFX
        SpawnEffect(spawnEffect, transform.position, transform.forward);
    }

    private void Update()
    {
        if (rb.linearVelocity.magnitude < Speed * 0.9f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * Speed;
        }
    }

    private void FixedUpdate()
    {
        if (Physics.SphereCast(transform.position, collisionCheckRadius,
            transform.forward, out RaycastHit hit, Speed * Time.fixedDeltaTime))
        {
            HandleCollision(hit);
        }
    }

    private void HandleCollision(RaycastHit hit)
    {
        IDamageable damageable = hit.collider.GetComponent<IDamageable>();

        if (damageable != null)
        {
            Vector3 hitDirection = transform.forward;
            damageable.TakeDamage(damage, hit.point, hitDirection);
            SpawnEffect(impactEffect, hit.point, hit.normal);
        }
        else
        {
            SpawnEffect(nonDamageEffect, hit.point, hit.normal);
        }

        StartCoroutine(DestroyTrail());
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        StartCoroutine(HitStop());

        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();

        if (damageable != null)
        {
            Vector3 hitDirection = transform.forward;
            Vector3 hitPoint = collision.contacts[0].point;
            damageable.TakeDamage(damage, hitPoint, hitDirection);
            SpawnEffect(impactEffect, hitPoint, collision.contacts[0].normal);
        }
        else
        {
            SpawnEffect(nonDamageEffect, collision.contacts[0].point, collision.contacts[0].normal);
        }

        StartCoroutine(DestroyTrail());
        Destroy(gameObject);
    }

    private IEnumerator HitStop()
    {
        if (hitStopDuration <= 0) yield break;

        float originalSpeed = Speed;
        Speed = 0;
        rb.linearVelocity = Vector3.zero;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        Speed = originalSpeed;
        if (this && rb) rb.linearVelocity = transform.forward * Speed;
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
