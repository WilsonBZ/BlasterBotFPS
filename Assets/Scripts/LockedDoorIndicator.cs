using UnityEngine;

public class LockedDoorIndicator : MonoBehaviour
{
    [Header("References")]
    public SlidingDoubleDoor door;
    
    [Header("Visual Feedback")]
    public Renderer[] doorRenderers;
    public Color lockedColor = Color.red;
    public Color unlockedColor = Color.green;
    
    [Header("Emissive Settings")]
    public bool useEmissive = true;
    public float emissiveIntensity = 2f;

    private MaterialPropertyBlock propertyBlock;
    private bool wasLocked;

    private void Awake()
    {
        if (door == null)
        {
            door = GetComponentInParent<SlidingDoubleDoor>();
        }

        if (doorRenderers == null || doorRenderers.Length == 0)
        {
            doorRenderers = GetComponentsInChildren<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        wasLocked = door != null && door.isLocked;
        UpdateVisuals();
    }

    private void Update()
    {
        if (door == null) return;

        if (door.isLocked != wasLocked)
        {
            wasLocked = door.isLocked;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        Color targetColor = door.isLocked ? lockedColor : unlockedColor;
        
        foreach (var renderer in doorRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", targetColor);
            propertyBlock.SetColor("_Color", targetColor);
            
            if (useEmissive)
            {
                propertyBlock.SetColor("_EmissionColor", targetColor * emissiveIntensity);
            }
            
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
