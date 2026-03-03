using UnityEngine;

/// <summary>
/// Self-managing VFX component: expands a sphere from its current scale to
/// targetScale while fading alpha to zero, then destroys itself.
/// Attach via AddComponent immediately after creating a primitive sphere.
/// Takes ownership of the supplied Material and destroys it when done.
/// </summary>
public class VFXExpandAndFade : MonoBehaviour
{
    private Material material;
    private float startScale;
    private float targetScale;
    private float duration;
    private float elapsed;
    private Color startColor;
    private bool initialized;

    /// <summary>
    /// Call immediately after AddComponent.
    /// </summary>
    public void Initialize(Material mat, float targetScale, float duration)
    {
        material          = mat;
        startScale        = transform.localScale.x;
        this.targetScale  = targetScale;
        this.duration     = Mathf.Max(0.01f, duration);
        startColor        = mat.GetColor("_BaseColor");
        initialized       = true;

        // Failsafe: destroy even if Update is somehow skipped
        Destroy(gameObject, duration + 0.5f);
    }

    private void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);

        if (material != null)
        {
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            material.SetColor("_BaseColor", c);
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
