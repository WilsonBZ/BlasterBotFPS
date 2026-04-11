using UnityEngine;

/// <summary>
/// Drives an expanding orb mesh at the GazGun firing point to visualise charge progress.
/// Requires a sibling or parent ChargeWeapon component on the weapon root.
/// Uses a MaterialPropertyBlock — zero per-frame GC, no material cloning.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class ChargeOrbVFX : MonoBehaviour
{
    [Header("Scale")]
    [Tooltip("Orb diameter at charge ratio 0 (fully released it snaps to this before hiding).")]
    [SerializeField] private float minScale = 0f;
    [Tooltip("Orb diameter at charge ratio 1.")]
    [SerializeField] private float maxScale = 0.55f;
    [Tooltip("How quickly the orb scales up/down (lerp speed).")]
    [SerializeField] private float scaleSpeed = 12f;

    [Header("Color")]
    [SerializeField] private Color minChargeColor = new Color(0.44f, 0.9f, 1f, 0.55f);   // cyan
    [SerializeField] private Color maxChargeColor = new Color(1f, 0.3f, 0.05f, 0.95f);   // orange

    [Header("Pulse")]
    [Tooltip("Additive pulsing amplitude on top of the base scale when fully charged.")]
    [SerializeField] private float pulseAmplitude = 0.06f;
    [Tooltip("Pulse frequency (Hz).")]
    [SerializeField] private float pulseFrequency = 6f;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private ChargeWeapon chargeWeapon;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float currentScale;

    private void Awake()
    {
        meshRenderer  = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        // Walk up the hierarchy to find the ChargeWeapon on the weapon root.
        chargeWeapon = GetComponentInParent<ChargeWeapon>();
        if (chargeWeapon == null)
            Debug.LogWarning("[ChargeOrbVFX] No ChargeWeapon found in parent hierarchy.", this);

        // Start hidden.
        meshRenderer.enabled = false;
        currentScale         = 0f;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (chargeWeapon == null) return;

        float ratio = chargeWeapon.ChargeRatio;
        bool  isCharging = ratio > 0f;

        meshRenderer.enabled = isCharging;

        if (!isCharging)
        {
            currentScale = 0f;
            transform.localScale = Vector3.zero;
            return;
        }

        // Scale — lerp toward target, add pulse at full charge.
        float pulse       = chargeWeapon.IsFullyCharged
            ? Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude
            : 0f;
        float targetScale = Mathf.Lerp(minScale, maxScale, ratio) + pulse;

        currentScale = Mathf.Lerp(currentScale, targetScale, scaleSpeed * Time.deltaTime);
        transform.localScale = Vector3.one * currentScale;

        // Color — set via MaterialPropertyBlock, no material clone / GC.
        Color color = Color.Lerp(minChargeColor, maxChargeColor, ratio);
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorID, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
