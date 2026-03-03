using UnityEngine;

public class ImpactSphere : MonoBehaviour
{
    [Header("Sphere Settings")]
    [SerializeField] private float maxScale = 1.5f;
    [SerializeField] private Color sphereColor = new Color(1f, 0.8f, 0.3f, 0.8f);
    [SerializeField] private float lifetime = 0.3f;

    [Header("Material")]
    [SerializeField] private Material baseMaterial;

    private GameObject sphere;
    private Material material;
    private float elapsed = 0f;
    
    private void Start()
    {
        CreateSphere();
        Destroy(gameObject, lifetime);
    }
    
    private void CreateSphere()
    {
        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        // Child of self — destroyed automatically when this GameObject is destroyed
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale    = Vector3.one * 0.1f;
        
        Destroy(sphere.GetComponent<Collider>());

        if (baseMaterial == null)
        {
            Debug.LogError("ImpactSphere: baseMaterial not assigned. Assign VFX_ImpactSphere.mat in the prefab.");
            return;
        }

        material = new Material(baseMaterial);
        material.SetColor("_BaseColor", sphereColor);
        sphere.GetComponent<Renderer>().material = material;
    }
    
    private void Update()
    {
        if (sphere == null || material == null) return;
        
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        
        sphere.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxScale, t);
        
        Color c = material.GetColor("_BaseColor");
        c.a = Mathf.Lerp(0.8f, 0f, t);
        material.SetColor("_BaseColor", c);
    }
    
    private void OnDestroy()
    {
        // sphere is a child — Unity destroys it automatically with this GameObject
        if (material != null)
            Destroy(material);
    }
}
