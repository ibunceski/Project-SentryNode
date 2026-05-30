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

    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

    [SerializeField]
    private Transform playerTarget;

    [Header("Close-Range Awareness")]
    [SerializeField]
    private float proximityAwarenessRadius = 1.75f;

    [SerializeField]
    private bool proximityRequiresLineOfSight = true;

    [SerializeField]
    [Range(0f, 100f)]
    private float proximitySuspicionFloor = 85f;

    [SerializeField]
    private float proximitySuspicionRatePerSecond = 80f;

    private float suspicion;
    private SuspicionLevel suspicionLevel;
    private bool hasVisualContact;
    private Transform playerTransform;
    private Vector3 suspicionFocusPosition;
    private bool hasSuspicionFocus;
    private Vector3 lastRaycastTargetPosition;
    private bool hasRaycastTargetPosition;

    /// <summary>
    /// Gets the vision range used for field-of-view checks.
    /// </summary>
    public float ViewDistance => viewDistance;

    /// <summary>
    /// Gets the full field-of-view angle in degrees.
    /// </summary>
    public float FieldOfViewAngle => fieldOfViewAngle;

    /// <summary>
    /// Gets the mask used to occlude guard vision rays.
    /// </summary>
    public LayerMask OcclusionMask => obstacleMask & ~playerMask;

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
        EnsurePlayerTarget();
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
        if (perception.HasProximityContact)
        {
            suspicion = Mathf.Max(suspicion, proximitySuspicionFloor);
        }

        suspicionLevel = ResolveSuspicionLevel(suspicion);

        if (CanSeePlayer() && hasSuspicionFocus)
        {
            LastKnownPosition = suspicionFocusPosition;
            HasLastKnownPosition = true;
        }

        if (perception.HasDirection)
        {
            Debug.DrawRay(
                perception.EyePosition,
                perception.DirectionToPlayer * viewDistance,
                CanSeePlayer() ? Color.red : Color.green);
        }
    }

    /// <summary>
    /// Returns whether suspicion has reached full detection threshold.
    /// </summary>
    /// <returns><see langword="true"/> when suspicion is at least 80; otherwise <see langword="false"/>.</returns>
    public bool CanSeePlayer()
    {
        return suspicion >= 80f && hasVisualContact;
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
        if (playerTarget == null)
        {
            return new PerceptionResult(-20f);
        }

        Vector3 eyePos = GetEyePosition();
        Vector3 playerCenter = playerTarget.position + Vector3.up * 0.9f;
        Vector3 toPlayer = playerCenter - eyePos;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer <= 0.0001f)
        {
            return new PerceptionResult(-20f);
        }

        Vector3 direction = toPlayer.normalized;
        float angle = Vector3.Angle(transform.forward, direction);
        bool inFov = angle < fieldOfViewAngle * 0.5f;
        bool inRange = distanceToPlayer <= viewDistance;
        bool inFovAndRange = inFov && inRange;
        bool inProximityRange = distanceToPlayer <= Mathf.Max(0f, proximityAwarenessRadius);

        int obstacleOnlyMask = obstacleMask.value & ~playerMask.value;
        bool hasObstacleBetween = false;
        Vector3 rayEnd = playerCenter;

        if (inFovAndRange || inProximityRange)
        {
            bool hitObstacle = Physics.Raycast(
                eyePos,
                direction,
                out RaycastHit hit,
                distanceToPlayer,
                obstacleOnlyMask,
                QueryTriggerInteraction.Ignore);

            if (hitObstacle)
            {
                rayEnd = hit.point;
                hasObstacleBetween = hit.distance <= distanceToPlayer;
            }
        }

        bool proximityContact = inProximityRange
            && (!proximityRequiresLineOfSight || !hasObstacleBetween);
        bool clearLineOfSight = (inFovAndRange && !hasObstacleBetween) || proximityContact;
        float suspicionRate;

        if (proximityContact)
        {
            suspicionRate = Mathf.Max(0f, proximitySuspicionRatePerSecond);
        }
        else if (clearLineOfSight && distanceToPlayer < 5f)
        {
            suspicionRate = 60f;
        }
        else if (clearLineOfSight && distanceToPlayer <= 10f)
        {
            suspicionRate = 30f;
        }
        else if (inFovAndRange && hasObstacleBetween)
        {
            suspicionRate = 5f;
        }
        else
        {
            suspicionRate = -20f;
        }

        return new PerceptionResult(suspicionRate)
        {
            HasClearLineOfSight = clearLineOfSight,
            VisiblePlayerTransform = clearLineOfSight ? playerTarget : null,
            HasSuspicionFocus = inFovAndRange || proximityContact,
            SuspicionFocusPosition = playerTarget.position,
            HasProximityContact = proximityContact,
            HasRaycastTarget = true,
            RaycastTargetPosition = rayEnd,
            EyePosition = eyePos,
            DirectionToPlayer = direction,
            HasDirection = true
        };
    }

    private Vector3 GetEyePosition()
    {
        return transform.position + eyeOffset;
    }

    private void EnsurePlayerTarget()
    {
        if (playerTarget != null)
        {
            return;
        }

        GameObject playerByTag = GameObject.FindWithTag("Player");
        if (playerByTag != null)
        {
            playerTarget = playerByTag.transform;
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            viewDistance * 2f,
            overlapResults,
            playerMask,
            QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
        {
            return;
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = overlapResults[i];
            if (collider == null)
            {
                continue;
            }

            playerTarget = collider.transform;
            return;
        }
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
            HasProximityContact = false;
            HasRaycastTarget = false;
            RaycastTargetPosition = Vector3.zero;
            EyePosition = Vector3.zero;
            DirectionToPlayer = Vector3.zero;
            HasDirection = false;
        }

        public float SuspicionRatePerSecond { get; set; }
        public bool HasClearLineOfSight { get; set; }
        public Transform VisiblePlayerTransform { get; set; }
        public bool HasSuspicionFocus { get; set; }
        public Vector3 SuspicionFocusPosition { get; set; }
        public bool HasProximityContact { get; set; }
        public bool HasRaycastTarget { get; set; }
        public Vector3 RaycastTargetPosition { get; set; }
        public Vector3 EyePosition { get; set; }
        public Vector3 DirectionToPlayer { get; set; }
        public bool HasDirection { get; set; }
    }

    private const int MaxPlayerColliders = 16;
    private readonly Collider[] overlapResults = new Collider[MaxPlayerColliders];
}
