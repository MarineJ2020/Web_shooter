using UnityEngine;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float acceleration = 20f; // How quickly to reach target speed
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
    public float staminaDrainRate = 20f; // Stamina per second while sprinting
    public float staminaRegenRate = 10f; // Stamina per second while not sprinting
    public float staminaRegenDelay = 1f; // Delay before regen starts

    [Header("Player UI")]
    public TextMeshProUGUI HealthUI_text;
    public TextMeshProUGUI StaminaUI_text;



    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity; // For gravity/jumping
    private Vector3 moveVelocity; // For horizontal movement
    private float xRotation = 0f;
    private bool isCursorLocked = true;
    private bool isGrounded;
    private float currentStamina;
    private float staminaRegenTimer;
    private bool canSprint;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        if (playerCamera != null)
        {
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0, 0.5f, 0); // Eye height
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize stamina
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
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * mouseX);

        // Ground check
        CheckGrounded();

        // Handle stamina
        HandleStamina();

        // Get raw input (instant response, no smoothing)
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Build desired movement direction in LOCAL space
        Vector3 inputDirection = new Vector3(inputX, 0, inputZ);
        float inputMagnitude = inputDirection.magnitude;

        // Normalize only if there's actual input (prevents division by zero and drifting)
        if (inputMagnitude > 0.01f)
            inputDirection /= inputMagnitude; // Normalize to prevent diagonal speed boost

        // Transform input into local forward/right relative to player rotation
        Vector3 desiredMove = transform.right * inputDirection.x + transform.forward * inputDirection.z;

        // Determine target speed (walk or sprint)
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift) &&
                             currentStamina > 0 &&
                             canSprint &&
                             inputMagnitude > 0.5f; // Only sprint if moving significantly

        float targetSpeed = wantsToSprint ? runSpeed : walkSpeed;

        // Calculate target velocity
        Vector3 targetVelocity = desiredMove * targetSpeed * Mathf.Clamp01(inputMagnitude);

        // Smoothly accelerate/decelerate toward target velocity
        moveVelocity = Vector3.Lerp(moveVelocity, targetVelocity, acceleration * Time.deltaTime);

        // Apply horizontal movement
        controller.Move(moveVelocity * Time.deltaTime);

        // Gravity and jump
        if (isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -0.1f; // Small downward force

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

        HealthUI_text.text = "HP: "+health.ToString();
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
        StaminaUI_text.text = "Stamina: "+currentStamina.ToString();
        bool isSprinting = canSprint && Input.GetKey(KeyCode.LeftShift) &&
                         (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0);

        if (isSprinting)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            staminaRegenTimer = staminaRegenDelay;
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                canSprint = false; // Stop sprinting
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
                    canSprint = true; // Allow sprinting again
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
}