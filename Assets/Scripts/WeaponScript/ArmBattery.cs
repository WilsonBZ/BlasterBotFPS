using UnityEngine;

public class ArmBattery : MonoBehaviour
{
    [Header("Battery")]
    public float maxCharge = 100f;
    [Tooltip("Initial charge")]
    public float initialCharge = 100f;
    public float rechargeRate = 8f; 
    public float rechargeDelay = 1.5f; 

    float currentCharge;
    float lastConsumeTime = -999f;

    void Awake()
    {
        currentCharge = Mathf.Clamp(initialCharge, 0f, maxCharge);
    }

    void Update()
    {
        if (Time.time > lastConsumeTime + rechargeDelay)
        {
            if (currentCharge < maxCharge)
                currentCharge = Mathf.Min(maxCharge, currentCharge + rechargeRate * Time.deltaTime);
        }
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f) return true;
        if (currentCharge >= amount)
        {
            currentCharge -= amount;
            lastConsumeTime = Time.time;
            return true;
        }
        return false;
    }

    public float GetBatteryPercent()
    {
        return currentCharge / maxCharge;
    }


    public float GetCurrent() => currentCharge;
    public float GetMax() => maxCharge;
    public float GetPercent() => (maxCharge <= 0f) ? 0f : (currentCharge / maxCharge);

    public void Refill() { currentCharge = maxCharge; }
}


