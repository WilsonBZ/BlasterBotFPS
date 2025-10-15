using UnityEngine;
using UnityEngine.UI;

public class BatteryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArmBattery battery;
    [SerializeField] private Image fillImage;

    private void Start()
    {
        if (battery == null)
            battery = FindAnyObjectByType<ArmBattery>();

        if (fillImage == null)
        {
            Debug.LogError("BatteryUI: Fill image not assigned!");
            enabled = false;
        }
    }

    private void Update()
    {
        if (battery == null || fillImage == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(battery.GetBatteryPercent());
    }
}
