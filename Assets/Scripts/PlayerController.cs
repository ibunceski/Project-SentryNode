using UnityEngine;

/// <summary>
/// Minimal first-person controller with movement, look, crouch, and noise reporting.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float mouseSensitivity = 2f;

    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform cameraTransform;

    private const float WalkSpeed = 3f;
    private const float RunSpeed = 6f;
    private const float CrouchSpeed = 1.5f;
    private const float Gravity = 9.81f;
    private const float NormalHeight = 1.8f;
    private const float CrouchHeight = 1f;
    private const float NormalCameraY = 1.6f;
    private const float CrouchCameraY = 0.5f;
    private const float FootstepNoiseThreshold = 4.5f;
    private const float FootstepNoiseInterval = 0.4f;

    private Vector3 velocity;
    private float pitch;
    private float footstepNoiseTimer;
    private bool isCursorLocked;
    private bool isCrouching;
    private bool isSprinting;
    private float currentSpeed;

    private void Start()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        ApplyCrouch(false);
        SetCursorLock(true);
    }

    private void Update()
    {
        HandleCursorToggle();
        HandleLook();
        isCrouching = Input.GetKey(KeyCode.C);
        ApplyCrouch(isCrouching);
        HandleMovement();
        HandleNoise();
        HandleInteractionNoise();
    }

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(!isCursorLocked);
        }
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        bool isCrouching = Input.GetKey(KeyCode.C);
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && characterController.isGrounded;

        float currentSpeed;
        if (isCrouching)
        {
            currentSpeed = 1.5f;
        }
        else if (isSprinting)
        {
            currentSpeed = 6f;
        }
        else
        {
            currentSpeed = 3f;
        }

        this.isCrouching = isCrouching;
        this.isSprinting = isSprinting;
        this.currentSpeed = currentSpeed;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        characterController.Move(move * currentSpeed * Time.deltaTime);

        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y -= Gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleNoise()
    {
        footstepNoiseTimer -= Time.deltaTime;
        if (isCrouching)
        {
            return;
        }

        if (isSprinting && characterController.isGrounded && footstepNoiseTimer <= 0f)
        {
            HearingSystem.ReportNoise(transform.position, 8f, HearingSystem.NoiseType.Footstep);
            footstepNoiseTimer = FootstepNoiseInterval;
        }
    }

    private void HandleInteractionNoise()
    {
    }

    private void ApplyCrouch(bool isCrouching)
    {
        float targetHeight = isCrouching ? CrouchHeight : NormalHeight;
        characterController.height = targetHeight;
        characterController.center = new Vector3(0f, targetHeight * 0.5f, 0f);

        if (cameraTransform != null)
        {
            Vector3 localPosition = cameraTransform.localPosition;
            localPosition.y = isCrouching ? CrouchCameraY : NormalCameraY;
            cameraTransform.localPosition = localPosition;
        }
    }

    private void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
