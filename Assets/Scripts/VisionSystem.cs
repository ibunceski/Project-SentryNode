using UnityEngine;

/// <summary>
/// Performs graduated vision-based awareness checks and tracks the player's last known position.
/// </summary>
public class VisionSystem : MonoBehaviour
{
    /// <summary>
    /// Represents high-level awareness tiers driven by suspicion value.
    /// </summary>
    public enum SuspicionLevel
    {
        /// <summary>
        /// The guard is not currently alerted.
        /// </summary>
        Unaware,

        /// <summary>
        /// The guard is alert but not fully certain.
        /// </summary>
        Suspicious,

        /// <summary>
        /// The guard has fully detected the player.
        /// </summary>
        Detected
    }

    [SerializeField]
    private float viewDistance = 10f;

    [SerializeField]
    private float fieldOfViewAngle = 120f;

    [SerializeField]
    private LayerMask obstacleMask;

    [SerializeField]
    private LayerMask playerMask;

    private const int MaxPlayerColliders = 16;
    private const float EyeHeight = 1.6f;
    private readonly Collider[] playerOverlapResults = new Collider[MaxPlayerColliders];

    private float suspicion;
    private SuspicionLevel suspicionLevel;
    private bool hasVisualContact;
    private Transform playerTransform;
    private Vector3 suspicionFocusPosition;
    private bool hasSuspicionFocus;
    private Vector3 lastRaycastTargetPosition;
    private bool hasRaycastTargetPosition;

    /// <summary>
    /// Gets the last known player position recorded by this vision system.
    /// </summary>
    public Vector3 LastKnownPosition { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the system has ever detected the player since the last clear.
    /// </summary>
    public bool HasLastKnownPosition { get; private set; }

    /// <summary>
    /// Gets the currently visible player transform when direct line of sight is clear.
    /// </summary>
    public Transform PlayerTransform => playerTransform;

    /// <summary>
    /// Gets the current perception target position used by guard logic.
    /// </summary>
    public Vector3 PlayerPosition => hasSuspicionFocus ? suspicionFocusPosition : LastKnownPosition;

    /// <summary>
    /// Gets the current suspicion value in the range 0 to 100.
    /// </summary>
    public float Suspicion => suspicion;

    /// <summary>
    /// Gets the current suspicion tier derived from <see cref="Suspicion"/>.
    /// </summary>
    public SuspicionLevel CurrentSuspicionLevel => suspicionLevel;

    /// <summary>
    /// Gets the position the guard is currently suspicious of.
    /// </summary>
    public Vector3 SuspicionFocusPosition => suspicionFocusPosition;

    /// <summary>
    /// Gets a value indicating whether a suspicion focus position exists this frame.
    /// </summary>
    public bool HasSuspicionFocus => hasSuspicionFocus;

    private void Update()
    {
        PerceptionResult perception = EvaluatePerception();
        hasRaycastTargetPosition = perception.HasRaycastTarget;
        lastRaycastTargetPosition = perception.RaycastTargetPosition;
        hasVisualContact = perception.HasClearLineOfSight;
        hasSuspicionFocus = perception.HasSuspicionFocus;
        suspicionFocusPosition = perception.SuspicionFocusPosition;

        if (perception.HasClearLineOfSight)
        {
            playerTransform = perception.VisiblePlayerTransform;
        }
        else
        {
            playerTransform = null;
        }

        suspicion = Mathf.Clamp(suspicion + perception.SuspicionRatePerSecond * Time.deltaTime, 0f, 100f);
        suspicionLevel = ResolveSuspicionLevel(suspicion);

        if (CanSeePlayer() && hasSuspicionFocus)
        {
            LastKnownPosition = suspicionFocusPosition;
            HasLastKnownPosition = true;
        }
    }

    /// <summary>
    /// Returns whether suspicion has reached full detection threshold.
    /// </summary>
    /// <returns><see langword="true"/> when suspicion is at least 80; otherwise <see langword="false"/>.</returns>
    public bool CanSeePlayer()
    {
        return suspicion >= 80f;
    }

    /// <summary>
    /// Clears the last-known-position flag so guard logic can require a fresh detection.
    /// </summary>
    public void ClearLastKnownPosition()
    {
        HasLastKnownPosition = false;
    }

    private PerceptionResult EvaluatePerception()
    {
        Vector3 eyePosition = GetEyePosition();
        float halfFov = fieldOfViewAngle * 0.5f;
        float viewDistanceSqr = viewDistance * viewDistance;
        float bestVisibleDistanceSqr = float.MaxValue;
        float bestBlockedDistanceSqr = float.MaxValue;
        Transform bestVisibleTransform = null;
        Vector3 bestVisiblePosition = Vector3.zero;
        Vector3 bestBlockedPosition = Vector3.zero;
        bool hasBlockedInFov = false;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            viewDistance,
            playerOverlapResults,
            playerMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return new PerceptionResult(-20f);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = playerOverlapResults[i];
            if (collider == null)
            {
                continue;
            }

            Transform target = collider.transform;
            Vector3 targetPosition = target.position;
            Vector3 toTarget = targetPosition - eyePosition;
            float distanceSqr = toTarget.sqrMagnitude;

            if (distanceSqr <= 0.0001f || distanceSqr > viewDistanceSqr)
            {
                continue;
            }

            Vector3 directionToTarget = toTarget.normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > halfFov)
            {
                continue;
            }

            float distance = Mathf.Sqrt(distanceSqr);
            bool blocked = Physics.Raycast(
                eyePosition,
                directionToTarget,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                hasBlockedInFov = true;
                if (distanceSqr < bestBlockedDistanceSqr)
                {
                    bestBlockedDistanceSqr = distanceSqr;
                    bestBlockedPosition = targetPosition;
                }

                continue;
            }

            if (distanceSqr < bestVisibleDistanceSqr)
            {
                bestVisibleDistanceSqr = distanceSqr;
                bestVisibleTransform = target;
                bestVisiblePosition = targetPosition;
            }
        }

