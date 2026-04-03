using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(Collider))]
public class ModularWeapon : MonoBehaviour
{
    [Header("Base Firing Stats")]
    [SerializeField] private int basePellets = 8;
    [Tooltip("Base spread angle in degrees")]
    [SerializeField] private float baseSpreadAngle = 6f;
    [Tooltip("Shots per second")]
    public float fireRate = 3f;
    [Tooltip("Energy consumed per full shot (before center multiplier)")]
    public float energyCostPerShot = 1f;

    [Header("References")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    [Tooltip("If true, when this weapon is the center it will aim at Camera.main viewport center")]
    public bool useCrosshairWhenCentered = true;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public MuzzleFlash muzzleFlashVFX;
    [Tooltip("VFX Graph muzzle flash child under FiringPoint (Firepoint.prefab). Driven via SendEvent OnPlay.")]
    [SerializeField] private VisualEffect muzzleVFXGraph;
    public AudioSource audioSource;
    public AudioClip shootSound;
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("Recoil")]
    [Tooltip("Vertical recoil applied per shot (degrees)")]
    public float recoilAngle = 2f;
    [Tooltip("Random variation applied to recoil (0 = no variation, 0.5 = ±50%)")]
    [Range(0f, 1f)]
    public float recoilRandomness = 0.2f;
    [Tooltip("How quickly recoil settles back to neutral (higher = faster)")]
    public float recoilReturnSpeed = 10f;
    [Tooltip("Optional transform to apply recoil to. If null, will use firePoint when available, otherwise the weapon root.")]
    public Transform recoilRoot;

    [Header("Fire Animation")]
    [Tooltip("Animator on the weapon mesh. If null, searched in children on Start.")]
    public Animator weaponAnimator;
    [Tooltip("Name of the animation state to play on each shot (must exist in the Animator Controller).")]
    public string fireAnimationState = "Fire";

    [Header("Aim Raycast")]
    [Tooltip("Layers excluded from the crosshair aim raycast. Player and Ring are always excluded automatically.")]
    public LayerMask aimRaycastExcludeLayers = 0;

    [Tooltip("How far ahead of the firePoint (along fire direction) the projectile spawns. Increase if projectiles hit ring geometry.")]
    public float spawnForwardOffset = 0.25f;

    // Cached layer mask computed once in Start.
    private int aimRaycastMask;
    private int pelletBonus = 0;
    private float spreadMultiplier = 1f;

    // ===== Runtime =====
    private float lastShotTime = -999f;
    public bool IsCenter { get; private set; } = false;
    private int fireSoundIndex;

    ArmMount360 parentMount = null;
    int parentSlotIndex = -1;

    Quaternion recoilOriginalLocalRotation;
    float recoilTarget = 0f;
    float recoilCurrent = 0f;

    // ===== Derived Stats =====
    public int Pellets => Mathf.Max(1, basePellets + pelletBonus);
    public float SpreadAngle => Mathf.Max(0.1f, baseSpreadAngle * spreadMultiplier);

    void Start()
    {
        CacheRecoilRootAndOriginal();
        AutoFindMuzzleVFXGraph();
        BuildAimRaycastMask();

        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<Animator>(true);
    }

    void OnEnable()
    {
        CacheRecoilRootAndOriginal();
        BuildAimRaycastMask();
    }

    /// <summary>Builds the aim raycast layer mask, always excluding Player and Ring.</summary>
    private void BuildAimRaycastMask()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int ringLayer   = LayerMask.NameToLayer("Ring");

