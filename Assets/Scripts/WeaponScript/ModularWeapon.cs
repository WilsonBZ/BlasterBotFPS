using System.Collections;
using UnityEngine;


public class ModularWeapon : MonoBehaviour
{
    [Header("Firing Settings")]
    [SerializeField] protected int pellets = 12;
    [SerializeField] protected float spreadAngle = 8f;
    [SerializeField] protected float fireRate = 0.75f;
    [SerializeField] protected float reloadTime = 1.5f;
    [SerializeField] protected int maxAmmo = 6;
    [SerializeField] protected GameObject projectilePrefab;
    [Tooltip("Energy cost consumed from ArmBattery per full-shot (per Fire call)")]
    [SerializeField] protected float energyCostPerShot = 1f;


    [Header("Recoil Settings")]
    [SerializeField] protected float recoilAmount = 2f;
    [SerializeField] protected float recoilRecoverySpeed = 5f;
    [SerializeField] protected Vector3 kickbackAmount = new Vector3(9f, 0.5f, 0f);
    [SerializeField] protected float kickbackRecoverySpeed = 10f;
    [SerializeField] protected Vector3 recoilRotation = new Vector3(-10f, .5f, -.4f);
    [SerializeField] protected float rotationRecoverySpeed = 8f;


    [Header("Crosshair Settings")]
    [SerializeField] protected float crosshairBloom = 20f;
    [SerializeField] protected float crosshairRecoveryRate = 8f;
    [SerializeField] protected RectTransform crosshair;


    [Header("References")]
    [SerializeField] protected Transform firePoint;
    [SerializeField] public Camera playerCamera;
    [SerializeField] protected Transform weaponPivot;
    [Header("Effects")]
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip shootSound;


    [Header("Behavior")]
    [Tooltip("If true the weapon listens to Fire1 input and behaves as player's main gun.")]
    public bool isMainGun = true;


    [Tooltip("Allow this weapon to apply recoil / camera shake when fired.")]
    public bool applyRecoil = true;


    // runtime
    protected int currentAmmo;
    protected float nextFireTime;
    protected Vector3 originalWeaponPosition;
    protected Quaternion originalWeaponRotation;
    protected float currentRecoil;
    protected float currentCrosshairSize;
    protected float originalCrosshairSize;


    protected virtual void Awake()
    {
        currentAmmo = maxAmmo;
        if (weaponPivot != null)
        {
            originalWeaponPosition = weaponPivot.localPosition;
            originalWeaponRotation = weaponPivot.localRotation;
        }


        if (crosshair)
        {
            currentCrosshairSize = crosshair.sizeDelta.x;
            originalCrosshairSize = currentCrosshairSize;
        }


        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;
    }
    protected virtual void Update()
    {
        HandleRecoilRecovery();
        HandleCrosshairRecovery();


        if (isMainGun)
        {
            if (Input.GetButtonDown("Fire1") && Time.time >= nextFireTime && currentAmmo > 0)
            {
                FireInternal();
            }


            if (Input.GetKeyDown(KeyCode.R))
            {
                StartCoroutine(Reload());
            }
        }
    }

    public virtual bool TryFire(ArmBattery battery = null)
    {
        if (Time.time < nextFireTime) return false;
        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return false;
        }


        if (battery != null && energyCostPerShot > 0f)
        {
            if (!battery.Consume(energyCostPerShot))
                return false; 
        }