        if (bestVisibleTransform != null)
        {
            float visibleDistance = Mathf.Sqrt(bestVisibleDistanceSqr);
            float gain = visibleDistance < 5f ? 60f : (visibleDistance <= 10f ? 30f : -20f);

            return new PerceptionResult(gain)
            {
                HasClearLineOfSight = true,
                VisiblePlayerTransform = bestVisibleTransform,
                HasSuspicionFocus = true,
                SuspicionFocusPosition = bestVisiblePosition,
                HasRaycastTarget = true,
                RaycastTargetPosition = bestVisiblePosition
            };
        }

        if (hasBlockedInFov)
        {
            return new PerceptionResult(5f)
            {
                HasClearLineOfSight = false,
                VisiblePlayerTransform = null,
                HasSuspicionFocus = true,
                SuspicionFocusPosition = bestBlockedPosition,
                HasRaycastTarget = true,
                RaycastTargetPosition = bestBlockedPosition
            };
        }

        return new PerceptionResult(-20f);
    }

    private Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * EyeHeight;
    }

    private void OnDrawGizmos()
    {
        Vector3 eyePosition = GetEyePosition();
        float halfFov = fieldOfViewAngle * 0.5f;
        Color color = hasVisualContact ? Color.red : Color.green;
        Gizmos.color = color;

        Vector3 leftDirection = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
        Vector3 rightDirection = Quaternion.Euler(0f, halfFov, 0f) * transform.forward;

        Gizmos.DrawLine(eyePosition, eyePosition + leftDirection * viewDistance);
        Gizmos.DrawLine(eyePosition, eyePosition + rightDirection * viewDistance);

        const int arcSegments = 20;
        Vector3 previousPoint = eyePosition + leftDirection * viewDistance;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfFov, halfFov, t);
            Vector3 segmentDirection = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 currentPoint = eyePosition + segmentDirection * viewDistance;
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        Vector3 rayEnd = hasRaycastTargetPosition
            ? lastRaycastTargetPosition
            : eyePosition + transform.forward * viewDistance;
        Gizmos.DrawLine(eyePosition, rayEnd);
    }

    private static SuspicionLevel ResolveSuspicionLevel(float value)
    {
        if (value >= 80f)
        {
            return SuspicionLevel.Detected;
        }

        if (value >= 40f)
        {
            return SuspicionLevel.Suspicious;
        }

        return SuspicionLevel.Unaware;
    }

    private struct PerceptionResult
    {
        public PerceptionResult(float suspicionRatePerSecond)
        {
            SuspicionRatePerSecond = suspicionRatePerSecond;
            HasClearLineOfSight = false;
            VisiblePlayerTransform = null;
            HasSuspicionFocus = false;
            SuspicionFocusPosition = Vector3.zero;
            HasRaycastTarget = false;
            RaycastTargetPosition = Vector3.zero;
        }

        public float SuspicionRatePerSecond { get; set; }
        public bool HasClearLineOfSight { get; set; }
        public Transform VisiblePlayerTransform { get; set; }
        public bool HasSuspicionFocus { get; set; }
        public Vector3 SuspicionFocusPosition { get; set; }
        public bool HasRaycastTarget { get; set; }
        public Vector3 RaycastTargetPosition { get; set; }
    }
}
