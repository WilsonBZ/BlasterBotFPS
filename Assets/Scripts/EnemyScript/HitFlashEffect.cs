using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitFlashEffect : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Material Settings")]
    [SerializeField] private string emissionProperty = "_EmissionColor";
    [SerializeField] private bool useEmission = true;
    
    private List<Material> materials = new List<Material>();
    private List<Color> originalColors = new List<Color>();
    private List<Color> originalEmissionColors = new List<Color>();
    private Coroutine flashCoroutine;
    
    private void Awake()
    {
        CacheMaterials();
    }
    
    private void CacheMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                materials.Add(mat);
                
                if (mat.HasProperty("_Color"))
                {
                    originalColors.Add(mat.color);
                }
                else
                {
                    originalColors.Add(Color.white);
                }
                
                if (useEmission && mat.HasProperty(emissionProperty))
                {
                    originalEmissionColors.Add(mat.GetColor(emissionProperty));
                }
                else
                {
                    originalEmissionColors.Add(Color.black);
                }
            }
        }
    }
    
    public void Flash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }
    
    private IEnumerator FlashCoroutine()
    {
        SetFlashAmount(1f);
        
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / flashDuration;
            float flashAmount = flashCurve.Evaluate(normalizedTime);
            
            SetFlashAmount(flashAmount);
            
            yield return null;
        }
        
        SetFlashAmount(0f);
        flashCoroutine = null;
    }
    
    private void SetFlashAmount(float amount)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i] == null) continue;
            
            Color targetColor = Color.Lerp(originalColors[i], flashColor, amount);
            materials[i].color = targetColor;
            
            if (useEmission && materials[i].HasProperty(emissionProperty))
            {
                Color emissionColor = Color.Lerp(originalEmissionColors[i], flashColor * 2f, amount);
                materials[i].SetColor(emissionProperty, emissionColor);
            }
        }
    }
    
    private void OnDestroy()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        
        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i] != null)
            {
                materials[i].color = originalColors[i];
                
                if (useEmission && materials[i].HasProperty(emissionProperty))
                {
                    materials[i].SetColor(emissionProperty, originalEmissionColors[i]);
                }
            }
        }
    }
}
