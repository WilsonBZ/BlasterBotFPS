using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerManager healthComponent;
    [SerializeField] private UnityEngine.UI.Image fillImage;

    private void Start()
    {
        if (healthComponent == null)
            healthComponent = FindAnyObjectByType<PlayerManager>();
        if (fillImage == null)
        {
            Debug.LogError("HealthBarUI: Fill image not assigned!");
            enabled = false;
        }
    }

    private void Update()
    {
        if (healthComponent == null || fillImage == null)
            return;
        fillImage.fillAmount = Mathf.Clamp01(healthComponent.GetHealthPercent());
    }

}
