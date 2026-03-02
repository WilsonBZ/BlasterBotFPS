using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projectile fired by ChargeWeapon. Scales in size and damage based on charge.
/// Explodes on impact, applying AoE damage to nearby enemies.
/// </summary>
public class ChargeProjectile : MonoBehaviour
{
    [Header("Base Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifetime = 6f;

    [Header("Explosion")]
    [SerializeField] private float minExplosionRadius = 1.5f;
    [SerializeField] private float maxExplosionRadius = 6f;
    [SerializeField] private float explosionForce = 300f;
    [SerializeField] private LayerMask damageableLayers;

    [Header("VFX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Color minChargeColor = new Color(0.4f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color maxChargeColor = new Color(1f, 0.4f, 0.05f, 0.9f);
    [SerializeField] private TrailRenderer trail;

    [Header("Charge Light")]
    [SerializeField] private Light projectileLight;
    [SerializeField] private float lightIntensity = 4f;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    private float chargeRatio;
    private float damage;
    private float explosionRadius;
    private Rigidbody rb;
    private bool hasExploded;
    private Material instanceMaterial;

    // ─── Init ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Called by ChargeWeapon immediately after instantiating this projectile.
    /// </summary>
    public void Initialize(float chargeRatio, float damage, float scale)
    {
        this.chargeRatio = chargeRatio;
        this.damage = damage;
        this.explosionRadius = Mathf.Lerp(minExplosionRadius, maxExplosionRadius, chargeRatio);

        // Scale the physical body
        transform.localScale = Vector3.one * scale;

        // Set visual color
        SetProjectileColor(chargeRatio);

        // Set light color
        if (projectileLight != null)
        {
            projectileLight.color = Color.Lerp(minChargeColor, maxChargeColor, chargeRatio);
            projectileLight.intensity = lightIntensity * (0.5f + chargeRatio * 0.5f);
            projectileLight.range = scale * 3f;
        }

        // Widen trail based on scale
        if (trail != null)
        {
            trail.startWidth = Mathf.Lerp(0.05f, 0.4f, chargeRatio);
        }
    }

    private void SetProjectileColor(float ratio)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        instanceMaterial = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        Color color = Color.Lerp(minChargeColor, maxChargeColor, ratio);
        instanceMaterial.SetColor("_BaseColor", color);

        rend.material = instanceMaterial;
    }

    // ─── Collision ────────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        Explode(collision.contacts[0].point);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;

        // Ignore other charge projectiles
        if (other.GetComponent<ChargeProjectile>() != null) return;

        Explode(other.ClosestPoint(transform.position));
    }

    // ─── Explosion ────────────────────────────────────────────────────────────

    private void Explode(Vector3 point)
    {
        hasExploded = true;

        SpawnExplosionVFX(point);
        ApplyAoEDamage(point);

        // Detach trail so it lingers
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail.gameObject, trail.time);
        }

        Destroy(gameObject);
    }

    private void SpawnExplosionVFX(Vector3 point)
    {
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, point, Quaternion.identity);
            fx.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, chargeRatio);
        }

        // Always spawn a shockwave sphere
        StartCoroutine(SpawnShockwave(point));
    }

    private IEnumerator SpawnShockwave(Vector3 point)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = point;
        sphere.transform.localScale = Vector3.one * 0.1f;
        Destroy(sphere.GetComponent<Collider>());

        Material mat = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        Color shockColor = Color.Lerp(minChargeColor, maxChargeColor, chargeRatio);
        mat.SetColor("_BaseColor", shockColor);
        sphere.GetComponent<Renderer>().material = mat;

        float duration = 0.4f;
        float elapsed = 0f;
        float targetScale = explosionRadius * 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            sphere.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, targetScale, t);

            Color c = mat.GetColor("_BaseColor");
            c.a = Mathf.Lerp(0.85f, 0f, t);
            mat.SetColor("_BaseColor", c);

            yield return null;
        }

        Destroy(mat);
        Destroy(sphere);
    }

    private void ApplyAoEDamage(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(point, explosionRadius, damageableLayers);
        HashSet<GameObject> alreadyDamaged = new HashSet<GameObject>();

        foreach (Collider col in hits)
        {
            GameObject root = col.transform.root.gameObject;
            if (alreadyDamaged.Contains(root)) continue;
            alreadyDamaged.Add(root);

            IDamageable damageable = col.GetComponent<IDamageable>()
                ?? col.GetComponentInParent<IDamageable>();

            if (damageable == null) continue;

            // Falloff: full damage at center, 50% at edge
            float dist = Vector3.Distance(point, col.transform.position);
            float falloff = 1f - (dist / explosionRadius) * 0.5f;
            float finalDamage = damage * falloff;

            Vector3 dir = (col.transform.position - point).normalized;
            damageable.TakeDamage(finalDamage, point, dir);

            // Apply physics force to rigidbodies
            Rigidbody hitRb = col.GetComponent<Rigidbody>() ?? col.GetComponentInParent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(explosionForce * chargeRatio, point, explosionRadius, 0.5f, ForceMode.Impulse);
            }
        }
    }

    private void OnDestroy()
    {
        if (instanceMaterial != null)
            Destroy(instanceMaterial);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
