using UnityEngine;

public class MuzzleFlashEffect : MonoBehaviour
{
    [Header("Light Flash")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private float lightIntensity = 5f;
    [SerializeField] private float lightRange = 3f;
    [SerializeField] private float lightDuration = 0.05f;
    
    [Header("Scale Animation")]
    [SerializeField] private bool animateScale = true;
    [SerializeField] private float scaleMultiplier = 1.5f;
    [SerializeField] private float scaleDuration = 0.1f;
    
    private Light flashLight;
    private ParticleSystem particles;
    private Vector3 originalScale;
    private float lightTimer;
    private float scaleTimer;
    private bool isFlashing;
    
    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        originalScale = transform.localScale;
        
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
    
    public void PlayFlash()
    {
        if (particles != null)
        {
            particles.Play();
        }
        
        if (useLight && flashLight != null)
        {
            flashLight.intensity = lightIntensity;
            lightTimer = lightDuration;
            isFlashing = true;
        }
        
        if (animateScale)
        {
            transform.localScale = originalScale * scaleMultiplier;
            scaleTimer = scaleDuration;
        }
    }
    
    private void Update()
    {
        if (isFlashing && flashLight != null)
        {
            lightTimer -= Time.deltaTime;
            
            if (lightTimer <= 0f)
            {
                flashLight.intensity = 0f;
                isFlashing = false;
            }
            else
            {
                float t = lightTimer / lightDuration;
                flashLight.intensity = Mathf.Lerp(0f, lightIntensity, t);
            }
        }
        
        if (animateScale && scaleTimer > 0f)
        {
            scaleTimer -= Time.deltaTime;
            float t = scaleTimer / scaleDuration;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * scaleMultiplier, t);
        }
    }
}
