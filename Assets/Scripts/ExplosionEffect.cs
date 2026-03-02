using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    [Header("Core Particles")]
    [SerializeField] private GameObject coreExplosion;
    [SerializeField] private GameObject flashEffect;
    [SerializeField] private GameObject smokeEffect;

    [Header("Shockwave Ring")]
    [SerializeField] private bool spawnShockwave = true;
    [SerializeField] private float shockwaveMaxScale = 10f;
    [SerializeField] private float shockwaveSpeed = 15f;
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.5f, 0f, 0.8f);
    [SerializeField] private Material shockwaveMaterial;

    [Header("Light Flash")]
    [SerializeField] private bool spawnLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private float lightIntensity = 10f;
    [SerializeField] private float lightRange = 15f;
    [SerializeField] private float lightDuration = 0.3f;

    [Header("Debris")]
    [SerializeField] private bool spawnDebris = true;
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private int debrisCount = 8;
    [SerializeField] private float debrisForce = 10f;
    [SerializeField] private float debrisLifetime = 3f;

    [Header("Timing")]
    [SerializeField] private float totalLifetime = 3f;

    private void Start()
    {
        SpawnExplosion();
        Destroy(gameObject, totalLifetime);
    }

    private void SpawnExplosion()
    {
        Vector3 position = transform.position;

        if (coreExplosion != null) Instantiate(coreExplosion, position, Quaternion.identity);
        if (flashEffect != null)   Instantiate(flashEffect,   position, Quaternion.identity);
        if (smokeEffect != null)   Instantiate(smokeEffect,   position, Quaternion.identity);

        if (spawnShockwave) CreateShockwave(position);
        if (spawnLight)     CreateLightFlash(position);
        if (spawnDebris && debrisPrefab != null) CreateDebris(position);
    }

    private void CreateShockwave(Vector3 position)
    {
        GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shockwave.transform.position = position;
        shockwave.transform.localScale = Vector3.one * 0.1f;

        Destroy(shockwave.GetComponent<Collider>());

        Material mat = shockwaveMaterial != null
            ? new Material(shockwaveMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        mat.SetColor("_BaseColor", shockwaveColor);

        shockwave.GetComponent<Renderer>().material = mat;

        ShockwaveExpander expander = shockwave.AddComponent<ShockwaveExpander>();
        expander.maxScale    = shockwaveMaxScale;
        expander.expandSpeed = shockwaveSpeed;
        expander.duration    = 0.5f;
    }

    private void CreateLightFlash(Vector3 position)
    {
        GameObject lightObj = new GameObject("ExplosionLight");
        lightObj.transform.position = position;

        Light light = lightObj.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = lightColor;
        light.intensity = lightIntensity;
        light.range     = lightRange;
        light.shadows   = LightShadows.None;

        LightFlasher flasher = lightObj.AddComponent<LightFlasher>();
        flasher.duration = lightDuration;
    }

    private void CreateDebris(Vector3 position)
    {
        for (int i = 0; i < debrisCount; i++)
        {
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);

            GameObject debris = Instantiate(debrisPrefab, position, Random.rotation);

            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb == null) rb = debris.AddComponent<Rigidbody>();

            rb.AddForce(randomDir * debrisForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);

            Destroy(debris, debrisLifetime);
        }
    }
}

public class ShockwaveExpander : MonoBehaviour
{
    public float maxScale    = 10f;
    public float expandSpeed = 15f;
    public float duration    = 0.5f;

    private float    elapsed;
    private Material material;

    private void Start()
    {
        material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;

        transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxScale, progress);

        Color c = material.GetColor("_BaseColor");
        c.a = 1f - progress;
        material.SetColor("_BaseColor", c);

        if (progress >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}

public class LightFlasher : MonoBehaviour
{
    public float duration = 0.3f;

    private Light lightComponent;
    private float startIntensity;
    private float elapsed;

    private void Start()
    {
        lightComponent = GetComponent<Light>();
        startIntensity = lightComponent.intensity;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;

        lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, progress);

        if (progress >= 1f) Destroy(gameObject);
    }
}

