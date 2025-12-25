using UnityEngine;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float acceleration = 20f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Camera Settings")]
    public float mouseSensitivity = 2f;
    public float verticalLookLimit = 80f;

    [Header("Health")]
    public int health = 100;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 20f;
    public float staminaRegenRate = 10f;
    public float staminaRegenDelay = 1f;

    [Header("Player UI")]
    public TextMeshProUGUI HealthUI_text;
    public TextMeshProUGUI StaminaUI_text;

    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;
    private Vector3 moveVelocity;
    private float xRotation = 0f;
    private bool isCursorLocked = true;
    private bool isGrounded;
    private float currentStamina;
    private float staminaRegenTimer;
    private bool canSprint;

    [Header("Camera Shake Settings")]
    public float hitSwayMagnitude = 18f;        // Max degrees the camera sways left/right on hit
    public float hitSwayDuration = 0.4f;        // How long the sway lasts
    public float hitVerticalKick = 8f;          // Small upward/downward kick on impact
    public AnimationCurve swayCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Smooth start and end

    // Private shake variables
    private float swayTimer = 0f;
    private float currentSwayAmount = 0f;       // Current yaw offset (positive = right, negative = left)
    private float currentVerticalOffset = 0f;   // Pitch offset
    private int lastHitPunchIndex = -1;         // 0 = left punch, 1 = right punch



    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        if (playerCamera != null)
        {
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0, 0.5f, 0);
            playerCamera.transform.localRotation = Quaternion.identity;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentStamina = maxStamina;
        canSprint = true;
    }

    void Update()
    {
        HandleCursorLock();

        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -verticalLookLimit, verticalLookLimit);

        // Base rotation from mouse
        Quaternion baseRotation = Quaternion.Euler(xRotation, 0, 0);

        // Apply hit sway if active
        if (swayTimer > 0)
        {
            swayTimer -= Time.deltaTime;
            float progress = 1f - (swayTimer / hitSwayDuration); // 0 to 1 over duration

            // Evaluate curve for smooth motion
            float curveValue = swayCurve.Evaluate(progress);

            // Interpolate sway yaw from full magnitude back to 0
            float currentYaw = Mathf.Lerp(currentSwayAmount, 0f, curveValue);

            // Vertical kick: quick up, then down and back to 0
            float vertical = Mathf.Lerp(currentVerticalOffset, 0f, curveValue);
            if (progress > 0.5f) vertical = Mathf.Lerp(0f, -hitVerticalKick * 0.5f, (progress - 0.5f) * 2f); // Slight recoil down

            // Apply sway as additional rotation
            Quaternion swayRotation = Quaternion.Euler(xRotation + vertical, currentYaw, 0);
            playerCamera.transform.localRotation = swayRotation;
        }
        else
        {
            // Normal: no sway
            playerCamera.transform.localRotation = baseRotation;
        }

        transform.Rotate(Vector3.up * mouseX);

        // Ground check
        CheckGrounded();

        // Handle stamina
        HandleStamina();

        // Get raw input
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Build desired movement direction in LOCAL space
        Vector3 inputDirection = new Vector3(inputX, 0, inputZ);
        float inputMagnitude = inputDirection.magnitude;
        if (inputMagnitude > 0.01f)
            inputDirection /= inputMagnitude;

        Vector3 desiredMove = transform.right * inputDirection.x + transform.forward * inputDirection.z;

        // Determine target speed
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift) &&
                            currentStamina > 0 &&
                            canSprint &&
                            inputMagnitude > 0.5f;
        float targetSpeed = wantsToSprint ? runSpeed : walkSpeed;

        // Calculate target velocity
        Vector3 targetVelocity = desiredMove * targetSpeed * Mathf.Clamp01(inputMagnitude);

        // Smoothly accelerate
        moveVelocity = Vector3.Lerp(moveVelocity, targetVelocity, acceleration * Time.deltaTime);

        // Apply horizontal movement
        controller.Move(moveVelocity * Time.deltaTime);

        // Gravity and jump
        if (isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -0.1f;
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Apply vertical movement
        controller.Move(velocity * Time.deltaTime);

        // Update UI
        HealthUI_text.text = "HP: " + health.ToString();
        StaminaUI_text.text = "Stamina: " + Mathf.RoundToInt(currentStamina).ToString();


        CheckDeath();
    }

    void CheckGrounded()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        float rayDistance = controller.height / 2f + groundCheckDistance;
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, rayDistance, groundMask);
        Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, isGrounded ? Color.green : Color.red);
    }

    void HandleStamina()
    {
        bool isSprinting = canSprint && Input.GetKey(KeyCode.LeftShift) &&
                         (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0);
        if (isSprinting)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            staminaRegenTimer = staminaRegenDelay;
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                canSprint = false;
            }
        }
        else
        {
            staminaRegenTimer -= Time.deltaTime;
            if (staminaRegenTimer <= 0)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina >= maxStamina)
                {
                    currentStamina = maxStamina;
                    canSprint = true;
                }
            }
        }
    }

    void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isCursorLocked = !isCursorLocked;
        }
        Cursor.lockState = isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isCursorLocked;
    }

    void CheckDeath()
    {
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }

    public void TakeDamage(int damage, int punchIndex)
    {
        health -= damage;
        Debug.Log($"Player took {damage} damage. Health: {health}");

        // Trigger strong directional sway
        TriggerHitSway(punchIndex);
    }

    private void TriggerHitSway(int punchIndex)
    {
        swayTimer = hitSwayDuration;
        lastHitPunchIndex = punchIndex;

        // Right-hand punch (index 1) -> head sways LEFT (negative yaw)
        // Left-hand punch (index 0) -> head sways RIGHT (positive yaw)
        float direction = (punchIndex == 1) ? -1f : 1f;
        currentSwayAmount = direction * hitSwayMagnitude;

        // Add a quick upward kick then down for realism
        currentVerticalOffset = hitVerticalKick;
    }

   
}