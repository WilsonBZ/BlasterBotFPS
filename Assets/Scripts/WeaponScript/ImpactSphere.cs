using UnityEngine;

public class ImpactSphere : MonoBehaviour
{
    [Header("Sphere Settings")]
    [SerializeField] private float maxScale = 1.5f;
    [SerializeField] private float expandSpeed = 8f;
    [SerializeField] private Color sphereColor = new Color(1f, 0.8f, 0.3f, 0.8f);
    [SerializeField] private float lifetime = 0.3f;
    
    private GameObject sphere;
    private Material material;
    private float elapsed = 0f;
    
    private void Start()
    {
        CreateSphere();
    }
    
    private void CreateSphere()
    {
        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = transform.position;
        sphere.transform.localScale = Vector3.one * 0.1f;
        
        Destroy(sphere.GetComponent<Collider>());
        
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", sphereColor);
        material.SetFloat("_Surface", 1);
        material.renderQueue = 3000;
        
        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.material = material;
        
        Destroy(gameObject, lifetime);
    }
    
    private void Update()
    {
        if (sphere == null) return;
        
        elapsed += Time.deltaTime;
        float progress = elapsed / lifetime;
        
        float scale = Mathf.Lerp(0.1f, maxScale, progress);
        sphere.transform.localScale = Vector3.one * scale;
        
        Color color = material.GetColor("_BaseColor");
        color.a = Mathf.Lerp(0.8f, 0f, progress);
        material.SetColor("_BaseColor", color);
    }
    
    private void OnDestroy()
    {
        if (sphere != null)
        {
            Destroy(sphere);
        }
        
        if (material != null)
        {
            Destroy(material);
        }
    }
}
