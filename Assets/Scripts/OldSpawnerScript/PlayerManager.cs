using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float invulnerabilityTime = 0.5f;
    [SerializeField] private GameObject deathEffect;
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
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || Time.time < lastDamageTime + invulnerabilityTime) return;

        currentHealth -= amount;
        lastDamageTime = Time.time;

        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        AudioSource.PlayClipAtPoint(damageSound, transform.position);

        StartCoroutine(DamageFlash());

        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }

    }

    private IEnumerator DamageFlash()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer)
        {
            Color original = renderer.material.color;
            renderer.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            renderer.material.color = original;
        }
    }

    private void Die()
    {
        isDead = true;

        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<MouseMovement>().enabled = false;

        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);

        OnDeath?.Invoke();

        Debug.Log("Player Died");
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
    }
}
