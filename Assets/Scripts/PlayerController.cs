using UnityEngine;

/// <summary>
/// Minimal first-person controller with movement, look, crouch, and noise reporting.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField]
    private float mouseSensitivity = 2f;

    [Header("References")]
    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform cameraTransform;

    [SerializeField]
    private AudioSource footstepAudioSource;

    [Header("Footstep Clips")]
    [SerializeField]
    private AudioClip walkFootstepClip;

    [SerializeField]
    private AudioClip sprintFootstepClip;

    [SerializeField]
    private AudioClip crouchFootstepClip;

    [Header("Footstep Timing")]
    [SerializeField]
    private float walkFootstepInterval = 0.5f;

    [SerializeField]
    private float sprintFootstepInterval = 0.3f;

    [SerializeField]
    private float crouchFootstepInterval = 0.75f;

    [Header("Footstep Volume")]
    [SerializeField]
    private float walkFootstepVolume = 0.35f;

    [SerializeField]
    private float sprintFootstepVolume = 0.6f;

    [SerializeField]
    private float crouchFootstepVolume = 0.2f;

    [Header("Footstep Noise")]
    [SerializeField]
    private float walkNoiseRadius = 4.5f;

    [SerializeField]
    private float sprintNoiseRadius = 8f;

    [SerializeField]
    private float crouchNoiseRadius = 0f;

    [SerializeField]
    private float moveInputThreshold = 0.01f;

    private const float WalkSpeed = 3f;
    private const float RunSpeed = 6f;
    private const float CrouchSpeed = 1.5f;
    private const float Gravity = 9.81f;
    private const float NormalHeight = 1.8f;
    private const float CrouchHeight = 1f;
    private const float NormalCameraY = 1.6f;
    private const float CrouchCameraY = 0.5f;

    private Vector3 velocity;
    private float pitch;
    private float footstepTimer;
    private bool isCursorLocked;
    private bool isCrouching;
    private bool isSprinting;
    private bool isMoving;

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

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        footstepAudioSource.loop = false;
        footstepAudioSource.playOnAwake = false;

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
        HandleFootsteps();
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
            currentSpeed = CrouchSpeed;
        }
        else if (isSprinting)
        {
            currentSpeed = RunSpeed;
        }
        else
        {
            currentSpeed = WalkSpeed;
        }

        this.isCrouching = isCrouching;
        this.isSprinting = isSprinting;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;
        isMoving = move.sqrMagnitude > moveInputThreshold;
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

    private void HandleFootsteps()
    {
        footstepTimer -= Time.deltaTime;

        if (!characterController.isGrounded || !isMoving)
        {
            footstepTimer = 0f;
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }

            return;
        }

        float interval;
        float volume;
        float noiseRadius;
        AudioClip clip;

        if (isCrouching)
        {
            interval = Mathf.Max(0.05f, crouchFootstepInterval);
            volume = Mathf.Clamp01(crouchFootstepVolume);
            noiseRadius = Mathf.Max(0f, crouchNoiseRadius);
            clip = crouchFootstepClip != null ? crouchFootstepClip : walkFootstepClip;
        }
        else if (isSprinting)
        {
            interval = Mathf.Max(0.05f, sprintFootstepInterval);
            volume = Mathf.Clamp01(sprintFootstepVolume);
            noiseRadius = Mathf.Max(0f, sprintNoiseRadius);
            clip = sprintFootstepClip != null ? sprintFootstepClip : walkFootstepClip;
        }
        else
        {
            interval = Mathf.Max(0.05f, walkFootstepInterval);
            volume = Mathf.Clamp01(walkFootstepVolume);
            noiseRadius = Mathf.Max(0f, walkNoiseRadius);
            clip = walkFootstepClip;
        }

        if (footstepTimer > 0f)
        {
            return;
        }

        if (clip != null && footstepAudioSource != null)
        {
            footstepAudioSource.PlayOneShot(clip, volume);
        }

        if (noiseRadius > 0f)
        {
            HearingSystem.ReportNoise(transform.position, noiseRadius, HearingSystem.NoiseType.Footstep);
        }

        footstepTimer = interval;
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
