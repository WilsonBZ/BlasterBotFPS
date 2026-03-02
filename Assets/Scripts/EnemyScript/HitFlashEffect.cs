using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitFlashEffect : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;
    
    private List<Material> materialInstances = new List<Material>();
    private List<Color> originalBaseColors = new List<Color>();
    private List<Color> originalEmissionColors = new List<Color>();
    private Renderer[] renderers;
    private Coroutine flashCoroutine;
    
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    
    private void Awake()
    {
        CacheMaterials();
    }
    
    private void CacheMaterials()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            
            foreach (Material mat in materials)
            {
                materialInstances.Add(mat);
                
                if (mat.HasProperty(BaseColorProperty))
                {
                    originalBaseColors.Add(mat.GetColor(BaseColorProperty));
                }
                else
                {
                    originalBaseColors.Add(Color.white);
                }
                
                if (mat.HasProperty(EmissionColorProperty))
                {
                    originalEmissionColors.Add(mat.GetColor(EmissionColorProperty));
                }
                else
                {
                    originalEmissionColors.Add(Color.black);
                }
            }
        }
        
        Debug.Log($"HitFlashEffect cached {materialInstances.Count} materials from {renderers.Length} renderers");
    }
    
    public void Flash()
    {
        if (materialInstances.Count == 0)
        {
            Debug.LogWarning("HitFlashEffect has no materials cached!");
            return;
        }
        
        Debug.Log($"HitFlashEffect.Flash() called on {gameObject.name}");
        
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }
    
    private IEnumerator FlashCoroutine()
    {
        SetFlashColor();
        
        yield return new WaitForSeconds(flashDuration);
        
        ResetColors();
        flashCoroutine = null;
    }
    
    private void SetFlashColor()
    {
        for (int i = 0; i < materialInstances.Count; i++)
        {
            if (materialInstances[i] == null) continue;
            
            materialInstances[i].SetColor(BaseColorProperty, flashColor);
            materialInstances[i].SetColor(EmissionColorProperty, flashColor * 2f);
        }
    }
    
    private void ResetColors()
    {
        for (int i = 0; i < materialInstances.Count; i++)
        {
            if (materialInstances[i] == null) continue;
            
            materialInstances[i].SetColor(BaseColorProperty, originalBaseColors[i]);
            materialInstances[i].SetColor(EmissionColorProperty, originalEmissionColors[i]);
        }
    }
    
    private void OnDestroy()
    {
        ResetColors();
    }
}
