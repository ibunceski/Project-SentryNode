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
        bool isCrouching = Input.GetKey(KeyCode.C);
        ApplyCrouch(isCrouching);
        HandleMovement(isCrouching);
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

    private void HandleMovement(bool isCrouching)
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(horizontal, 0f, vertical);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float moveSpeed = isCrouching ? CrouchSpeed : (isRunning ? RunSpeed : WalkSpeed);

        Vector3 move = transform.TransformDirection(input) * moveSpeed;
        characterController.Move(move * Time.deltaTime);

        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y -= Gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleNoise()
    {
        bool isCrouching = Input.GetKey(KeyCode.C);
        footstepNoiseTimer -= Time.deltaTime;
        if (isCrouching)
        {
            return;
        }

        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        float speed = horizontalVelocity.magnitude;

        if (characterController.isGrounded && speed > FootstepNoiseThreshold && footstepNoiseTimer <= 0f)
        {
            HearingSystem.ReportNoise(transform.position, 8f, HearingSystem.NoiseType.Footstep);
            footstepNoiseTimer = FootstepNoiseInterval;
        }
    }

    private void HandleInteractionNoise()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            HearingSystem.ReportNoise(transform.position, 12f, HearingSystem.NoiseType.Interaction);
        }
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
