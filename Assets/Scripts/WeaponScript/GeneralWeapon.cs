using System.Collections;
using UnityEngine;

public class GeneralWeapon : MonoBehaviour
{
    [Header("Firing Settings")]
    [SerializeField] private int pellets = 12;
    [SerializeField] private float spreadAngle = 8f;
    [SerializeField] private float fireRate = 0.75f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private int maxAmmo = 6;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Recoil Settings")]
    [SerializeField] private float recoilAmount = 2f;
    [SerializeField] private float recoilRecoverySpeed = 5f;
    [SerializeField] private Vector3 kickbackAmount = new Vector3(9, 0.5f, 0f);
    [SerializeField] private float kickbackRecoverySpeed = 10f;
    [SerializeField] private Vector3 recoilRotation = new Vector3(-10f, .5f, -.4f);
    [SerializeField] private float rotationRecoverySpeed = 8f;

    [Header("Crosshair Settings")]
    [SerializeField] private float crosshairBloom = 20f;
    [SerializeField] private float crosshairRecoveryRate = 8f;
    [SerializeField] private RectTransform crosshair;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform weaponPivot;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;

    public bool applyRecoil = true;
    //public SidegunManager sidegunManager;
    public bool isMainGun = true;
    private int currentAmmo;
    private float nextFireTime;
    private Vector3 originalWeaponPosition;
    private Quaternion originalWeaponRotation;
    private float currentRecoil;
    private float currentCrosshairSize;


    private void Awake()
    {
        currentAmmo = maxAmmo;
        originalWeaponPosition = weaponPivot.localPosition;
        originalWeaponRotation = weaponPivot.localRotation;

        if (crosshair) currentCrosshairSize = crosshair.sizeDelta.x;
    }

    void Start()
    {
        //if (sidegunManager != null)
        //    sidegunManager.Init(this);
    }

    private void Update()
    {
        HandleRecoilRecovery();
        HandleCrosshairRecovery();

        if (isMainGun)
        {
            if (Input.GetButtonDown("Fire1") && Time.time >= nextFireTime && currentAmmo > 0)
            {
                Fire();
                //if (sidegunManager) sidegunManager.FireAll();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                StartCoroutine(Reload());
            }
        }
    }

    public void Fire()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);

        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 perfectForward = aimRay.direction;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 spreadDirection = CalculateSpread(perfectForward);
            GameObject projectile = Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.LookRotation(spreadDirection)
            );

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb) rb.linearVelocity = spreadDirection * projectile.GetComponent<Projectile>().Speed;
        }

        ApplyRecoil();


        if (currentAmmo <= 0) StartCoroutine(Reload());
    }

    private Vector3 CalculateSpread(Vector3 perfectForward)
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

    private void ApplyRecoil()
    {
        if (!applyRecoil) return;
        currentRecoil += recoilAmount;

        weaponPivot.localPosition += new Vector3(0, 0, kickbackAmount.z);

        weaponPivot.localRotation *= Quaternion.Euler(recoilRotation);

        if (crosshair)
        {
            currentCrosshairSize += crosshairBloom;
            crosshair.sizeDelta = new Vector2(currentCrosshairSize, currentCrosshairSize);
        }

        StartCoroutine(ScreenShake(0.3f, 0.15f));
    }



    private IEnumerator ScreenShake(float duration, float magnitude)
    {
        Vector3 originalCamPos = playerCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration / 2)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(0f, 2f) * magnitude;

            playerCamera.transform.localPosition = originalCamPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.transform.localPosition = originalCamPos;
    }

    private void HandleRecoilRecovery()
    {
        weaponPivot.localPosition = Vector3.Lerp(
            weaponPivot.localPosition,
            originalWeaponPosition,
            kickbackRecoverySpeed * Time.deltaTime
        );

        weaponPivot.localRotation = Quaternion.Slerp(
            weaponPivot.localRotation,
            originalWeaponRotation,
            recoilRecoverySpeed * Time.deltaTime
        );

        weaponPivot.localRotation = Quaternion.Slerp(
            weaponPivot.localRotation,
            originalWeaponRotation,
            rotationRecoverySpeed * Time.deltaTime
        );

        if (currentRecoil > 0)
        {
            float recoilRecovery = recoilRecoverySpeed * Time.deltaTime;
            playerCamera.transform.Rotate(-recoilRecovery, 0, 0);
            currentRecoil = Mathf.Max(0, currentRecoil - recoilRecovery);
        }
    }

    private void HandleCrosshairRecovery()
    {
        if (!crosshair) return;

        currentCrosshairSize = Mathf.Lerp(
            currentCrosshairSize,
            currentCrosshairSize - crosshairBloom,
            crosshairRecoveryRate * Time.deltaTime
        );

        crosshair.sizeDelta = new Vector2(
            Mathf.Max(10f, currentCrosshairSize),
            Mathf.Max(10f, currentCrosshairSize)
        );
    }

    private IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
    }

    public void Equip()
    {
        gameObject.SetActive(true);
        currentAmmo = maxAmmo;
    }

    public void Unequip()
    {
        gameObject.SetActive(false);
    }

    public void BuffPellets(int extra)
    {
        pellets += extra;
    }

    public void BuffFireRate(float reduction)
    {
        fireRate = Mathf.Max(0.1f, fireRate - reduction);
    }

    public void BuffSpread(float reduction)
    {
        spreadAngle = Mathf.Max(1f, spreadAngle - reduction);
    }

}
