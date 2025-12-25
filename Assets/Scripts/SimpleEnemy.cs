using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimpleEnemy : MonoBehaviour
{
    [Header("Player & References")]
    public Transform player;

    [Header("Movement")]
    public float chaseSpeed = 3.5f;
    public float stoppingDistance = 2f;

    [Header("Attack")]
    public float attackRange = 3f;
    public float attackConeAngle = 45f;
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    public float attackDelay = 0.4f;
    public float raycastHeight = 1.2f;

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Debug")]
    public bool debugRays = false;

    private NavMeshAgent agent;
    public Animator animator;
    private int currentHealth;
    private float lastAttackTime;
    private float attackDelayTimer = 0f;
    private bool isDelayingAttack = false;
    private int Punch_Left_Right_index = 0;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = chaseSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = 360f;
        agent.acceleration = 8f;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (player == null || currentHealth <= 0) return;

        agent.SetDestination(player.position);
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float speed = agent.velocity.magnitude / agent.speed;
        animator.SetFloat("Speed", speed);

        if (debugRays && distanceToPlayer <= attackRange)
        {
            DrawDebugCone();
        }

        if (distanceToPlayer <= attackRange)
        {
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle <= attackConeAngle)
            {
                TryInitiateAttack();
            }
        }

        if (isDelayingAttack)
        {
            attackDelayTimer -= Time.deltaTime;
            if (attackDelayTimer <= 0f)
            {
                PerformConeAttack();
                isDelayingAttack = false;
            }
        }
    }

    private void TryInitiateAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (isDelayingAttack) return;
        animator.SetTrigger("Attack");
        attackDelayTimer = attackDelay;
        isDelayingAttack = true;
    }

    private void PerformConeAttack()
    {
        // Toggle punch index
        animator.SetInteger("PunchIndex", Punch_Left_Right_index);
        Punch_Left_Right_index = 1 - Punch_Left_Right_index; // Toggle 0 <-> 1

        Vector3 origin = transform.position + Vector3.up * raycastHeight;
        int rayCount = 9;
        float currentAngle = -attackConeAngle;
        float angleStep = (attackConeAngle * 2f) / (rayCount - 1);
        bool hitPlayer = false;

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 rayDir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            rayDir.Normalize();
            if (Physics.Raycast(origin, rayDir, out RaycastHit hit, attackRange))
            {
                Color rayColor = hit.transform.CompareTag("Player") ? Color.red : Color.blue;
                if (debugRays)
                    Debug.DrawRay(origin, rayDir * hit.distance, rayColor, 1f);
                if (hit.transform.CompareTag("Player"))
                {
                    hitPlayer = true;
                    break;
                }
            }
            else if (debugRays)
            {
                Debug.DrawRay(origin, rayDir * attackRange, Color.gray, 0.5f);
            }
            currentAngle += angleStep;
        }

        if (hitPlayer)
        {
            SimpleFPSController playerController = player.GetComponent<SimpleFPSController>();
            if (playerController != null)
            {
                playerController.TakeDamage(attackDamage, Punch_Left_Right_index);
                Debug.Log($"Enemy hit player! Player health: {playerController.health}");
            }
            lastAttackTime = Time.time;
        }
    }

    private void DrawDebugCone()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeight;
        int rayCount = 9;
        float currentAngle = -attackConeAngle;
        float angleStep = (attackConeAngle * 2f) / (rayCount - 1);
        for (int i = 0; i < rayCount; i++)
        {
            Vector3 rayDir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            rayDir.Normalize();
            Debug.DrawRay(origin, rayDir * attackRange, Color.yellow);
            currentAngle += angleStep;
        }
    }

    public void TakeDamage(int damage)
    {
        animator.SetTrigger("Hit");
        currentHealth -= damage;
        Debug.Log($"Enemy took {damage} damage. Health: {currentHealth}");
        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        Destroy(gameObject, 2f);
    }
}