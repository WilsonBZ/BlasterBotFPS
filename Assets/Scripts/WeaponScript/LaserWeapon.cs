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

    // 3-D cylinder beam objects.
    private MeshRenderer cylinderCore;
    private MeshRenderer cylinderOuter;

    // Unity's built-in cylinder mesh: 2 units tall, 1 unit diameter, Y-axis aligned.
    private const float CylinderHalfHeight = 1f; // half of the 2-unit default height
    private const float CylinderDiameter   = 1f; // default diameter in local space

    private bool isFiring;
    private int lastFireFrame = -1;   // frame TryFire was last called
    private float warmUpTimer;

    private IDamageable currentTarget;
    private float damageAccumulator;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildCylinders();
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
        UpdateCylinders(origin, endPoint, warmUpRatio);
        SetCylindersActive(true);

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
        SetCylindersActive(false);
        warmUpTimer       = 0f;
        damageAccumulator = 0f;
        currentTarget     = null;

        if (impactParticles != null && impactParticles.isPlaying)
            impactParticles.Stop();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // ─── Cylinder helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates core and outer cylinder GameObjects using the built-in cylinder mesh.
    /// No CapsuleCollider is added — we strip it immediately to keep it purely visual.
    /// </summary>
    private void BuildCylinders()
    {
        // Grab the shared cylinder mesh from a temporary primitive and discard the GO.
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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
        GameObject go    = new GameObject(goName);
        go.layer         = LayerMask.NameToLayer("Ignore Raycast");
        go.transform.SetParent(transform, false);

        MeshFilter mf    = go.AddComponent<MeshFilter>();
        mf.sharedMesh    = mesh;

        MeshRenderer mr             = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial           = mat;
        mr.shadowCastingMode        = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows           = false;
        mr.lightProbeUsage          = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage     = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return mr;
    }

    /// <summary>
    /// Positions and scales both cylinders so they span from <paramref name="start"/>
    /// to <paramref name="end"/> every frame.
    ///
    /// Unity's cylinder mesh is 2 units tall and 1 unit in diameter along its local Y axis.
    /// To align it with an arbitrary world direction:
    ///   - Position  = midpoint of start→end
    ///   - Rotation  = FromToRotation(Vector3.up, direction)
    ///   - Scale.Y   = length / 2   (halved because the mesh is 2 units, not 1)
    ///   - Scale.XZ  = desired diameter (mesh is already 1 unit wide)
    /// </summary>
    private void UpdateCylinders(Vector3 start, Vector3 end, float warmUpRatio)
    {
        Vector3 delta     = end - start;
        float   length    = delta.magnitude;
        if (length < 0.001f) return;

        Vector3    midPoint  = start + delta * 0.5f;
        Quaternion rotation  = Quaternion.FromToRotation(Vector3.up, delta / length);

        // Diameter grows from 0 to full during warm-up.
        float coreDiameter  = Mathf.Lerp(0f, beamStartWidth,        warmUpRatio);
        float outerDiameter = Mathf.Lerp(0f, beamStartWidth * 3.5f, warmUpRatio);
        float halfLength    = length / (CylinderHalfHeight * 2f);   // = length / 2

        if (cylinderCore != null)
        {
            Transform t     = cylinderCore.transform;
            t.position      = midPoint;
            t.rotation      = rotation;
            t.localScale    = new Vector3(coreDiameter, halfLength, coreDiameter);
        }

        if (cylinderOuter != null)
        {
            Transform t     = cylinderOuter.transform;
            t.position      = midPoint;
            t.rotation      = rotation;
            t.localScale    = new Vector3(outerDiameter, halfLength, outerDiameter);
        }
    }

    private void SetCylindersActive(bool active)
    {
        if (cylinderCore  != null && cylinderCore.enabled  != active) cylinderCore.enabled  = active;
        if (cylinderOuter != null && cylinderOuter.enabled != active) cylinderOuter.enabled = active;
    }

    /// <summary>
    /// Generates a minimal URP Unlit beam material at runtime.
    /// Used only when no material asset is assigned in the Inspector.
    /// </summary>
    private static Material CreateBeamMaterial(Color color, bool opaque)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.enableInstancing = true;

        if (opaque)
        {
            mat.SetFloat("_Surface", 0f); // Opaque
        }
        else
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend",   4f); // Additive
            mat.SetFloat("_Cull",    0f); // Off — visible from inside the cylinder
            mat.renderQueue = 3001;
        }

        return mat;
    }

    private void OnDisable()
    {
        StopBeam();
        isFiring = false;
    }
}
