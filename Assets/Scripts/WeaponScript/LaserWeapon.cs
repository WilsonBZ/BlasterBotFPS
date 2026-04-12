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
    [Tooltip("Diameter of the solid core cylinder in world units.")]
    [SerializeField] private float beamStartWidth = 0.06f;
    [Tooltip("Layers the beam can hit.")]
    [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

    [Header("Beam Materials")]
    [Tooltip("Opaque emissive core (VFX_LaserCore.mat). Falls back to a generated material if not assigned.")]
    [SerializeField] private Material beamMaterialCore;
    [Tooltip("Transparent additive outer glow (VFX_LaserOuter.mat). Falls back to a generated material if not assigned.")]
    [SerializeField] private Material beamMaterialOuter;

    [Header("Beam Colour (fallback when no material assigned)")]
    [SerializeField] private Color beamColorInner = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color beamColorOuter = new Color(1f, 0.4f, 0.0f, 0.6f);

    [Header("Beam Pulse")]
    [Tooltip("Diameter oscillation amplitude while held, as a fraction of beamStartWidth.")]
    [SerializeField] private float pulseAmplitude = 0.25f;
    [Tooltip("Oscillations per second for the cylinder diameter pulse.")]
    [SerializeField] private float pulseFrequency = 6f;

    [Header("VFX")]
    [Tooltip("ParticleSystem under FiringPoint that plays at the world-space hit point.")]
    [SerializeField] private ParticleSystem impactParticles;

    [Header("Energy")]
    [Tooltip("Energy drained per second while the beam is active.")]
    [SerializeField] private float energyPerSecond = 3f;

    [Header("Warm-up")]
    [Tooltip("Seconds from trigger-press to full damage. Beam appears immediately but deals 0→full damage over this window.")]
    [SerializeField] private float warmUpDuration = 0.25f;

    [Header("Vibration")]
    [Tooltip("Peak positional shake magnitude on the weapon mesh while the beam is active.")]
    [SerializeField] private float vibrationAmplitude = 0.004f;
    [Tooltip("Speed of the Perlin-noise vibration cycle.")]
    [SerializeField] private float vibrationSpeed = 55f;

    [Header("Laser Audio")]
    [Tooltip("Played once when the beam first fires. Overwrites any previous init sound in progress.")]
    [SerializeField] private AudioClip laserInitClip;
    [Tooltip("Looped continuously while the beam is held. Fades out after releasing.")]
    [SerializeField] private AudioClip laserLoopClip;
    [Tooltip("Dedicated AudioSource for the one-shot init sound.")]
    [SerializeField] private AudioSource laserInitSource;
    [Tooltip("Dedicated AudioSource for the looping beam sound. Set Loop = true in the Inspector.")]
    [SerializeField] private AudioSource laserLoopSource;
    [Tooltip("Seconds for the loop to fade from full volume to silent after releasing fire.")]
    [SerializeField] private float loopFadeOutDuration = 0.35f;

    // ─── Private ───────────────────────────────────────────────────────────────

    private MeshRenderer cylinderCore;
    private MeshRenderer cylinderOuter;

    // Unity's built-in cylinder mesh: 2 units tall, 1 unit diameter, Y-axis aligned.
    private const float CylinderHalfHeight = 1f;

    // Vibration state.
    private Vector3 vibrationBaseLocalPos;
    private bool    vibrationBaseCached;
    private float   vibrationSeedX;
    private float   vibrationSeedZ;

    private bool  isFiring;
    private int   lastFireFrame = -1;
    private float warmUpTimer;
    private bool  beamWasActive; // true if beam was active on the previous frame

    private IDamageable currentTarget;
    private float       damageAccumulator;

    // Audio state.
    private float     loopTargetVolume;   // volume the loop is trying to reach
    private Coroutine loopFadeCoroutine;  // running fade-out, cancelled on re-fire

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildCylinders();

        // Random seed so each weapon instance vibrates on its own Perlin track.
        vibrationSeedX = Random.Range(0f, 100f);
        vibrationSeedZ = Random.Range(0f, 100f);
    }

    /// <summary>
    /// LateUpdate runs after ArmMount360.Update which calls TryFire.
    /// If TryFire wasn't called this frame, the beam is released.
    /// </summary>
    private void LateUpdate()
    {
        if (lastFireFrame != Time.frameCount)
            StopBeam();
    }

    // ─── ModularWeapon override ────────────────────────────────────────────────

    /// <summary>
    /// Called every frame Fire1 is held and this weapon is centred.
    /// Drains energy per-second instead of per-shot.
    /// </summary>
    public override bool TryFire(ArmBattery battery, Camera playerCamera, float centerMultiplier = 1f)
    {
        float cost = energyPerSecond * Time.deltaTime * (IsCenter ? centerMultiplier : 1f);
        if (battery == null || !battery.Consume(cost))
        {
            StopBeam();
            return false;
        }

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

        // ── Aim direction ──────────────────────────────────────────────────────
        Vector3 origin    = firePoint.position;
        Vector3 direction = firePoint.forward;

        if (IsCenter && useCrosshairWhenCentered && playerCamera != null)
        {
            direction = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction;
        }

        // ── Physics cast ───────────────────────────────────────────────────────
        bool       didHit;
        RaycastHit hit;

        if (beamRadius > 0f)
            didHit = Physics.SphereCast(origin, beamRadius, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);
        else
            didHit = Physics.Raycast(origin, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        Vector3 endPoint = didHit ? hit.point : origin + direction * range;

        // ── Cylinder pulse — sine-wave on diameter while fully charged ─────────
        float pulse = warmUpRatio >= 1f
            ? Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude
            : 0f;

        UpdateCylinders(origin, endPoint, warmUpRatio, pulse);
        SetCylindersActive(true);

        // ── Gun vibration ──────────────────────────────────────────────────────
        ApplyVibration(warmUpRatio);

        // ── Audio ──────────────────────────────────────────────────────────────
        if (!beamWasActive)
            OnBeamStart();
        else
            TickBeamAudio();

        beamWasActive = true;

        // ── Muzzle particles — continuous while firing ─────────────────────────
        if (muzzleFlash != null && !muzzleFlash.isPlaying)
            muzzleFlash.Play();

        // ── Impact particles — teleport to hit point each frame ────────────────
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

        // ── Damage tick ────────────────────────────────────────────────────────
        if (didHit && warmUpRatio >= 1f)
        {
            IDamageable damageable = hit.collider.GetComponent<IDamageable>()
                                  ?? hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageAccumulator += damagePerSecond * centerMultiplier * Time.deltaTime;

                if (damageAccumulator >= 1f)
                {
                    float toApply     = Mathf.Floor(damageAccumulator);
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
    }

    private void StopBeam()
    {
        if (beamWasActive)
            OnBeamStop();

        SetCylindersActive(false);
        StopVibration();

        warmUpTimer       = 0f;
        damageAccumulator = 0f;
        currentTarget     = null;
        beamWasActive     = false;

        if (muzzleFlash     != null && muzzleFlash.isPlaying)     muzzleFlash.Stop();
        if (impactParticles != null && impactParticles.isPlaying) impactParticles.Stop();
    }

    // ─── Laser audio ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the first frame the beam becomes active.
    /// Plays the init one-shot (overwriting any still-playing copy) then
    /// starts or resumes the loop with full volume.
    /// </summary>
    private void OnBeamStart()
    {
        // Init sound — Stop first so rapid re-triggers never stack copies.
        if (laserInitSource != null && laserInitClip != null)
        {
            laserInitSource.Stop();
            laserInitSource.clip = laserInitClip;
            laserInitSource.loop = false;
            laserInitSource.Play();
        }

        // Cancel any in-progress fade and restore loop to full volume.
        if (loopFadeCoroutine != null)
        {
            StopCoroutine(loopFadeCoroutine);
            loopFadeCoroutine = null;
        }

        if (laserLoopSource != null && laserLoopClip != null)
        {
            if (!laserLoopSource.isPlaying)
            {
                laserLoopSource.clip   = laserLoopClip;
                laserLoopSource.loop   = true;
                laserLoopSource.volume = 1f;
                laserLoopSource.Play();
            }
            else
            {
                // Was already playing (fade-out cancelled) — snap volume back.
                laserLoopSource.volume = 1f;
            }

            loopTargetVolume = 1f;
        }
    }

    /// <summary>
    /// Called every frame while the beam continues to fire (after the first).
    /// Guards against the loop stopping for any external reason.
    /// </summary>
    private void TickBeamAudio()
    {
        if (laserLoopSource != null && laserLoopClip != null && !laserLoopSource.isPlaying)
        {
            laserLoopSource.clip   = laserLoopClip;
            laserLoopSource.loop   = true;
            laserLoopSource.volume = loopTargetVolume;
            laserLoopSource.Play();
        }
    }

    /// <summary>
    /// Called on the first frame the beam stops.
    /// Stops the init sound immediately and begins the fade-out coroutine for the loop.
    /// </summary>
    private void OnBeamStop()
    {
        if (laserInitSource != null && laserInitSource.isPlaying)
            laserInitSource.Stop();

        if (laserLoopSource != null && laserLoopSource.isPlaying && loopFadeOutDuration > 0f)
        {
            if (loopFadeCoroutine != null) StopCoroutine(loopFadeCoroutine);
            loopFadeCoroutine = StartCoroutine(FadeOutLoop(loopFadeOutDuration));
        }
        else if (laserLoopSource != null)
        {
            laserLoopSource.Stop();
        }
    }

    /// <summary>
    /// Fades the loop AudioSource volume from its current value to 0 over
    /// <paramref name="duration"/> seconds, then stops it.
    /// If <see cref="OnBeamStart"/> is called during the fade it cancels this
    /// coroutine and the loop continues at full volume — no copy ever stacks.
    /// </summary>
    private IEnumerator FadeOutLoop(float duration)
    {
        float startVolume = laserLoopSource.volume;
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            laserLoopSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        laserLoopSource.volume = 0f;
        laserLoopSource.Stop();
        loopFadeCoroutine = null;
    }

    // ─── Vibration ────────────────────────────────────────────────────────────

    /// <summary>
    /// Perlin-noise positional shake applied to recoilRoot while firing.
    /// Magnitude scales with warmUpRatio so it ramps in during warm-up.
    /// </summary>
    private void ApplyVibration(float warmUpRatio)
    {
        if (recoilRoot == null || vibrationAmplitude <= 0f) return;

        // Cache the neutral position once so StopVibration can restore it cleanly.
        if (!vibrationBaseCached)
        {
            vibrationBaseLocalPos = recoilRoot.localPosition;
            vibrationBaseCached   = true;
        }

        float t      = Time.time * vibrationSpeed;
        float shakeX = (Mathf.PerlinNoise(vibrationSeedX + t, 0f)  - 0.5f) * 2f;
        float shakeZ = (Mathf.PerlinNoise(0f, vibrationSeedZ + t)  - 0.5f) * 2f;
        float mag    = vibrationAmplitude * warmUpRatio;

        recoilRoot.localPosition = vibrationBaseLocalPos
            + recoilRoot.right   * (shakeX * mag)
            + recoilRoot.forward * (shakeZ * mag * 0.4f);
    }

    private void StopVibration()
    {
        if (recoilRoot != null && vibrationBaseCached)
            recoilRoot.localPosition = vibrationBaseLocalPos;

        vibrationBaseCached = false;
    }

    // ─── Cylinder helpers ─────────────────────────────────────────────────────

    private void BuildCylinders()
    {
        GameObject temp   = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);

        Material coreMat  = beamMaterialCore  != null ? beamMaterialCore  : CreateBeamMaterial(beamColorInner, opaque: true);
        Material outerMat = beamMaterialOuter != null ? beamMaterialOuter : CreateBeamMaterial(beamColorOuter, opaque: false);

        cylinderCore  = CreateCylinderMesh("Beam_Core",  cylinderMesh, coreMat);
        cylinderOuter = CreateCylinderMesh("Beam_Outer", cylinderMesh, outerMat);

        SetCylindersActive(false);
    }

    private MeshRenderer CreateCylinderMesh(string goName, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(goName);
        go.layer      = LayerMask.NameToLayer("Ignore Raycast");
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr         = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial       = mat;
        mr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows       = false;
        mr.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return mr;
    }

    /// <summary>
    /// Positions and scales both cylinders to span <paramref name="start"/>→<paramref name="end"/>.
    /// Unity cylinder: 2 units tall (scale.Y = length/2), 1 unit wide (scale.XZ = diameter).
    /// <paramref name="pulse"/> adds a sine-wave modulation to diameter once fully charged.
    /// </summary>
    private void UpdateCylinders(Vector3 start, Vector3 end, float warmUpRatio, float pulse)
    {
        Vector3 delta  = end - start;
        float   length = delta.magnitude;
        if (length < 0.001f) return;

        Vector3    mid      = start + delta * 0.5f;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
        float      halfLen  = length / (CylinderHalfHeight * 2f);

        float coreDiam  = Mathf.Max(0f, Mathf.Lerp(0f, beamStartWidth,        warmUpRatio) + beamStartWidth        * pulse);
        float outerDiam = Mathf.Max(0f, Mathf.Lerp(0f, beamStartWidth * 3.5f, warmUpRatio) + beamStartWidth * 3.5f * pulse * 0.6f);

        if (cylinderCore != null)
        {
            cylinderCore.transform.SetPositionAndRotation(mid, rotation);
            cylinderCore.transform.localScale = new Vector3(coreDiam, halfLen, coreDiam);
        }

        if (cylinderOuter != null)
        {
            cylinderOuter.transform.SetPositionAndRotation(mid, rotation);
            cylinderOuter.transform.localScale = new Vector3(outerDiam, halfLen, outerDiam);
        }
    }

    private void SetCylindersActive(bool active)
    {
        if (cylinderCore  != null && cylinderCore.enabled  != active) cylinderCore.enabled  = active;
        if (cylinderOuter != null && cylinderOuter.enabled != active) cylinderOuter.enabled = active;
    }

    private static Material CreateBeamMaterial(Color color, bool opaque)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.enableInstancing = true;

        if (opaque)
        {
            mat.SetFloat("_Surface", 0f);
        }
        else
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend",   4f); // Additive
            mat.SetFloat("_Cull",    0f); // Off
            mat.renderQueue = 3001;
        }

        return mat;
    }

    private void OnDisable()
    {
        if (loopFadeCoroutine != null)
        {
            StopCoroutine(loopFadeCoroutine);
            loopFadeCoroutine = null;
        }

        if (laserInitSource != null) laserInitSource.Stop();
        if (laserLoopSource != null) laserLoopSource.Stop();

        StopBeam();
        isFiring = false;
    }
}
