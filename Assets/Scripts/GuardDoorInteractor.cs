using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Opens nearby sliding doors for guards as they move through the level.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class GuardDoorInteractor : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField]
    private float detectionDistance = 1.5f;

    [SerializeField]
    private float detectionRadius = 0.3f;

    [SerializeField]
    private float eyeHeight = 1.1f;

    [SerializeField]
    private float movementThreshold = 0.05f;

    [Header("Door Timing")]
    [SerializeField]
    private float closeDelayAfterPass = 1.25f;

    [SerializeField]
    private float interactionCooldown = 0.2f;

    [SerializeField]
    private LayerMask detectionMask = Physics.DefaultRaycastLayers;

    private NavMeshAgent navMeshAgent;
    private float nextInteractTime;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        detectionMask &= ~(1 << gameObject.layer);
    }

    private void OnValidate()
    {
        detectionDistance = Mathf.Max(0.1f, detectionDistance);
        detectionRadius = Mathf.Max(0.01f, detectionRadius);
        movementThreshold = Mathf.Max(0.01f, movementThreshold);
        closeDelayAfterPass = Mathf.Max(0f, closeDelayAfterPass);
        interactionCooldown = Mathf.Max(0.01f, interactionCooldown);
    }

    private void Update()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        Vector3 movement = navMeshAgent.desiredVelocity;
        if (movement.sqrMagnitude < movementThreshold * movementThreshold)
        {
            return;
        }

        if (Time.time < nextInteractTime)
        {
            return;
        }

        Vector3 direction = movement.normalized;
        Vector3 origin = transform.position + (Vector3.up * eyeHeight);
        if (!Physics.SphereCast(
                origin,
                detectionRadius,
                direction,
                out RaycastHit hit,
                detectionDistance,
                detectionMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Door door = hit.collider.GetComponentInParent<Door>();
        if (door == null)
        {
            return;
        }

        door.OpenTemporarily(closeDelayAfterPass);
        nextInteractTime = Time.time + interactionCooldown;
    }
}