        FireInternal();
        return true;
    }

    protected virtual void FireInternal()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;


        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);


        Vector3 perfectForward = (playerCamera != null)
        ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)).direction
        : (firePoint != null ? firePoint.forward : transform.forward);


        for (int i = 0; i < pellets; i++)
        {
            Vector3 spreadDirection = CalculateSpread(perfectForward);
            if (projectilePrefab == null || firePoint == null) continue;


            GameObject projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(spreadDirection)
            );


            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            var proj = projectile.GetComponent<Projectile>();
            float speed = (proj != null) ? proj.Speed : 30f;


            if (rb)
            {
                SetRigidbodyVelocitySafe(rb, spreadDirection * speed);
            }
        }

        ApplyRecoil();


        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    protected Vector3 CalculateSpread(Vector3 perfectForward)
    {
        Vector3 right = Vector3.Cross(perfectForward, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(perfectForward, right).normalized;


        float angle = Random.Range(0f, 2f * Mathf.PI);
        float distance = Random.Range(0f, spreadAngle * Mathf.Deg2Rad);


        Vector3 spreadDirection = perfectForward
        + right * Mathf.Sin(angle) * distance
        + up * Mathf.Cos(angle) * distance;


        return spreadDirection.normalized;
    }


    protected virtual void ApplyRecoil()
    {
        if (!applyRecoil) return;


        currentRecoil += recoilAmount;


        if (weaponPivot != null)
            weaponPivot.localPosition += new Vector3(0, 0, kickbackAmount.z);


        if (weaponPivot != null)
            weaponPivot.localRotation *= Quaternion.Euler(recoilRotation);


        if (crosshair)
        {
            currentCrosshairSize += crosshairBloom;
            crosshair.sizeDelta = new Vector2(currentCrosshairSize, currentCrosshairSize);
        }


        if (playerCamera != null)
            StartCoroutine(ScreenShake(0.3f, 0.15f));
    }

    private IEnumerator ScreenShake(float duration, float magnitude)
    {
        if (playerCamera == null) yield break;


        //Vector3 originalCamPos = playerCamera.transform.localPosition;
        float elapsed = 0f;


        while (elapsed < duration / 2f)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(0f, 2f) * magnitude;


            //playerCamera.transform.localPosition = originalCamPos + new Vector3(x, y, 0);


            elapsed += Time.deltaTime;
            yield return null;
        }


        //playerCamera.transform.localPosition = originalCamPos;
    }

    protected virtual void HandleRecoilRecovery()
    {
        if (weaponPivot != null)
        {
            weaponPivot.localPosition = Vector3.Lerp(
            weaponPivot.localPosition,
            originalWeaponPosition,
            kickbackRecoverySpeed * Time.deltaTime
            );


            weaponPivot.localRotation = Quaternion.Slerp(
            weaponPivot.localRotation,
            originalWeaponRotation,
            rotationRecoverySpeed * Time.deltaTime
            );
        }


        if (currentRecoil > 0)
        {
            float recoilRecovery = recoilRecoverySpeed * Time.deltaTime;
            if (playerCamera != null)
                playerCamera.transform.Rotate(-recoilRecovery, 0, 0);
            currentRecoil = Mathf.Max(0, currentRecoil - recoilRecovery);
        }
    }

    protected virtual void HandleCrosshairRecovery()
    {
        if (!crosshair) return;


        currentCrosshairSize = Mathf.Lerp(currentCrosshairSize, originalCrosshairSize, crosshairRecoveryRate * Time.deltaTime);


        crosshair.sizeDelta = new Vector2(
        Mathf.Max(10f, currentCrosshairSize),
        Mathf.Max(10f, currentCrosshairSize)
        );
    }


    protected virtual IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
    }


    public virtual void Equip()
    {
        gameObject.SetActive(true);
        currentAmmo = maxAmmo;
    }


    public virtual void Unequip()
    {
        gameObject.SetActive(false);
    }


    public virtual void BuffPellets(int extra)
    {
        pellets += extra;
    }


    public virtual void BuffFireRate(float reduction)
    {
        fireRate = Mathf.Max(0.05f, fireRate - reduction);
    }


    public virtual void BuffSpread(float reduction)
    {
        spreadAngle = Mathf.Max(0.1f, spreadAngle - reduction);
    }


    public float EnergyCostPerShot => energyCostPerShot;

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
        }
    }
}