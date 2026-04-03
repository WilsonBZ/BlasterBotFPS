using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lingering fire zone dropped by a GazGun projectile when the Molotov Explosion buff is active.
/// Pulses damage to every IDamageable enemy inside its radius every tickInterval seconds.
/// </summary>
public class MolotovZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("World-space radius of the fire zone.")]
    [SerializeField] private float radius = 3f;
    [Tooltip("Damage dealt to each enemy per tick.")]
    [SerializeField] private float damagePerTick = 8f;
    [Tooltip("Seconds between damage ticks.")]
    [SerializeField] private float tickInterval = 0.5f;
    [Tooltip("Total lifetime in seconds. Leave at 0 to auto-read from the root ParticleSystem duration.")]
    [SerializeField] private float duration = 0f;
    [Tooltip("Layers considered damageable. Should include the enemy layers.")]
    [SerializeField] private LayerMask damageableLayers;

    private float resolvedDuration;

    private void Start()
    {
        resolvedDuration = ResolveLifetime();
        StartCoroutine(TickDamage());
        Destroy(gameObject, resolvedDuration);
    }

    /// <summary>
    /// Returns the effective lifetime.
    /// Priority: manually set duration > root ParticleSystem duration > 5s fallback.
    /// </summary>
    private float ResolveLifetime()
    {
        if (duration > 0f) return duration;

        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            // Non-looping PS: use its authoured duration.
            // Looping PS: use startLifetime as a proxy for how long the fire burns.
            float psDuration = main.loop
                ? main.startLifetime.constantMax
                : main.duration;

            return psDuration > 0f ? psDuration : 5f;
        }

        return 5f;
    }

    private IEnumerator TickDamage()
    {
        var wait = new WaitForSeconds(tickInterval);
        HashSet<GameObject> hitThisTick = new HashSet<GameObject>();
        float elapsed = 0f;

        while (elapsed < resolvedDuration)
        {
            yield return wait;
            elapsed += tickInterval;

            hitThisTick.Clear();
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, damageableLayers);
            foreach (Collider col in hits)
            {
                GameObject root = col.transform.root.gameObject;
                if (hitThisTick.Contains(root)) continue;
                hitThisTick.Add(root);

                IDamageable damageable = col.GetComponent<IDamageable>()
                    ?? col.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(damagePerTick, col.transform.position, Vector3.up);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0f, 0.35f);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0.35f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
