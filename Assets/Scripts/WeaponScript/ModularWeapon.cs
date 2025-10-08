using System.Collections;
using UnityEngine;

public enum OnEmptyAction
{
    Destroy,
    Drop
}

[RequireComponent(typeof(Collider))]
public class ModularWeapon : MonoBehaviour
{
    [Header("Firing Settings")]
    [SerializeField] protected int pellets = 12;
    [SerializeField] protected float spreadAngle = 8f;
    [Tooltip("Shots per second when firing repeatedly")]
    [SerializeField] protected float fireRate = 4f;
    [SerializeField] protected float reloadTime = 1.5f;

    [Tooltip("Number of full 'shots' (not pellets) this weapon has when picked up")]
    [SerializeField] protected int maxAmmo = 6;

    [SerializeField] protected GameObject projectilePrefab;
    [Tooltip("Energy cost consumed from ArmBattery per full-shot (per Fire call)")]
    [SerializeField] protected float energyCostPerShot = 0f;

    [Header("Auto/Semi auto")]
    [Tooltip("If true the weapon will fire repeatedly when the player holds the fire button (automatic). If false it only fires on button down.")]
    [SerializeField] protected bool isAutomatic = true;

    [Header("On-empty behavior")]
    [SerializeField] protected OnEmptyAction onEmptyAction = OnEmptyAction.Destroy;
    [Tooltip("When dropping (onEmptyAction==Drop) this force is applied in the forward direction")]
    [SerializeField] protected float dropForce = 4f;
    [SerializeField] protected float dropUpForce = 1.2f;

    [Header("Recoil / Visuals")]
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip shootSound;

    [Header("References")]
    [SerializeField] protected Transform firePoint; 
    [SerializeField] protected Transform weaponPivot;

    [Header("Behavior")]
    [Tooltip("If true the weapon listens to Fire1 input (when used as the main handheld gun)")]
    public bool isMainGun = true;

    [Tooltip("Allow this weapon to apply recoil / camera shake when fired.")]
    public bool applyRecoil = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    protected int currentAmmo;
    protected float nextFireTime;
    protected Vector3 originalWeaponPosition;
    protected Quaternion originalWeaponRotation;

    protected ArmMount parentMount = null;
    protected int parentSlotIndex = -1;

    protected virtual void Awake()
    {
        currentAmmo = Mathf.Max(0, maxAmmo);

        if (weaponPivot != null)
        {
            originalWeaponPosition = weaponPivot.localPosition;
            originalWeaponRotation = weaponPivot.localRotation;
        }
    }

    protected virtual void Update()
    {
        if (isMainGun)
        {
            if (isAutomatic)
            {
                if (Input.GetButton("Fire1")) TryFire(null);
            }
            else
            {
                if (Input.GetButtonDown("Fire1")) TryFire(null);
            }

            if (Input.GetKeyDown(KeyCode.R))
                StartCoroutine(Reload());
        }
    }
    public virtual bool TryFire(ArmBattery battery = null)
    {
        if (Time.time < nextFireTime)
        {
            if (debugLogs) Debug.Log($"{name} cannot fire: cooling down. Next = {nextFireTime:F2}, now = {Time.time:F2}");
            return false;
        }

        if (currentAmmo <= 0)
        {
            if (debugLogs) Debug.Log($"{name} cannot fire: no ammo.");
            HandleEmpty();
            return false;
        }

        if (battery != null && energyCostPerShot > 0f)
        {
            if (!battery.Consume(energyCostPerShot))
            {
                if (debugLogs) Debug.Log($"{name} cannot fire: battery insufficient.");
                return false; 
            }
        }

        FireInternal();

        // Decrement ammo (one full-shot consumed)
        currentAmmo = Mathf.Max(0, currentAmmo - 1);

        // If ammo finished, handle empty behavior
        if (currentAmmo <= 0)
            HandleEmpty();

        return true;
    }

    protected virtual void FireInternal()
    {
        nextFireTime = Time.time + (1f / Mathf.Max(0.0001f, fireRate));

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);

        // Important: fire in the direction the weapon is currently pointed at
        Vector3 forward = (firePoint != null) ? firePoint.forward : transform.forward;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = CalculateSpread(forward);

            if (projectilePrefab == null || firePoint == null) continue;

            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            var proj = projectile.GetComponent<Projectile>();
            float speed = (proj != null) ? proj.Speed : 30f;

            if (rb)
            {
                SetRigidbodyVelocitySafe(rb, dir * speed);
            }
        }

        // Optional: apply local recoil to weaponPivot
        if (applyRecoil && weaponPivot != null)
        {
            weaponPivot.localPosition += transform.forward * -0.05f;
        }

        if (debugLogs) Debug.Log($"{name} fired. Ammo remaining: {currentAmmo - 1}");
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
        // If attached to a mount, tell the mount to detach (returns the instance before destroyed)
        if (parentMount != null && parentSlotIndex >= 0)
        {
            var returned = parentMount.DetachWeapon(parentSlotIndex, drop: (onEmptyAction == OnEmptyAction.Drop));

            // If we were dropped back into the world, add a small pop force outward
            if (returned == this && onEmptyAction == OnEmptyAction.Drop)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
                Vector3 popDir = (firePoint != null) ? firePoint.forward : transform.forward;
                rb.AddForce(popDir * dropForce + Vector3.up * dropUpForce, ForceMode.Impulse);
            }

            return;
        }

        // If not attached, just either drop (unparent) or destroy
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

    /// <summary>
    /// Called by ArmMount when it attaches this weapon so the weapon knows its slot.
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

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsAutomatic => isAutomatic;

    protected IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
    }

    // Robust setter for Rigidbody velocity to handle Unity API changes (velocity vs linearVelocity)
    protected void SetRigidbodyVelocitySafe(Rigidbody rb, Vector3 vel)
    {
        if (rb == null) return;

        var t = rb.GetType();
        var prop = t.GetProperty("linearVelocity");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(rb, vel, null);
            return;
        }

        var field = t.GetField("linearVelocity");
        if (field != null)
        {
            field.SetValue(rb, vel);
            return;
        }

        prop = t.GetProperty("velocity");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(rb, vel, null);
            return;
        }

        field = t.GetField("velocity");
        if (field != null)
        {
            field.SetValue(rb, vel);
            return;
        }

        // Fallback
        rb.linearVelocity = vel;
    }
}
