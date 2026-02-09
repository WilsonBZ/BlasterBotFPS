using UnityEngine;

public class PlayerManager : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float invulnerabilityTime = 0.5f;
    [SerializeField] private AudioClip damageSound;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private float currentHealth;
    private float lastDamageTime;
    private bool isDead;

    public event System.Action OnDeath;
    public event System.Action<float> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(GetHealthPercent());
    }

    public void TakeDamage(float amount)
    {
        if (isDead || Time.time < lastDamageTime + invulnerabilityTime)
        {
            return;
        }

        currentHealth -= amount;
        lastDamageTime = Time.time;

        OnHealthChanged?.Invoke(GetHealthPercent());

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public float GetHealthPercent()
    {
        if (maxHealth <= 0f)
        {
            return 0f;
        }

        return currentHealth / maxHealth;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        MouseMovement mouse = GetComponentInChildren<MouseMovement>();
        if (mouse != null)
        {
            mouse.enabled = false;
        }

        OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (isDead)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(GetHealthPercent());
    }
    
    public void IncreaseMaxHealth(float amount)
    {
        if (isDead)
        {
            return;
        }
        
        maxHealth += amount;
        OnHealthChanged?.Invoke(GetHealthPercent());
    }
}
