using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;


[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Enemy : BaseEnemy, IDamageable
{
    [Header("Core Settings")]
    [SerializeField] private EnemyConfig config;
    //[SerializeField] private EnemyState initialState = EnemyState.Idle;

    [Header("Health Settings")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2f, 0);

    [Header("Combat")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask playerLayer;
    [HideInInspector] public WaveSpawner spawner;

    [Header("Visuals")]
    [SerializeField] private Renderer[] materialRenderers;
    [SerializeField] private GameObject deathEffect;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 numberOffset = new Vector3(0, 1.5f, 0);

    [Header("Explosion Settings")]
    [SerializeField] private float explosionRange = 2f;
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float selfDestructDelay = 0.5f;

    //private TextMeshProUGUI damageNumberGUI;
    private Slider healthSlider;
    private GameObject healthBarInstance;
    private float currentHealth;

    //public event Action OnDeath;
    private NavMeshAgent agent;
    //private Animator animator;
    private PlayerManager player;

    private EnemyState currentState;
    private float lastAttackTime;
    private bool isExploded;

    private readonly int animMoveSpeed = Animator.StringToHash("MoveSpeed");
    private readonly int animAttack = Animator.StringToHash("Attack");
    private readonly int animTakeDamage = Animator.StringToHash("TakeDamage");
    private readonly int animDie = Animator.StringToHash("Die");



    private void Awake()
    {
        //damageNumberGUI = damageNumberPrefab.GetComponent<TextMeshProUGUI>();
        agent = GetComponent<NavMeshAgent>();
        //animator = GetComponent<Animator>();
        player = FindFirstObjectByType<PlayerManager>();

        InitializeFromConfig();
    }
    private void Start()
    {
        currentHealth = config.maxHealth;
        CreateHealthBar();
    }

    private void Update()
    {
        if (isExploded) return;

        if (config.canExplode && Vector3.Distance(transform.position, player.transform.position) <= config.explosionRange)
        {
            StartCoroutine(Explode());
            return;
        }

        UpdateStateMachine();
        //UpdateAnimations();
    }

    private void CreateHealthBar()
    {
        if (healthBarPrefab == null) return;

        healthBarInstance = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity);
        healthBarInstance.transform.SetParent(transform); 
        healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / config.maxHealth;
        }
    }

    private void InitializeFromConfig()
    {
        currentHealth = config.maxHealth;
        agent.speed = config.moveSpeed;
        agent.acceleration = config.acceleration;

        agent.stoppingDistance = config.attackRange * 0.9f;

    }

    private void UpdateStateMachine()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState(distanceToPlayer);
                break;
            case EnemyState.Chase:
                HandleChaseState(distanceToPlayer);
                break;
            case EnemyState.Attack:
                HandleAttackState(distanceToPlayer);
                break;
            case EnemyState.Dead:
                HandleDeadState();
                break;
        }

        if (currentState != EnemyState.Dead)
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
            }
        }
    }

    /*private void UpdateAnimations()
    {
        animator.SetFloat(animMoveSpeed, agent.velocity.magnitude / agent.speed);
    }*/

    private void HandleIdleState(float distanceToPlayer)
    {
        if (distanceToPlayer <= config.detectionRange)
        {
            TransitionToState(EnemyState.Chase);
        }
    }

    private void HandleChaseState(float distanceToPlayer)
    {
        if (distanceToPlayer <= config.attackRange)
        {
            agent.ResetPath(); 
            TransitionToState(EnemyState.Attack);
            return;
        }

        agent.SetDestination(player.transform.position);
    }

    private void HandleAttackState(float distanceToPlayer)
    {
        if (distanceToPlayer > config.attackRange * 1.1f)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        if (Time.time >= lastAttackTime + config.attackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }
    }

    private void HandleDeadState()
    {

    }

    private void TransitionToState(EnemyState newState)
    {
        currentState = newState;

        if (newState == EnemyState.Attack)
        {
            agent.isStopped = true;
        }
        else if (currentState == EnemyState.Chase)
        {
            agent.isStopped = false;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isExploded) return;

        currentHealth -= damage;
        //animator.SetTrigger(animTakeDamage);
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (config.fleeOnHit)
            {
                StartCoroutine(FleeBehavior());
            }
        }

        ShowDamageNumber(damage);
    }


    private void Attack()
    {
        //animator.SetTrigger(animAttack);

    }

    public void OnAttackFrame()
    {
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, config.attackRadius, playerLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(config.attackDamage);
            }
        }
    }

    private void Die()
    {
        isExploded = true;

        if (healthBarInstance) healthBarInstance.SetActive(false);
        if (agent) agent.enabled = false;

        //animator.SetTrigger(animDie);
        agent.enabled = false;

        foreach (var collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }

        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);
        HandleDeath();
        Destroy(gameObject, config.deathCleanupTime);
        Debug.Log(message: "die");
    }

    private IEnumerator Explode()
    {
        isExploded = true;
        agent.isStopped = true;
        //animator.SetTrigger(animDie);

        yield return new WaitForSeconds(selfDestructDelay);

        if (explosionEffect)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }


        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRange);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float damageMultiplier = 1 - Mathf.Clamp01(distance / explosionRange);
                damageable.TakeDamage(explosionDamage * damageMultiplier);

                Rigidbody rb = hit.GetComponent<Rigidbody>();
                if (rb)
                {
                    Vector3 direction = (hit.transform.position - transform.position).normalized;
                    rb.AddForce(direction * explosionForce, ForceMode.Impulse);
                }
            }
        }
        HandleDeath();
        Die();
    }

    private IEnumerator FleeBehavior()
    {
        Vector3 fleeDirection = (transform.position - player.transform.position).normalized;
        Vector3 fleePosition = transform.position + fleeDirection * config.fleeDistance;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleePosition, out hit, config.fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            yield return new WaitForSeconds(config.fleeDuration);

            if (!isExploded) TransitionToState(EnemyState.Chase);
        }


    }

    private void ShowDamageNumber(float damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + numberOffset + UnityEngine.Random.insideUnitSphere * 0.3f;
        GameObject number = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);

        number.transform.LookAt(Camera.main.transform);
        number.transform.Rotate(0, 180, 0);

        DamageNumber dn = number.GetComponent<DamageNumber>();
        if (dn) dn.Initialize(damage);
    }

    private void LateUpdate()
    {
        if (healthBarInstance)
        {
            healthBarInstance.transform.position = transform.position + healthBarOffset;
            healthBarInstance.transform.rotation = Camera.main.transform.rotation;
        }
    }

    //private void OnDrawGizmosSelected()
    //{
    //    // Visualization
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(transform.position, config.detectionRange);

    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(transform.position, config.chaseRange);

    //    if (attackPoint)
    //    {
    //        Gizmos.color = Color.magenta;
    //        Gizmos.DrawWireSphere(attackPoint.position, config.attackRadius);
    //    }
    //}
}

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Ranged Combat")]
    public bool isRanged = true;
    public float preferredDistance = 10f;
    public float fireCooldown = 1.5f;
    public GameObject projectilePrefab;


    [Header("Explosion Settings")]
    public bool canExplode = true;
    public float explosionRange = 2f;
    public float explosionDamage = 50f;
    public float explosionForce = 10f;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float acceleration = 8f;
    public float detectionRange = 15f;
    public float chaseRange = 20f;
    public float fleeDistance = 10f;
    public float fleeDuration = 3f;
    public bool fleeOnHit = true;

    [Header("Combat")]
    public float attackDamage = 15f;
    public float attackRange = 2f;
    public float attackRadius = 1.5f;
    public float attackCooldown = 2f;

    [Header("Death")]
    public float deathCleanupTime = 3f;
}