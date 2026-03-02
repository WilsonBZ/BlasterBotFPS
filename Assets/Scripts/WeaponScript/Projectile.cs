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
    
    public void SetPitch(float pitch)
    {
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        rb.linearVelocity = transform.forward * Speed;

        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }
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
            transform.forward, out RaycastHit hit, Speed * Time.fixedDeltaTime,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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
        }
        
        SpawnImpactSphere(hit.point);

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
        }
        
        SpawnImpactSphere(collision.contacts[0].point);

        StartCoroutine(DestroyTrail());
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) return;

        IDamageable damageable = other.GetComponent<IDamageable>();

        if (damageable != null)
        {
            Vector3 hitDirection = transform.forward;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(damage, hitPoint, hitDirection);
        }
        
        SpawnImpactSphere(other.ClosestPoint(transform.position));

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

    private void SpawnImpactSphere(Vector3 position)
    {
        if (impactSphere != null)
        {
            Instantiate(impactSphere, position, Quaternion.identity);
        }
    }
}
