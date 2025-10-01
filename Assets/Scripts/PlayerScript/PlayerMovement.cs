using Unity.VisualScripting;
using UnityEngine;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private GameObject zooming_Effect;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float groundAcceleration = 50f;
    [SerializeField] private float friction = 12f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, -0.9f, 0);
    [SerializeField] private LayerMask groundMask;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private int maxAirJumps = 1;

    [Header("Slide Settings")]
    [SerializeField] private float slideSpeed = 20f;
    [SerializeField] private float slideDuration = 1f;
    [SerializeField] private float slideCooldown = 0.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private bool isGrounded;
    private bool isSliding;
    private bool isWallRunning;
    private float slideTimeRemaining;
    private float slideCooldownRemaining;
    private int airJumpsRemaining;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        airJumpsRemaining = maxAirJumps;
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(
        transform.position + groundCheckOffset,
        groundCheckRadius,
        groundMask);

        HandleInput();
        HandleMovement();
        HandleGravity();
        HandleSliding();

        if (Input.GetButtonDown("Fire1"))
        {
            GetComponentInChildren<ArmMount>().FireAll();
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        moveDirection = (forward * vertical + right * horizontal).normalized;
    }

    private void HandleMovement()
    {
        if (isSliding) return;

        float targetSpeed = Input.GetKey(KeyCode.LeftControl) ? runSpeed : walkSpeed;

        Vector3 targetVelocity = moveDirection * targetSpeed;
        targetVelocity.y = velocity.y;

        velocity = Vector3.Lerp(velocity, targetVelocity, groundAcceleration * Time.deltaTime);
        ApplyFriction(friction);

        if (isGrounded)
        {
            airJumpsRemaining = maxAirJumps;
        }

        if (Input.GetButtonDown("Jump"))
        {
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
            }
            else if (airJumpsRemaining > 0)
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
                airJumpsRemaining--;
            }
        }
    }

    private void HandleGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else if (isWallRunning)
        {
            velocity.y -= gravity * 2f * Time.deltaTime;
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime;
        }
    }

    private void HandleSliding()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && isGrounded && slideCooldownRemaining <= 0)
        {
            isSliding = true;
            zooming_Effect.SetActive(true);
            slideTimeRemaining = slideDuration;

            Vector3 slideDirection = transform.forward;

            if (TryGetComponent<MouseMovement>(out var cam))
            {
                cam.SetSlideEffects(true, 1f);
            }

            if (velocity.magnitude > 0.1f)
            {
                slideDirection = velocity.normalized;
            }
            velocity = slideDirection * slideSpeed;
        }

        if (isSliding)
        {
            //float speedNormalized = Mathf.Clamp01(velocity.magnitude / maxSlideSpeed);
            Debug.Log(message: "IS sliding");
            slideTimeRemaining -= Time.deltaTime;

            //if (TryGetComponent<MouseMovement>(out var cam))
            //{
            //    cam.SetSlideEffects(true, speedNormalized);
            //}

            if (slideTimeRemaining <= 0 || !isGrounded || velocity.magnitude < walkSpeed)
            {
                isSliding = false;
                zooming_Effect.SetActive(false);
                slideCooldownRemaining = slideCooldown;
            }
        }
        else
        {
            if (TryGetComponent<MouseMovement>(out var cam))
            {
                cam.SetSlideEffects(false, 0f);
            }

            if (slideCooldownRemaining > 0)
            {
                slideCooldownRemaining -= Time.deltaTime;
            }
        }
    }

    private void ApplyFriction(float amount)
    {
        Vector3 horizontalVelocity = velocity;
        horizontalVelocity.y = 0;

        if (horizontalVelocity.magnitude > 0)
        {
            float reduction = amount * Time.deltaTime;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, reduction);

            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (isSliding && hit.normal.y < 0.3f)
        {
            velocity = Vector3.Reflect(velocity, hit.normal) * 0.7f;
        }
    }

    public float GetSpeed()
    {
        Vector3 horizontalVelocity = velocity;
        horizontalVelocity.y = 0;
        return horizontalVelocity.magnitude;
    }

    public float GetTotalSpeed()
    {
        return velocity.magnitude;
    }
}
