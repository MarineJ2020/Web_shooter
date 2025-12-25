using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
//[RequireComponent(typeof(Animator))]
public class SimpleEnemy : MonoBehaviour
{
    [Header("Player & References")]
    public Transform player; // Assign or auto-find by "Player" tag

    [Header("Movement")]
    public float chaseSpeed = 3.5f;
    public float stoppingDistance = 2f;

    [Header("Attack")]
    public float attackRange = 3f;
    public float attackConeAngle = 45f;        // Half-angle (total cone = 90бу)
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;        // Minimum time between successful attacks
    [Tooltip("Delay after triggering the attack animation before the cone raycast actually fires. Use this to sync damage with the animation swipe.")]
    public float attackDelay = 0.4f;            // NEW: Adjustable animation compensation delay
    [Tooltip("Height offset from enemy position where the cone rays originate (e.g., chest level).")]
    public float raycastHeight = 1.2f;          // NEW: Adjustable raycast origin height

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Debug")]
    public bool debugRays = false;

    private NavMeshAgent agent;
    public Animator animator;
    private int currentHealth;
    private float lastAttackTime;
    private float attackDelayTimer = 0f;       // Tracks the delay after triggering animation
    private bool isDelayingAttack = false;     // Flag to know if we're in the delay phase

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
       // animator = GetComponent<Animator>();

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

        // Always chase
        agent.SetDestination(player.position);

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float speed = agent.velocity.magnitude / agent.speed;
        animator.SetFloat("Speed", speed);

        // Debug cone preview
        if (debugRays && distanceToPlayer <= attackRange)
        {
            DrawDebugCone();
        }

        // Start attack check
        if (distanceToPlayer <= attackRange)
        {
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);

            if (angle <= attackConeAngle)
            {
                TryInitiateAttack();
            }
        }

        // Handle the delay timer (counts down when in delay phase)
        if (isDelayingAttack)
        {
            attackDelayTimer -= Time.deltaTime;
            if (attackDelayTimer <= 0f)
            {
                PerformConeAttack();           // Actually fire the rays after delay
                isDelayingAttack = false;
            }
        }
    }

    private int Punch_Left_Right_index=0;

    private void TryInitiateAttack()
    {



        // Respect cooldown (based on last SUCCESSFUL attack)
        if (Time.time < lastAttackTime + attackCooldown) return;

        // Prevent starting a new delay if already in one
        if (isDelayingAttack) return;

        // Trigger animation immediately
        animator.SetTrigger("Attack");

        // Start delay before actual damage check
        attackDelayTimer = attackDelay;
        isDelayingAttack = true;
    }

    private void PerformConeAttack()
    {
        // Switch left right hand punch
        if (Punch_Left_Right_index == 0)
        {
            animator.SetInteger("PunchIndex", Punch_Left_Right_index);
            Punch_Left_Right_index = 1;
        }
        else
        {
            animator.SetInteger("PunchIndex", Punch_Left_Right_index);
            Punch_Left_Right_index = 0;
        }
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
                playerController.health -= attackDamage;
                Debug.Log($"Enemy hit player! Player health: {playerController.health}");
            }

            lastAttackTime = Time.time;   // Cooldown starts only on successful hit
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