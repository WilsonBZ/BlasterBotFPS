using UnityEngine;

public class HitImpactEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject hitParticles;
    [SerializeField] private float particleLifetime = 1f;
    
    [Header("Light Flash")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.5f, 0.2f);
    [SerializeField] private float lightIntensity = 3f;
    [SerializeField] private float lightRange = 5f;
    [SerializeField] private float lightDuration = 0.15f;
    
    [Header("Ring Burst")]
    [SerializeField] private bool useRingBurst = true;
    [SerializeField] private float ringMaxScale = 2f;
    [SerializeField] private float ringSpeed = 10f;
    [SerializeField] private Color ringColor = new Color(1f, 0.7f, 0.3f, 0.6f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] hitSounds;
    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 0.5f;
    
    private void Start()
    {
        Vector3 hitPosition = transform.position;
        Quaternion hitRotation = transform.rotation;
        
        if (hitParticles != null)
        {
            GameObject particles = Instantiate(hitParticles, hitPosition, hitRotation);
            Destroy(particles, particleLifetime);
        }
        
        if (useLight)
        {
            CreateLightFlash(hitPosition);
        }
        
        if (useRingBurst)
        {
            CreateRingBurst(hitPosition, hitRotation);
        }
        
        if (hitSounds != null && hitSounds.Length > 0)
        {
            AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
            AudioSource.PlayClipAtPoint(clip, hitPosition, hitVolume);
        }
        
        Destroy(gameObject, Mathf.Max(particleLifetime, lightDuration, 1f));
    }
    
    private void CreateLightFlash(Vector3 position)
    {
        GameObject lightObj = new GameObject("HitLight");
        lightObj.transform.position = position;
        
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = lightIntensity;
        light.range = lightRange;
        light.shadows = LightShadows.None;
        
        HitLightFlasher flasher = lightObj.AddComponent<HitLightFlasher>();
        flasher.duration = lightDuration;
        flasher.startIntensity = lightIntensity;
    }
    
    private void CreateRingBurst(Vector3 position, Quaternion rotation)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ring.transform.position = position;
        ring.transform.rotation = rotation;
        ring.transform.localScale = Vector3.one * 0.1f;
        
        Destroy(ring.GetComponent<Collider>());
        
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", ringColor);
        mat.SetFloat("_Surface", 1);
        mat.renderQueue = 3000;
        
        Renderer renderer = ring.GetComponent<Renderer>();
        renderer.material = mat;
        
        HitRingExpander expander = ring.AddComponent<HitRingExpander>();
        expander.maxScale = ringMaxScale;
        expander.expandSpeed = ringSpeed;
        expander.duration = 0.3f;
    }
}

public class HitLightFlasher : MonoBehaviour
{
    public float duration = 0.15f;
    public float startIntensity = 3f;
    
    private Light lightComponent;
    private float elapsed = 0f;
    
    private void Start()
    {
        lightComponent = GetComponent<Light>();
    }
    
    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;
        
        lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, progress);
        
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public class HitRingExpander : MonoBehaviour
{
    public float maxScale = 2f;
    public float expandSpeed = 10f;
    public float duration = 0.3f;
    
    private float elapsed = 0f;
    private Material material;
    
    private void Start()
    {
        material = GetComponent<Renderer>().material;
    }
    
    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;
        
        float scale = Mathf.Lerp(0.1f, maxScale, progress);
        transform.localScale = Vector3.one * scale;
        
        Color color = material.GetColor("_BaseColor");
        color.a = 1f - progress;
        material.SetColor("_BaseColor", color);
        
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
