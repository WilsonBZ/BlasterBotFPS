using System.Collections;
using UnityEngine;

/// <summary>
/// Hold Fire1 to charge, release to fire a scaled projectile.
/// Attach this instead of ModularWeapon on the weapon root.
/// </summary>
public class ChargeWeapon : ModularWeapon
{
    [Header("Charge Settings")]
    [SerializeField] private float minChargeTime = 0.4f;
    [SerializeField] private float maxChargeTime = 2.5f;

    [Header("Projectile Scaling")]
    [SerializeField] private GameObject chargeProjectilePrefab;
    [SerializeField] private float minDamage = 20f;
    [SerializeField] private float maxDamage = 150f;
    [SerializeField] private float minProjectileScale = 0.3f;
    [SerializeField] private float maxProjectileScale = 3f;

    [Header("Charge VFX")]
    [SerializeField] private ParticleSystem chargeParticles;
    [SerializeField] private Light chargeLight;
    [SerializeField] private Color minChargeColor = new Color(0.4f, 0.9f, 1f);
    [SerializeField] private Color maxChargeColor = new Color(1f, 0.3f, 0.05f);
    [SerializeField] private float maxLightIntensity = 8f;
    [SerializeField] private float maxLightRange = 5f;

    [Header("Charge Audio")]
    [SerializeField] private AudioClip chargeStartSound;
    [SerializeField] private AudioClip chargeFireSound;
    [SerializeField] private AudioClip chargeCancelSound;
    [SerializeField] private AudioSource chargeAudioSource;

    // ─── Runtime ────────────────────────────────────────────────────────────
    private bool isCharging;
    private float chargeStartTime;
    private ArmBattery cachedBattery;
    private float cachedCenterMultiplier = 1f;

    public float ChargeRatio => isCharging
        ? Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime)
        : 0f;

    public bool IsFullyCharged => isCharging && ChargeRatio >= 1f;

    // ─── Override TryFire ────────────────────────────────────────────────────

    /// <summary>
    /// Called by ArmMount360 every frame while Fire1 is held.
    /// We use it only to begin charging — we never fire immediately.
    /// </summary>
    public override bool TryFire(ArmBattery battery, Camera playerCamera, float centerMultiplier = 1f)
    {
        if (!IsCenter) return false;

        if (!isCharging)
        {
            cachedBattery = battery;
            cachedCenterMultiplier = centerMultiplier;
            BeginCharge();
        }

        return false; // charge weapons never fire on button-down
    }

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsCenter && isCharging)
        {
            CancelCharge();
            return;
        }

        if (isCharging && Input.GetButtonUp("Fire1"))
        {
            ReleaseCharge();
        }

        UpdateChargeVisuals();
    }

    // ─── Charge State ────────────────────────────────────────────────────────

    private void BeginCharge()
    {
        isCharging = true;
        chargeStartTime = Time.time;

        if (chargeParticles != null)
            chargeParticles.Play();

        if (chargeLight != null)
        {
            chargeLight.enabled = true;
            chargeLight.intensity = 0f;
        }

        PlayOneShot(chargeStartSound);
    }

    private void ReleaseCharge()
    {
        float elapsed = Time.time - chargeStartTime;

        if (elapsed >= minChargeTime)
        {
            float ratio = Mathf.Clamp01(elapsed / maxChargeTime);
            FireCharged(ratio);
        }
        else
        {
            PlayOneShot(chargeCancelSound);
        }

        EndCharge();
    }

    private void CancelCharge()
    {
        PlayOneShot(chargeCancelSound);
        EndCharge();
    }

    private void EndCharge()
    {
        isCharging = false;

        if (chargeParticles != null)
            chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (chargeLight != null)
            chargeLight.enabled = false;
    }

    // ─── Fire ────────────────────────────────────────────────────────────────

    private void FireCharged(float chargeRatio)
    {
        if (chargeProjectilePrefab == null || firePoint == null) return;

        // Consume battery proportional to charge
        if (cachedBattery != null)
        {
            float cost = energyCostPerShot * cachedCenterMultiplier * Mathf.Lerp(0.5f, 2f, chargeRatio);
            cachedBattery.Consume(cost);
        }

        // Aim direction — prefer crosshair raycast when centered
        Vector3 forward = firePoint.forward;
        if (useCrosshairWhenCentered && Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int playerLayer = LayerMask.NameToLayer("Player");
            int ringLayer   = LayerMask.NameToLayer("Ring");
            int mask = Physics.DefaultRaycastLayers;
            if (playerLayer >= 0) mask &= ~(1 << playerLayer);
            if (ringLayer   >= 0) mask &= ~(1 << ringLayer);

            forward = Physics.Raycast(ray, out RaycastHit hit, 1000f, mask, QueryTriggerInteraction.Ignore)
                ? (hit.point - firePoint.position).normalized
                : ray.direction;
        }

        GameObject proj = SpawnProjectile(chargeProjectilePrefab, firePoint.position, Quaternion.LookRotation(forward));

        ChargeProjectile cp = proj.GetComponent<ChargeProjectile>();
        if (cp != null)
        {
            float damage = Mathf.Lerp(minDamage, maxDamage, chargeRatio);
            float scale  = Mathf.Lerp(minProjectileScale, maxProjectileScale, chargeRatio);
            cp.Initialize(chargeRatio, damage, scale);
        }

        GetComponent<MolotovChargeWeaponHook>()?.OnProjectileFired(proj);

        PlayMuzzleEffects();

        ApplyRecoil();
        PlayOneShot(chargeFireSound);
    }

    // ─── Visuals ─────────────────────────────────────────────────────────────

    private void UpdateChargeVisuals()
    {
        if (!isCharging || chargeLight == null) return;

        float ratio = ChargeRatio;
        chargeLight.color     = Color.Lerp(minChargeColor, maxChargeColor, ratio);
        chargeLight.intensity = Mathf.Lerp(0f, maxLightIntensity, ratio);
        chargeLight.range     = Mathf.Lerp(1f, maxLightRange, ratio);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource src = chargeAudioSource != null ? chargeAudioSource : audioSource;
        if (src != null) src.PlayOneShot(clip);
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!IsCenter || !isCharging) return;

        // Debug charge bar — only in editor/development builds
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float ratio = ChargeRatio;
        Rect bg  = new Rect(Screen.width * 0.5f - 100f, Screen.height - 60f, 200f, 20f);
        Rect bar = new Rect(bg.x, bg.y, bg.width * ratio, bg.height);

        GUI.color = Color.black;
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = IsFullyCharged ? Color.red : Color.Lerp(Color.cyan, Color.yellow, ratio);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = Color.white;
#endif
    }
}
