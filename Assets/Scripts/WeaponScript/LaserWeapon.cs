using System.Collections;
using UnityEngine;

/// <summary>
/// Hold-to-fire laser weapon. Extends ModularWeapon so it slots into the
/// ring system exactly like any other weapon. While Fire1 is held and this
/// weapon is centred, it casts a continuous beam and deals damage every tick.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LaserWeapon : ModularWeapon
{
    // ─── Inspector ─────────────────────────────────────────────────────────────

    [Header("Laser Beam")]
    [Tooltip("Maximum reach of the beam in world units.")]
    [SerializeField] private float range = 40f;
    [Tooltip("Damage per second while the beam is on a target.")]
    [SerializeField] private float damagePerSecond = 25f;
    [Tooltip("Radius of the beam for the damage sphere cast (0 = pure raycast).")]
    [SerializeField] private float beamRadius = 0.08f;
    [Tooltip("Width of the visual line at the muzzle end.")]
    [SerializeField] private float beamStartWidth = 0.06f;
    [Tooltip("Width of the visual line at the impact end.")]
    [SerializeField] private float beamEndWidth = 0.02f;
    [Tooltip("Layers the beam can hit.")]
    [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

    [Header("Beam Colour")]
    [SerializeField] private Color beamColorInner = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color beamColorOuter = new Color(1f, 0.4f, 0.0f, 0.6f);

    [Header("Impact VFX")]
    [Tooltip("Optional particle system that plays at the beam's hit point.")]
    [SerializeField] private ParticleSystem impactParticles;

    [Header("Energy")]
    [Tooltip("Energy drained per second while the beam is active.")]
    [SerializeField] private float energyPerSecond = 3f;

    [Header("Warm-up")]
    [Tooltip("Seconds from trigger-press to full damage. Beam appears immediately but deals 0→full damage over this window.")]
    [SerializeField] private float warmUpDuration = 0.25f;

    // ─── Private ───────────────────────────────────────────────────────────────

    private LineRenderer outerBeam;
    private LineRenderer innerBeam;

    private bool isFiring;
    private int lastFireFrame = -1;   // frame TryFire was last called
    private float warmUpTimer;

    private IDamageable currentTarget;
    private float damageAccumulator;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildLineRenderers();
    }

    /// <summary>
    /// LateUpdate runs after all Update calls, including ArmMount360.Update which
    /// triggers TryFire. If TryFire wasn't called this frame, shut the beam off.
    /// </summary>
    private void LateUpdate()
    {
        if (lastFireFrame != Time.frameCount)
        {
            StopBeam();
        }
    }

    // ─── ModularWeapon overrides ───────────────────────────────────────────────

    /// <summary>
    /// Called every frame Fire1 is held and this weapon is centred.
    /// Drains energy per-second instead of per-shot, and drives the beam.
    /// </summary>
    public override bool TryFire(ArmBattery battery, Camera playerCamera, float centerMultiplier = 1f)
    {
        float cost = energyPerSecond * Time.deltaTime * (IsCenter ? centerMultiplier : 1f);
        if (battery == null || !battery.Consume(cost))
        {
            StopBeam();
            return false;
        }

        // Stamp the frame so LateUpdate knows TryFire ran this frame.
        lastFireFrame = Time.frameCount;
        isFiring      = true;

        warmUpTimer = Mathf.Min(warmUpTimer + Time.deltaTime, warmUpDuration);
        float warmUpRatio = (warmUpDuration > 0f) ? warmUpTimer / warmUpDuration : 1f;

        FireBeam(playerCamera, warmUpRatio, centerMultiplier);
        return true;
    }

    // ─── Beam logic ───────────────────────────────────────────────────────────

    private void FireBeam(Camera playerCamera, float warmUpRatio, float centerMultiplier)
    {
        if (firePoint == null) return;

        // Resolve aim direction.
        Vector3 origin    = firePoint.position;
        Vector3 direction = firePoint.forward;

        if (IsCenter && useCrosshairWhenCentered && playerCamera != null)
        {
            Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            direction = aimRay.direction;
            origin    = firePoint.position; // keep origin at muzzle
        }

        // Raycast / sphere cast for hit.
        bool didHit;
        RaycastHit hit;

        if (beamRadius > 0f)
            didHit = Physics.SphereCast(origin, beamRadius, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
        else
            didHit = Physics.Raycast(origin, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        Vector3 endPoint = didHit ? hit.point : origin + direction * range;

        // Update visuals.
        SetBeamPositions(origin, endPoint);
        SetBeamActive(true);
        SetBeamBrightness(warmUpRatio);

        // Impact particles.
        if (impactParticles != null)
        {
            if (didHit)
            {
                impactParticles.transform.position = hit.point;
                impactParticles.transform.forward  = -direction;
                if (!impactParticles.isPlaying) impactParticles.Play();
            }
            else
            {
                if (impactParticles.isPlaying) impactParticles.Stop();
            }
        }

        // Damage tick.
        if (didHit && warmUpRatio >= 1f)
        {
            IDamageable damageable = hit.collider.GetComponent<IDamageable>()
                                  ?? hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                float dps = damagePerSecond * centerMultiplier;
                damageAccumulator += dps * Time.deltaTime;

                if (damageAccumulator >= 1f)
                {
                    float toApply = Mathf.Floor(damageAccumulator);
                    damageAccumulator -= toApply;
                    damageable.TakeDamage(toApply, hit.point, -direction);
                }
            }
            else
            {
                damageAccumulator = 0f;
            }
        }
        else if (!didHit)
        {
            damageAccumulator = 0f;
        }

        // Muzzle loop sound — play one-shot per-frame would be wrong; use a looping AudioSource.
        if (audioSource != null && shootSound != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    private void StopBeam()
    {
        SetBeamActive(false);
        warmUpTimer       = 0f;
        damageAccumulator = 0f;
        currentTarget     = null;

        if (impactParticles != null && impactParticles.isPlaying)
            impactParticles.Stop();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // ─── LineRenderer helpers ─────────────────────────────────────────────────

    private void BuildLineRenderers()
    {
        innerBeam = CreateBeam("Beam_Inner", beamStartWidth,       beamEndWidth,       beamColorInner);
        outerBeam = CreateBeam("Beam_Outer", beamStartWidth * 2.5f, beamEndWidth * 2f, beamColorOuter);

        SetBeamActive(false);
    }

    private LineRenderer CreateBeam(string goName, float startWidth, float endWidth, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        LineRenderer lr   = go.AddComponent<LineRenderer>();
        lr.positionCount  = 2;
        lr.startWidth     = startWidth;
        lr.endWidth       = endWidth;
        lr.useWorldSpace  = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.generateLightingData = true;

        // Use a simple additive/unlit material so the beam glows regardless of lighting.
        Material mat = CreateBeamMaterial(color);
        lr.material = mat;

        return lr;
    }

    private static Material CreateBeamMaterial(Color color)
    {
        // URP Unlit with additive blending gives the brightest glow.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor(EmissionColorId, color * 2f);
        mat.enableInstancing = true;

        // Additive blend on URP Unlit particle shader.
        if (mat.HasProperty("_BlendMode"))
            mat.SetFloat("_BlendMode", 4f); // Additive

        return mat;
    }

    private void SetBeamPositions(Vector3 start, Vector3 end)
    {
        if (innerBeam != null) { innerBeam.SetPosition(0, start); innerBeam.SetPosition(1, end); }
        if (outerBeam != null) { outerBeam.SetPosition(0, start); outerBeam.SetPosition(1, end); }
    }

    private void SetBeamActive(bool active)
    {
        if (innerBeam != null && innerBeam.enabled != active) innerBeam.enabled = active;
        if (outerBeam != null && outerBeam.enabled != active) outerBeam.enabled = active;
    }

    private void SetBeamBrightness(float t)
    {
        // Pulse width up during warm-up.
        if (innerBeam != null)
        {
            innerBeam.startWidth = Mathf.Lerp(0f, beamStartWidth,        t);
            innerBeam.endWidth   = Mathf.Lerp(0f, beamEndWidth,          t);
        }
        if (outerBeam != null)
        {
            outerBeam.startWidth = Mathf.Lerp(0f, beamStartWidth * 2.5f, t);
            outerBeam.endWidth   = Mathf.Lerp(0f, beamEndWidth   * 2f,   t);
        }
    }

    private void OnDisable()
    {
        StopBeam();
        isFiring = false;
    }
}
