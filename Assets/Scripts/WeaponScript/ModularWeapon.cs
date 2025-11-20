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
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;
    public AudioClip shootSound;

    float lastShotTime = -999f;
    public bool IsCenter { get; private set; } = false;

    ArmMount360 parentMount = null;
    int parentSlotIndex = -1;

    public void SetParentMount(ArmMount360 mount, int slotIndex)
    {
        parentMount = mount;
        parentSlotIndex = slotIndex;
    }

    public void ClearParentMount()
    {
        parentMount = null;
        parentSlotIndex = -1;
    }

    public void SetCenterState(bool isCenter)
    {
        IsCenter = isCenter;
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
        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shootSound) audioSource.PlayOneShot(shootSound);

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
