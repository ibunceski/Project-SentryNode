using UnityEngine;

/// <summary>
/// Detects interactables in front of the player and triggers them.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField]
    private Transform cameraTransform;

    [SerializeField]
    private float interactionDistance = 2.5f;

    [SerializeField]
    private LayerMask interactionMask = Physics.DefaultRaycastLayers;

    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
    }

    private void Awake()
    {
        interactionMask &= ~(1 << gameObject.layer);
        ResolveCameraTransform();
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    private void ResolveCameraTransform()
    {
        if (cameraTransform != null)
        {
            return;
        }

        Camera childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
        {
            cameraTransform = childCamera.transform;
        }
    }

    private void TryInteract()
    {
        if (cameraTransform == null)
        {
            ResolveCameraTransform();
            if (cameraTransform == null)
            {
                return;
            }
        }

        float maxDistance = Mathf.Max(0.1f, interactionDistance);
        Vector3 rayOrigin = cameraTransform.position + (cameraTransform.forward * 0.05f);
        Ray ray = new Ray(rayOrigin, cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        IInteractable interactable = FindInteractable(hit.collider);
        if (interactable != null)
        {
            interactable.Interact(gameObject);
        }
    }

    private static IInteractable FindInteractable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        MonoBehaviour[] candidates = collider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is IInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }
}
