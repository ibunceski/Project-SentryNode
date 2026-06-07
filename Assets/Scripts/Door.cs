using UnityEngine;

/// <summary>
/// Opens and closes a sliding door by moving in local space.
/// </summary>
public class Door : MonoBehaviour, IInteractable
{
    [Header("Door Motion")]
    [SerializeField]
    private Vector3 slideDirection = Vector3.right;

    [SerializeField]
    private float slideDistance = 1f;

    [SerializeField]
    private float openSpeed = 6f;

    [SerializeField]
    private bool startsOpen;

    [SerializeField]
    private float defaultAutoCloseDelay = 1.5f;

    private Vector3 closedLocalPosition;
    private float currentDistance;
    private bool isOpen;
    private float autoCloseAtTime = -1f;

    public bool IsOpen => isOpen;

    private void OnValidate()
    {
        slideDistance = Mathf.Max(0f, slideDistance);
        openSpeed = Mathf.Max(0.01f, openSpeed);
        defaultAutoCloseDelay = Mathf.Max(0f, defaultAutoCloseDelay);
    }

    private void Awake()
    {
        closedLocalPosition = transform.localPosition;
        isOpen = startsOpen;
        currentDistance = isOpen ? slideDistance : 0f;
        ApplyPosition(currentDistance);
    }

    private void Update()
    {
        if (isOpen && autoCloseAtTime >= 0f && Time.time >= autoCloseAtTime)
        {
            isOpen = false;
            autoCloseAtTime = -1f;
        }

        float targetDistance = isOpen ? slideDistance : 0f;
        float movementSpeed = Mathf.Max(0.01f, openSpeed);
        currentDistance = Mathf.MoveTowards(currentDistance, targetDistance, movementSpeed * Time.deltaTime);
        ApplyPosition(currentDistance);
    }

    public void Interact(GameObject interactor)
    {
        isOpen = !isOpen;
        autoCloseAtTime = -1f;
    }

    public void Open()
    {
        isOpen = true;
        autoCloseAtTime = -1f;
    }

    public void Close()
    {
        isOpen = false;
        autoCloseAtTime = -1f;
    }

    public void OpenTemporarily(float closeDelay)
    {
        isOpen = true;
        float delay = closeDelay > 0f ? closeDelay : defaultAutoCloseDelay;
        if (delay <= 0f)
        {
            autoCloseAtTime = -1f;
            return;
        }

        autoCloseAtTime = Time.time + delay;
    }

    private void ApplyPosition(float distance)
    {
        Vector3 direction = slideDirection.sqrMagnitude > 0.0001f ? slideDirection.normalized : Vector3.right;
        transform.localPosition = closedLocalPosition + direction * distance;
    }
}
