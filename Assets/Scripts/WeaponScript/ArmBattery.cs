using UnityEngine;


public class ArmBattery : MonoBehaviour
{
    [SerializeField] private float maxCharge = 100f;
    [SerializeField] private float currentCharge = 100f;
    [SerializeField] private float rechargeRate = 10f;
    [SerializeField] private float rechargeDelay = 1.5f;


    private float lastConsumeTime = -999f;


    private void Awake() { currentCharge = Mathf.Clamp(currentCharge, 0f, maxCharge); }


    private void Update()
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
        if (currentCharge >= amount) { currentCharge -= amount; lastConsumeTime = Time.time; return true; }
        return false;
    }


    public float GetCurrentCharge() => currentCharge;
    public float GetMaxCharge() => maxCharge;
    public float GetChargePercent() => (maxCharge <= 0f) ? 0f : (currentCharge / maxCharge);
    public void Refill() => currentCharge = maxCharge;
}