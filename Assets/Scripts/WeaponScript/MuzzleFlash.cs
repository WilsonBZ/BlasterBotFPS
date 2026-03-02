using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural muzzle flash effect — attach to the weapon's FiringPoint.
/// Call Play() each time the weapon fires.
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    [Header("Sphere Burst")]
    [SerializeField] private float burstMaxScale = 0.4f;
    [SerializeField] private float burstDuration = 0.08f;
    [SerializeField] private Color burstColor = new Color(1f, 0.9f, 0.4f, 0.9f);

    [Header("Light Flash")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private float lightIntensity = 6f;
    [SerializeField] private float lightRange = 4f;
    [SerializeField] private float lightDuration = 0.06f;

    [Header("Material")]
    [SerializeField] private Material baseMaterial;

    private Light flashLight;

    private void Awake()
    {
        if (useLight)
        {
            GameObject lightObj = new GameObject("MuzzleLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;

            flashLight = lightObj.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = lightColor;
            flashLight.intensity = 0f;
            flashLight.range = lightRange;
            flashLight.shadows = LightShadows.None;
        }
    }

    /// <summary>
    /// Triggers the muzzle flash. Call this every time the weapon fires.
    /// </summary>
    public void Play()
    {
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        GameObject sphere = CreateBurstSphere(out Material mat);
        float elapsed = 0f;

        if (flashLight != null)
            flashLight.intensity = lightIntensity;

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;

            if (sphere != null && mat != null)
            {
                float scale = Mathf.Lerp(0.05f, burstMaxScale, t);
                sphere.transform.localScale = Vector3.one * scale;

                Color c = mat.GetColor("_BaseColor");
                c.a = Mathf.Lerp(0.9f, 0f, t);
                mat.SetColor("_BaseColor", c);
            }

            if (flashLight != null)
            {
                float lightT = elapsed / lightDuration;
                flashLight.intensity = Mathf.Lerp(lightIntensity, 0f, lightT);
            }

            yield return null;
        }

        if (flashLight != null)
            flashLight.intensity = 0f;

        if (mat != null) Destroy(mat);
        if (sphere != null) Destroy(sphere);
    }

    private GameObject CreateBurstSphere(out Material mat)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * 0.05f;

        Destroy(sphere.GetComponent<Collider>());

        mat = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        mat.SetColor("_BaseColor", burstColor);

        sphere.GetComponent<Renderer>().material = mat;

        return sphere;
    }
}
