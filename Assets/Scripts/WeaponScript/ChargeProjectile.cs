using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projectile fired by ChargeWeapon. Scales in size and damage based on charge.
/// Spawns at a tiny safe scale and grows to its intended size over growDuration,
/// preventing self-hits on the player at the moment of firing.
/// Explodes on impact, applying AoE damage to nearby enemies.
/// </summary>
public class ChargeProjectile : MonoBehaviour
{
    [Header("Base Movement")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifetime = 6f;

    [Header("Scale Growth")]
    [Tooltip("How long (seconds) the projectile takes to grow from spawn scale to its full target scale.")]
    [SerializeField] private float growDuration = 0.08f;
    [Tooltip("The collider scale fraction used at spawn — small enough to clear the player muzzle.")]
    [SerializeField] private float spawnScaleFraction = 0.05f;

    [Header("Explosion")]
    [SerializeField] private float minExplosionRadius = 1.5f;
    [SerializeField] private float maxExplosionRadius = 6f;
    [SerializeField] private float explosionForce = 300f;
    [SerializeField] private LayerMask damageableLayers;

    [Header("VFX")]
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Small impact flash spawned at contact point — use Hit000.prefab.")]
    [SerializeField] private GameObject impactFlashPrefab;
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Color minChargeColor = new Color(0.4f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color maxChargeColor = new Color(1f, 0.4f, 0.05f, 0.9f);
    [SerializeField] private TrailRenderer trail;

    [Header("Charge Light")]
    [SerializeField] private Light projectileLight;
    [SerializeField] private float lightIntensity = 4f;

    [Header("Molotov")]
    [Tooltip("Spawns a lingering fire zone on impact. Activated by the Molotov Explosion buff.")]
    public bool molotovEnabled = false;
    [Tooltip("MolotovZone prefab to spawn on explosion. Assign in the ChargeProjectile prefab.")]
    public GameObject molotovZonePrefab;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    private float chargeRatio;
    private float damage;
    private float explosionRadius;
    private Rigidbody rb;
    private bool hasExploded;
    private Material instanceMaterial;

    private float targetScale;
    private float growTimer;
    private int playerLayer;

    /// <summary>Returns true if the GameObject is on the Player layer or tagged as Player.</summary>
    private bool IsPlayer(GameObject go) =>
        go.layer == playerLayer || go.CompareTag("Player");

    // ─── Init ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        playerLayer = LayerMask.NameToLayer("Player");

        // Ignore all Player-layer colliders at the physics matrix level.
        Physics.IgnoreLayerCollision(gameObject.layer, playerLayer, true);
    }

    private void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (growTimer >= growDuration) return;

        growTimer += Time.deltaTime;
        float t = Mathf.Clamp01(growTimer / growDuration);
        // Ease-out so the projectile snaps to full size quickly but smoothly.
        float easedT = 1f - (1f - t) * (1f - t);
        float currentScale = Mathf.Lerp(targetScale * spawnScaleFraction, targetScale, easedT);
        transform.localScale = Vector3.one * currentScale;

        // Update light range to track the growing projectile.
        if (projectileLight != null)
            projectileLight.range = currentScale * 3f;
    }

    /// <summary>
    /// Called by ChargeWeapon immediately after instantiating this projectile.
    /// </summary>
    public void Initialize(float chargeRatio, float damage, float scale)
    {
        this.chargeRatio = chargeRatio;
        this.damage = damage;
        this.explosionRadius = Mathf.Lerp(minExplosionRadius, maxExplosionRadius, chargeRatio);
        this.targetScale = scale;
        this.growTimer = 0f;

        // Start at a tiny fraction of the target scale so the collider clears the player.
        transform.localScale = Vector3.one * (scale * spawnScaleFraction);

        // Set visual color
        SetProjectileColor(chargeRatio);

        // Set light color
        if (projectileLight != null)
        {
            projectileLight.color = Color.Lerp(minChargeColor, maxChargeColor, chargeRatio);
            projectileLight.intensity = lightIntensity * (0.5f + chargeRatio * 0.5f);
            projectileLight.range = scale * spawnScaleFraction * 3f;
        }

        // Widen trail based on scale
        if (trail != null)
            trail.startWidth = Mathf.Lerp(0.05f, 0.4f, chargeRatio);
    }

    private void SetProjectileColor(float ratio)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        if (baseMaterial == null)
        {
            Debug.LogError("ChargeProjectile: baseMaterial not assigned. Assign VFX_ChargeProjectile.mat in the prefab.");
            return;
        }

        instanceMaterial = new Material(baseMaterial);
        Color color = Color.Lerp(minChargeColor, maxChargeColor, ratio);
        instanceMaterial.SetColor("_BaseColor", color);
        rend.material = instanceMaterial;
    }

    // ─── Collision ────────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        if (IsPlayer(collision.gameObject) || IsPlayer(collision.transform.root.gameObject)) return;
        Explode(collision.contacts[0].point);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        if (IsPlayer(other.gameObject) || IsPlayer(other.transform.root.gameObject)) return;

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

        if (molotovEnabled && molotovZonePrefab != null)
        {
            // Raycast down to snap the zone to the floor surface.
            Vector3 zoneOrigin = point + Vector3.up * 0.5f;
            Vector3 floorPos   = Physics.Raycast(zoneOrigin, Vector3.down, out RaycastHit floorHit, 5f)
                ? floorHit.point
                : point;
            Instantiate(molotovZonePrefab, floorPos, Quaternion.identity);
        }

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

        if (impactFlashPrefab != null)
            Instantiate(impactFlashPrefab, point, Quaternion.identity);

        SpawnShockwave(point);
    }

    // No longer a coroutine — the sphere manages its own lifecycle via VFXExpandAndFade.
    private void SpawnShockwave(Vector3 point)
    {
        if (baseMaterial == null)
        {
            Debug.LogError("ChargeProjectile: baseMaterial is not assigned. Shockwave skipped.");
            return;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position   = point;
        sphere.transform.localScale = Vector3.one * 0.1f;
        Destroy(sphere.GetComponent<Collider>());

        Material mat = new Material(baseMaterial);
        Color shockColor = Color.Lerp(minChargeColor, maxChargeColor, chargeRatio);
        mat.SetColor("_BaseColor", shockColor);
        sphere.GetComponent<Renderer>().material = mat;

        VFXExpandAndFade vfx = sphere.AddComponent<VFXExpandAndFade>();
        vfx.Initialize(mat, explosionRadius * 2f, 0.4f);
    }

    private void ApplyAoEDamage(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(point, explosionRadius, damageableLayers);
        HashSet<GameObject> alreadyDamaged = new HashSet<GameObject>();

        foreach (Collider col in hits)
        {
            // Never damage the player with a player-fired weapon
            if (IsPlayer(col.gameObject) || IsPlayer(col.transform.root.gameObject)) continue;

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