        aimRaycastMask = Physics.DefaultRaycastLayers;
        if (playerLayer >= 0) aimRaycastMask &= ~(1 << playerLayer);
        if (ringLayer   >= 0) aimRaycastMask &= ~(1 << ringLayer);
        aimRaycastMask &= ~aimRaycastExcludeLayers.value;
    }

    /// <summary>Searches child of firePoint for a VisualEffect if not manually assigned.</summary>
    private void AutoFindMuzzleVFXGraph()
    {
        if (muzzleVFXGraph != null || firePoint == null) return;
        muzzleVFXGraph = firePoint.GetComponentInChildren<VisualEffect>(true);
    }

    void CacheRecoilRootAndOriginal()
    {
        if (recoilRoot == null)
            recoilRoot = (firePoint != null) ? firePoint : transform;

        recoilOriginalLocalRotation = recoilRoot.localRotation;
    }

    void Update()
    {
        recoilCurrent = Mathf.Lerp(recoilCurrent, recoilTarget, Time.deltaTime * recoilReturnSpeed * 1.5f);
        recoilTarget = Mathf.Lerp(recoilTarget, 0f, Time.deltaTime * recoilReturnSpeed * 0.7f);

        if (recoilRoot != null)
        {
            recoilRoot.localRotation =
                recoilOriginalLocalRotation * Quaternion.Euler(-recoilCurrent, 0f, 0f);
        }
    }

    public void SetParentMount(ArmMount360 mount, int slotIndex)
    {
        parentMount = mount;
        parentSlotIndex = slotIndex;
        CacheRecoilRootAndOriginal();
    }

    public void ClearParentMount()
    {
        parentMount = null;
        parentSlotIndex = -1;
        CacheRecoilRootAndOriginal();
    }

    public void SetCenterState(bool isCenter)
    {
        IsCenter = isCenter;
        CacheRecoilRootAndOriginal();
    }

    public virtual bool TryFire(ArmBattery battery, Camera playerCamera, float centerMultiplier = 1f)
    {
        float interval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < interval) return false;

        float cost = energyCostPerShot * (IsCenter ? centerMultiplier : 1f);

        if (battery == null || !battery.Consume(cost))
            return false;

        FireInternal(playerCamera);
        lastShotTime = Time.time;
        return true;
    }

    protected virtual void FireInternal(Camera playerCamera)
    {
        PlayMuzzleEffects();
        PlayFireSound();
        ApplyRecoil();
        PlayFireAnimation();

        Vector3 forwardDir = (firePoint != null) ? firePoint.forward : transform.forward;

        if (IsCenter && useCrosshairWhenCentered && playerCamera != null)
        {
            Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint;
            RaycastHit hit;
            float maxAimDistance = 1000f;

            if (Physics.Raycast(aimRay, out hit, maxAimDistance, aimRaycastMask, QueryTriggerInteraction.Ignore))
                aimPoint = hit.point;
            else
                aimPoint = aimRay.GetPoint(maxAimDistance);

            forwardDir = (firePoint != null)
                ? (aimPoint - firePoint.position).normalized
                : (aimPoint - transform.position).normalized;
        }

        for (int i = 0; i < Pellets; i++)
        {
            Vector3 dir = CalculateSpread(forwardDir);

            if (projectilePrefab == null || firePoint == null) continue;

            // Offset spawn forward so the projectile starts clear of the ring geometry.
            Vector3 spawnPos = firePoint.position + dir * spawnForwardOffset;
            GameObject proj = SpawnProjectile(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            GetComponent<HomingProjectileSpawnerHook>()?.OnProjectileFired(proj);

            Rigidbody rb = proj.GetComponent<Rigidbody>();
            Projectile p = proj.GetComponent<Projectile>();
            float speed = (p != null) ? p.Speed : 30f;

            if (rb != null)
                rb.linearVelocity = dir * speed;
        }
    }

    public void FireAtPoint(Vector3 aimPoint)
    {
        PlayMuzzleEffects();
        PlayFireSound();
        ApplyRecoil();

        Vector3 forwardDir = (firePoint != null)
            ? (aimPoint - firePoint.position).normalized
            : (aimPoint - transform.position).normalized;

        for (int i = 0; i < Pellets; i++)
        {
            Vector3 dir = CalculateSpread(forwardDir);

            if (projectilePrefab == null || firePoint == null) continue;

            Vector3 spawnPos = firePoint.position + dir * spawnForwardOffset;
            GameObject proj = SpawnProjectile(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            GetComponent<HomingProjectileSpawnerHook>()?.OnProjectileFired(proj);

            Rigidbody rb = proj.GetComponent<Rigidbody>();
            Projectile p = proj.GetComponent<Projectile>();
            float speed = (p != null) ? p.Speed : 30f;

            if (rb != null)
                rb.linearVelocity = dir * speed;
        }
    }

    /// <summary>Spawns a projectile via pool if available, otherwise falls back to Instantiate.</summary>
    protected static GameObject SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (PoolManager.Instance != null)
            return PoolManager.Instance.Get(prefab, position, rotation);
        return Instantiate(prefab, position, rotation);
    }

    /// <summary>Plays the fire animation state on the weapon Animator if one is assigned.</summary>
    protected void PlayFireAnimation()
    {
        if (weaponAnimator == null || string.IsNullOrEmpty(fireAnimationState)) return;
        weaponAnimator.Play(fireAnimationState, 0, 0f);
    }

    /// <summary>Plays all muzzle flash effects — particle system, procedural VFX, and VFX Graph.</summary>
    protected void PlayMuzzleEffects()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzleFlashVFX != null) muzzleFlashVFX.Play();
        if (muzzleVFXGraph != null) muzzleVFXGraph.SendEvent("OnPlay");
    }

    private void PlayFireSound()
    {
        if (audioSource == null || shootSound == null) return;
        float pitch = 1f + (fireSoundIndex % 2 == 0 ? -pitchVariation : pitchVariation);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(shootSound);
        fireSoundIndex++;
    }

    protected void ApplyRecoil()
    {
        if (recoilAngle <= 0f) return;
        float rand = Random.Range(1f - recoilRandomness, 1f + recoilRandomness);
        recoilTarget += recoilAngle * rand;
    }

    Vector3 CalculateSpread(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(0f, SpreadAngle * Mathf.Deg2Rad);

        Vector3 spreadDir =
            forward +
            right * Mathf.Sin(angle) * distance +
            up * Mathf.Cos(angle) * distance;

        return spreadDir.normalized;
    }

    // ===== Gizmos =====

    private void OnDrawGizmos()
    {
        if (firePoint == null) return;

        // Green ray = firePoint.forward (the direction ModularWeapon passes to projectiles).
        Gizmos.color = Color.green;
        Gizmos.DrawRay(firePoint.position, firePoint.forward * 2f);

        // Yellow sphere at the spawn origin.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(firePoint.position, 0.05f);

        // Red ray = firePoint.back, so you can instantly see if forward is flipped.
        Gizmos.color = Color.red;
        Gizmos.DrawRay(firePoint.position, -firePoint.forward * 0.5f);
    }

    public void TossOut(Vector3 direction, float forwardForce = 4f, float upForce = 1.2f)
    {
        transform.SetParent(null, true);
        ClearParentMount();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.AddForce(direction.normalized * forwardForce + Vector3.up * upForce, ForceMode.Impulse);
    }

    // ===== Buff API =====

    /// <summary>Adds pellets to this weapon.</summary>
    public void AddPellets(int amount)
    {
        pelletBonus += amount;
    }

    /// <summary>Reduces spread by a percentage (0.3 = 30% reduction).</summary>
    public void ReduceSpreadPercent(float percent)
    {
        spreadMultiplier *= (1f - percent);
    }
}
