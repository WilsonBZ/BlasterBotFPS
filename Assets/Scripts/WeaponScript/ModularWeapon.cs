using System.Collections;
using UnityEngine;


public enum OnEmptyAction { Destroy, Drop }


[RequireComponent(typeof(Collider))]
public class ModularWeapon : MonoBehaviour
{
    [Header("Firing")]
    [Tooltip("Pellets spawned per shot (visual) has no effect on ammo count which is per-shot)")]
    public int pellets = 12;
    [Tooltip("Spread angle in degrees")]
    public float spreadAngle = 8f;
    [Tooltip("Shots per second")]
    public float fireRate = 2f;


    [Tooltip("Full-shot ammo count when picked up")]
    public int maxAmmo = 6;


    public GameObject projectilePrefab;
    public Transform firePoint; // direction for non-centered guns (and fallback)


    [Header("Empty behavior")]
    public OnEmptyAction onEmptyAction = OnEmptyAction.Destroy;
    public float dropForce = 4f;
    public float dropUpForce = 1.2f;


    [Header("Center behavior")]
    [Tooltip("If true this gun will use camera crosshair aiming when it's the center gun")]
    public bool useCrosshairWhenCentered = true;


    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;
    public AudioClip shootSound;


    // runtime
    protected int currentAmmo;
    protected float lastShotTime = -999f; // used only to honor fireRate (no separate cooldown variable)


    // parent info
    protected ArmMount parentMount = null;
    protected int parentSlotIndex = -1;


    // state
    public bool IsCenter { get; private set; } = false;

    protected virtual void Awake()
    {
        currentAmmo = Mathf.Max(0, maxAmmo);
    }


    /// <summary>
    /// Called by the mount when this becomes (or stops being) the center gun.
    /// </summary>
    public void SetCenterState(bool isCenter)
    {
        IsCenter = isCenter;
    }


    /// <summary>
    /// Set the mount/slot that owns this weapon.
    /// </summary>
    public void SetParentMount(ArmMount mount, int slotIndex)
    {
        parentMount = mount;
        parentSlotIndex = slotIndex;
    }


    public void ClearParentMount()
    {
        parentMount = null;
        parentSlotIndex = -1;
    }


    /// <summary>
    /// Attempt to fire. Battery can be null. Returns true if a shot was spawned.
    /// </summary>
    public bool TryFire(ArmBattery battery = null, Camera playerCamera = null)
    {
        // respect fireRate via lastShotTime only
        float minInterval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < minInterval) return false;


        if (currentAmmo <= 0)
        {
            HandleEmpty();
            return false;
        }

        if (battery != null && battery.GetMaxCharge() > 0f)
        {
            // weapons may have no energy cost; mount decides whether to pass battery.
        }


        FireInternal(playerCamera);


        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        lastShotTime = Time.time;


        if (currentAmmo <= 0)
            HandleEmpty();


        return true;
    }


    protected virtual void FireInternal(Camera playerCamera)
    {
        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);


        // Determine firing direction: if center and allowed, use camera crosshair; else use firePoint.forward
        Vector3 forwardDir = (firePoint != null) ? firePoint.forward : transform.forward;


        if (IsCenter && useCrosshairWhenCentered && playerCamera != null)
        {
            // aim toward center of screen
            Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            forwardDir = aimRay.direction;
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
                SetRigidbodyVelocitySafe(rb, dir * speed);
        }
    }

    protected Vector3 CalculateSpread(Vector3 forward)
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


    protected virtual void HandleEmpty()
    {
        if (parentMount != null && parentSlotIndex >= 0)
        {
            // tell mount to detach; mount will drop or destroy depending on parameter
            parentMount.DetachWeapon(parentSlotIndex, drop: (onEmptyAction == OnEmptyAction.Drop));
            // mount DetachWeapon will unparent or destroy the gameobject, so we return
            return;
        }


        if (onEmptyAction == OnEmptyAction.Drop)
        {
            transform.SetParent(null, true);
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            Vector3 popDir = (firePoint != null) ? firePoint.forward : transform.forward;
            rb.AddForce(popDir * dropForce + Vector3.up * dropUpForce, ForceMode.Impulse);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;


    protected void SetRigidbodyVelocitySafe(Rigidbody rb, Vector3 vel)
    {
        if (rb == null) return;


        // robust attempt to set velocity across APIs
        try { rb.linearVelocity = vel; } catch { /* ignore */ }
    }
}