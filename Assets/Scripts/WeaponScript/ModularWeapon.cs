using MoreMountains.Feedbacks;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ModularWeapon : MonoBehaviour
{
    [Header("Firing")]
    public int pellets = 8;
    [Tooltip("Spread angle in degrees")]
    public float spreadAngle = 6f;
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
    public MMF_Player muzzleFlash;
    public AudioSource audioSource;
    public AudioClip shootSound;

    [Header("Recoil")]
    [Tooltip("Vertical recoil applied per shot (degrees)")]
    public float recoilAngle = 2f;
    [Tooltip("Random variation applied to recoil (0 = no variation, 0.5 = ±50%)")]
    [Range(0f, 1f)]
    public float recoilRandomness = 0.2f;
    [Tooltip("How quickly recoil settles back to neutral (higher = faster)")]
    public float recoilReturnSpeed = 10f;
    [Tooltip("Optional transform to apply recoil to. If null, will use `firePoint` when available, otherwise the weapon root.")]
    public Transform recoilRoot;

    float lastShotTime = -999f;
    public bool IsCenter { get; private set; } = false;

    ArmMount360 parentMount = null;
    int parentSlotIndex = -1;

    // Recoil state (local pitch applied on top of original local rotation)
    Quaternion recoilOriginalLocalRotation;
    float recoilTarget = 0f;
    float recoilCurrent = 0f;

    void Start()
    {
        CacheRecoilRootAndOriginal();
    }

    void OnEnable()
    {
        CacheRecoilRootAndOriginal();
    }

    void CacheRecoilRootAndOriginal()
    {
        // Prefer explicit recoilRoot, then firePoint (so aim visuals move), then the weapon root.
        if (recoilRoot == null)
            recoilRoot = (firePoint != null) ? firePoint : transform;

        recoilOriginalLocalRotation = recoilRoot.localRotation;
    }

    void Update()
    {
        // Smoothly move current recoil toward target, and smoothly decay target toward zero.
        recoilCurrent = Mathf.Lerp(recoilCurrent, recoilTarget, Time.deltaTime * recoilReturnSpeed * 1.5f);
        recoilTarget = Mathf.Lerp(recoilTarget, 0f, Time.deltaTime * recoilReturnSpeed * 0.7f);

        if (recoilRoot != null)
        {
            // Apply recoil as a local pitch (negative to kick the muzzle up)
            recoilRoot.localRotation = recoilOriginalLocalRotation * Quaternion.Euler(-recoilCurrent, 0f, 0f);
        }
    }

    public void SetParentMount(ArmMount360 mount, int slotIndex)
    {
        parentMount = mount;
        parentSlotIndex = slotIndex;

        // Parent/mount likely changed local rotations: refresh base rotation for recoil root.
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

        // When weapon becomes/ceases center, its local transform may be changed by mount logic.
        CacheRecoilRootAndOriginal();
    }

    public bool TryFire(ArmBattery battery, Camera playerCamera, float centerMultiplier = 1f)
    {
        float interval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < interval) return false;

        float cost = energyCostPerShot * (IsCenter ? centerMultiplier : 1f);

        if (battery != null)
        {
            if (!battery.Consume(cost)) return false; 
        }
        else
        {

            return false;
        }

        FireInternal(playerCamera);
        lastShotTime = Time.time;
        return true;
    }

    protected virtual void FireInternal(Camera playerCamera)
    {
        muzzleFlash.PlayFeedbacks();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);

        ApplyRecoil();

        Vector3 forwardDir = (firePoint != null) ? firePoint.forward : transform.forward;

        if (IsCenter && useCrosshairWhenCentered && playerCamera != null)
        {
            Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint;
            RaycastHit hit;
            float maxAimDistance = 1000f;

            if (Physics.Raycast(aimRay, out hit, maxAimDistance))
            {
                aimPoint = hit.point;
            }
            else
            {
                aimPoint = aimRay.GetPoint(maxAimDistance);
            }

            if (firePoint != null)
                forwardDir = (aimPoint - firePoint.position).normalized;
            else
                forwardDir = (aimPoint - transform.position).normalized;
        }

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = CalculateSpread(forwardDir);

            if (projectilePrefab == null || firePoint == null) continue;

            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            var p = proj.GetComponent<Projectile>();
            float speed = (p != null) ? p.Speed : 30f;

            if (rb != null)
            {
                rb.linearVelocity = dir * speed;
            }
        }
    }

    // New method used by ability: spawn projectiles directly toward a world point (ignores battery)
    public void FireAtPoint(Vector3 aimPoint)
    {
        muzzleFlash.PlayFeedbacks();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);

        // apply recoil on firing
        ApplyRecoil();

        Vector3 forwardDir = (firePoint != null) ? (aimPoint - firePoint.position).normalized : (aimPoint - transform.position).normalized;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = CalculateSpread(forwardDir);

            if (projectilePrefab == null || firePoint == null) continue;

            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            var p = proj.GetComponent<Projectile>();
            float speed = (p != null) ? p.Speed : 30f;

            if (rb != null)
            {
                rb.linearVelocity = dir * speed;
            }
        }
    }

    void ApplyRecoil()
    {
        if (recoilAngle <= 0f) return;
        float rand = Random.Range(1f - recoilRandomness, 1f + recoilRandomness);
        recoilTarget += recoilAngle * rand;
    }

    Vector3 CalculateSpread(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;
        float angle = Random.Range(0f, 2f * Mathf.PI);
        float distance = Random.Range(0f, spreadAngle * Mathf.Deg2Rad);

        Vector3 spreadDirection = forward
            + right * Mathf.Sin(angle) * distance
            + up * Mathf.Cos(angle) * distance;
        return spreadDirection.normalized;
    }

    public void TossOut(Vector3 direction, float forwardForce = 4f, float upForce = 1.2f)
    {
        transform.SetParent(null, true);
        ClearParentMount();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.AddForce(direction.normalized * forwardForce + Vector3.up * upForce, ForceMode.Impulse);
    }
}
