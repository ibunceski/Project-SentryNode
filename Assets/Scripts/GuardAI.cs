using System;
using System.Reflection;
using StealthAI.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls a guard using a behavior tree composed of vision, hearing, investigation, and patrol behaviors.
/// </summary>
public class GuardAI : MonoBehaviour
{
    /// <summary>
    /// Represents the high-level guard behavior state for debugging and external UI.
    /// </summary>
    public enum GuardState
    {
        /// <summary>
        /// The guard is following its patrol routine.
        /// </summary>
        Patrolling,

        /// <summary>
        /// The guard is aware of potential danger and focusing on a possible target.
        /// </summary>
        Suspicious,

        /// <summary>
        /// The guard is actively pursuing the player.
        /// </summary>
        Chasing,

        /// <summary>
        /// The guard is moving to an investigation target.
        /// </summary>
        Investigating,

        /// <summary>
        /// The guard is searching an area after reaching an investigation target.
        /// </summary>
        Searching
    }

    [Header("Injected Systems")]
    [SerializeField]
    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private VisionSystem visionSystem;

    [SerializeField]
    private HearingSystem hearingSystem;

    [SerializeField]
    private PatrolSystem patrolSystem;

    [Header("Search Tuning")]
    [SerializeField]
    private float searchDurationSeconds = 3f;

    [SerializeField]
    private float destinationEpsilon = 0.15f;

    [SerializeField]
    private float searchCircleRadius = 2f;

    [SerializeField]
    private float searchCircleAngularSpeed = 120f;

    [Header("Runtime Debug")]
    [SerializeField]
    private string lastActiveLeafNodeName = "None";

    [SerializeField]
    private GuardState currentGuardState = GuardState.Patrolling;

    private Node root;
    private Sequence investigateSequenceNode;
    private Vector3 lastKnownPosition;
    private bool hasLastKnownPosition;
    private float searchTimer;
    private bool searchTimerActive;
    private Vector3 searchCenterPosition;
    private float searchAngleDegrees;
    private float suspiciousTimer;
    private bool suspiciousTimerActive;
    private float suspiciousCooldownUntil;
    private float defaultAgentSpeed = 3.5f;
    private string lastLoggedActiveNodeName = "None";
    private bool hasLoggedMissingHearingSystem;
    private float nextNavMeshRecoveryTime;
    private bool hasLoggedNavMeshIssue;
    private NodeState previousInvestigateSequenceState = NodeState.Failure;
    private float patrolNoPathTimer;

    /// <summary>
    /// Gets the last leaf node name evaluated by the behavior tree.
    /// </summary>
    public string LastActiveLeafNodeName => lastActiveLeafNodeName;

    /// <summary>
    /// Gets the active behavior tree node name used for debug visualization.
    /// </summary>
    public string ActiveNodeName => lastActiveLeafNodeName;

    /// <summary>
    /// Gets the current high-level state of the guard.
    /// </summary>
    public GuardState CurrentGuardState => currentGuardState;

    /// <summary>
    /// Builds and initializes the behavior tree.
    /// </summary>
    private void Start()
    {
        GuardAlertSystem.Reset();

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (visionSystem == null)
        {
            visionSystem = GetComponent<VisionSystem>();
        }

        if (hearingSystem == null)
        {
            hearingSystem = GetComponent<HearingSystem>();
        }

        if (patrolSystem == null)
        {
            patrolSystem = GetComponent<PatrolSystem>();
        }

        TryRecoverAgentOnNavMesh();

        if (navMeshAgent != null)
        {
            defaultAgentSpeed = navMeshAgent.speed;
        }

        root = BuildTree();
        patrolSystem?.ResetPatrol();
        patrolSystem?.NotifyMovementReset();
        if (navMeshAgent != null && patrolSystem != null)
        {
            navMeshAgent.isStopped = false;
            patrolSystem.ForceStartPatrol(navMeshAgent);
        }
    }

    /// <summary>
    /// Ticks the behavior tree once per frame.
    /// </summary>
    private void Update()
    {
        GuardAlertSystem.Tick();

        if (root == null)
        {
            return;
        }

        if (Time.time >= nextNavMeshRecoveryTime)
        {
            nextNavMeshRecoveryTime = Time.time + 1f;
            TryRecoverAgentOnNavMesh();

            if (currentGuardState == GuardState.Patrolling
                && patrolSystem != null
                && navMeshAgent != null
                && navMeshAgent.enabled
                && navMeshAgent.isOnNavMesh
                && !navMeshAgent.pathPending
                && !navMeshAgent.hasPath)
            {
                patrolSystem.ForceStartPatrol(navMeshAgent);
            }
        }

        root.Evaluate();
        HandleInvestigateSequenceCompletion();

        if (!string.Equals(lastLoggedActiveNodeName, ActiveNodeName, StringComparison.Ordinal))
        {
            Debug.Log($"[GuardAI] Transition: {lastLoggedActiveNodeName} -> {ActiveNodeName}");
            lastLoggedActiveNodeName = ActiveNodeName;
        }
    }

    private Node BuildTree()
    {
        Selector rootSelector = new Selector("ROOT");

        Sequence chaseSequence = new Sequence("Chase Sequence");
        chaseSequence.AddChild(new LambdaConditionNode("CanSeePlayer", EvaluateCanSeePlayer));
        chaseSequence.AddChild(new LambdaActionNode("ChasePlayer", EvaluateChasePlayer));

        Sequence suspiciousSequence = new Sequence("Suspicious Sequence");
        suspiciousSequence.AddChild(new LambdaConditionNode("IsSuspicious", EvaluateIsSuspicious));
        suspiciousSequence.AddChild(new LambdaActionNode("TurnTowardSuspicion", EvaluateTurnTowardSuspicion));

        Sequence noiseSequence = new Sequence("Noise Sequence");
        noiseSequence.AddChild(new LambdaConditionNode("HeardNoise", EvaluateHeardNoise));
        noiseSequence.AddChild(new LambdaActionNode("MoveToNoisePosition", EvaluateMoveToNoisePosition));
        noiseSequence.AddChild(new LambdaActionNode("ClearNoise", EvaluateClearNoise));

        investigateSequenceNode = new Sequence("Investigate Sequence");
        investigateSequenceNode.AddChild(new LambdaConditionNode("HasLastKnownPosition", EvaluateHasLastKnownPosition));
        investigateSequenceNode.AddChild(new LambdaActionNode("MoveToLastKnownPosition", EvaluateMoveToLastKnownPosition));
        investigateSequenceNode.AddChild(new LambdaActionNode("SearchArea", EvaluateSearchArea));

        LambdaActionNode patrolNode = new LambdaActionNode("Patrol", EvaluatePatrol);

        rootSelector.AddChild(chaseSequence);
        rootSelector.AddChild(noiseSequence);
        rootSelector.AddChild(suspiciousSequence);
        rootSelector.AddChild(investigateSequenceNode);
        rootSelector.AddChild(patrolNode);

        return rootSelector;
    }

    private NodeState EvaluateCanSeePlayer()
    {
        MarkLeafActive("CanSeePlayer");

        if (visionSystem == null)
        {
            return NodeState.Failure;
        }

        if (visionSystem.Suspicion < 80f)
        {
            return NodeState.Failure;
        }

        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            lastKnownPosition = playerPosition;
            hasLastKnownPosition = true;
            GuardAlertSystem.ReportPlayerSeen(playerPosition);
        }
        else if (visionSystem != null && visionSystem.HasLastKnownPosition)
        {
            lastKnownPosition = visionSystem.LastKnownPosition;
            hasLastKnownPosition = true;
            GuardAlertSystem.ReportPlayerSeen(lastKnownPosition);
        }

        return NodeState.Success;
    }

    private NodeState EvaluateIsSuspicious()
    {
        MarkLeafActive("IsSuspicious");

        if (visionSystem == null)
        {
            return NodeState.Failure;
        }

        if (Time.time < suspiciousCooldownUntil)
        {
            return NodeState.Failure;
        }

        if (visionSystem.CurrentSuspicionLevel != VisionSystem.SuspicionLevel.Suspicious)
        {
            return NodeState.Failure;
        }

        return NodeState.Success;
    }

    private NodeState EvaluateTurnTowardSuspicion()
    {
        MarkLeafActive("TurnTowardSuspicion");
        currentGuardState = GuardState.Suspicious;
        ResetSearchTimer();
        SetAgentSpeedMultiplier(0.6f);
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
        }

        if (!TryGetSuspicionFocusPosition(out Vector3 focusPosition))
        {
            return NodeState.Failure;
        }

        if (!suspiciousTimerActive)
        {
            suspiciousTimerActive = true;
            suspiciousTimer = 0f;
        }

        suspiciousTimer += Time.deltaTime;
        if (suspiciousTimer >= 5f)
        {
            suspiciousTimerActive = false;
            suspiciousCooldownUntil = Time.time + 2f;
            return NodeState.Failure;
        }

        Vector3 toFocus = focusPosition - transform.position;
        toFocus.y = 0f;
        if (toFocus.sqrMagnitude <= 0.0001f)
        {
            return NodeState.Running;
        }

        float turnSpeed = navMeshAgent != null ? navMeshAgent.angularSpeed : 360f;
        Quaternion targetRotation = Quaternion.LookRotation(toFocus.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

        return NodeState.Running;
    }

    private NodeState EvaluateChasePlayer()
    {
        MarkLeafActive("ChasePlayer");
        currentGuardState = GuardState.Chasing;
        ResetSearchTimer();
        suspiciousTimerActive = false;
        SetAgentSpeedMultiplier(1f);

        if (!TryGetPlayerPosition(out Vector3 playerPosition))
        {
            return NodeState.Failure;
        }

        SetDestination(playerPosition);
        lastKnownPosition = playerPosition;
        hasLastKnownPosition = true;
        GuardAlertSystem.ReportPlayerSeen(playerPosition);

        if (HasReachedDestination())
        {
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState EvaluateHasLastKnownPosition()
    {
        MarkLeafActive("HasLastKnownPosition");
        if (visionSystem != null && visionSystem.HasLastKnownPosition)
        {
            lastKnownPosition = visionSystem.LastKnownPosition;
            hasLastKnownPosition = true;
        }
        else if (!hasLastKnownPosition && GuardAlertSystem.CurrentAlert == GuardAlertSystem.AlertLevel.FullAlert)
        {
            lastKnownPosition = GuardAlertSystem.SharedLastKnownPosition;
            hasLastKnownPosition = true;
        }

        return hasLastKnownPosition ? NodeState.Success : NodeState.Failure;
    }

    private NodeState EvaluateMoveToLastKnownPosition()
    {
        MarkLeafActive("MoveToLastKnownPosition");
        currentGuardState = GuardState.Investigating;
        suspiciousTimerActive = false;
        SetAgentSpeedMultiplier(1f);

        if (!hasLastKnownPosition)
        {
            if (visionSystem != null && visionSystem.HasLastKnownPosition)
            {
                lastKnownPosition = visionSystem.LastKnownPosition;
                hasLastKnownPosition = true;
            }
            else if (GuardAlertSystem.CurrentAlert == GuardAlertSystem.AlertLevel.FullAlert)
            {
                lastKnownPosition = GuardAlertSystem.SharedLastKnownPosition;
                hasLastKnownPosition = true;
            }
        }

        if (!hasLastKnownPosition)
        {
            return NodeState.Failure;
        }

        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return NodeState.Failure;
        }

        if (!navMeshAgent.hasPath || Vector3.Distance(navMeshAgent.destination, lastKnownPosition) > 0.1f)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(lastKnownPosition);
        }

        if (!navMeshAgent.pathPending && navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return NodeState.Failure;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance + 0.2f)
        {
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState EvaluateSearchArea()
    {
        MarkLeafActive("SearchArea");
        currentGuardState = GuardState.Searching;
        suspiciousTimerActive = false;
        SetAgentSpeedMultiplier(1f);

        if (!searchTimerActive)
        {
            searchTimerActive = true;
            searchTimer = 0f;
            searchCenterPosition = hasLastKnownPosition ? lastKnownPosition : transform.position;
            searchAngleDegrees = 0f;
        }

        searchTimer += Time.deltaTime;
        UpdateSearchCircleMovement();
        float requiredSearchDuration = Mathf.Max(0.1f, searchDurationSeconds);
        if (searchTimer < requiredSearchDuration)
        {
            return NodeState.Running;
        }

        ResetInvestigationMemory();
        ResetSearchTimer();
        return NodeState.Success;
    }

    private NodeState EvaluateHeardNoise()
    {
        MarkLeafActive("HeardNoise");

        if (hearingSystem == null)
        {
            if (!hasLoggedMissingHearingSystem)
            {
                Debug.LogError("[GuardAI] HearingSystem reference is null. Assign HearingSystem on the same guard GameObject.");
                hasLoggedMissingHearingSystem = true;
            }

            return NodeState.Failure;
        }

        hasLoggedMissingHearingSystem = false;
        if (!hearingSystem.HeardNoise)
        {
            return NodeState.Failure;
        }

        GuardAlertSystem.ReportSuspicious(hearingSystem.NoisePosition);
        return NodeState.Success;
    }

    private NodeState EvaluateMoveToNoisePosition()
    {
        MarkLeafActive("MoveToNoisePosition");
        currentGuardState = GuardState.Investigating;
        suspiciousTimerActive = false;
        SetAgentSpeedMultiplier(1f);

        if (hearingSystem == null)
        {
            if (!hasLoggedMissingHearingSystem)
            {
                Debug.LogError("[GuardAI] HearingSystem reference is null. Assign HearingSystem on the same guard GameObject.");
                hasLoggedMissingHearingSystem = true;
            }

            return NodeState.Failure;
        }

        if (!hearingSystem.HeardNoise)
        {
            return NodeState.Failure;
        }

        Vector3 noisePosition = hearingSystem.NoisePosition;
        SetDestination(noisePosition);

        if (HasReachedDestination())
        {
            lastKnownPosition = noisePosition;
            hasLastKnownPosition = true;
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState EvaluateClearNoise()
    {
        MarkLeafActive("ClearNoise");

        if (hearingSystem == null)
        {
            if (!hasLoggedMissingHearingSystem)
            {
                Debug.LogError("[GuardAI] HearingSystem reference is null. Assign HearingSystem on the same guard GameObject.");
                hasLoggedMissingHearingSystem = true;
            }

            return NodeState.Failure;
        }

        hearingSystem.ClearNoise();
        return NodeState.Success;
    }

    private NodeState EvaluatePatrol()
    {
        MarkLeafActive("Patrol");
        if (currentGuardState != GuardState.Patrolling && patrolSystem != null)
        {
            patrolSystem.ResetPatrol();
        }

        if (currentGuardState == GuardState.Chasing
            || currentGuardState == GuardState.Suspicious
            || currentGuardState == GuardState.Investigating
            || currentGuardState == GuardState.Searching)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh && !navMeshAgent.hasPath)
            {
                patrolSystem?.NotifyMovementReset();
            }
        }

        currentGuardState = GuardState.Patrolling;
        ResetSearchTimer();
        suspiciousTimerActive = false;
        SetAgentSpeedMultiplier(1f);

        if (patrolSystem != null)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                bool missingPath = !navMeshAgent.pathPending && !navMeshAgent.hasPath;
                if (missingPath)
                {
                    patrolNoPathTimer += Time.deltaTime;
                    if (patrolNoPathTimer >= 0.25f)
                    {
                        patrolSystem.ForceStartPatrol(navMeshAgent);
                        patrolNoPathTimer = 0f;
                    }
                }
                else
                {
                    patrolNoPathTimer = 0f;
                }
            }

            return patrolSystem.Patrol(navMeshAgent);
        }

        return NodeState.Running;
    }

    private void MarkLeafActive(string leafName)
    {
        lastActiveLeafNodeName = leafName;
    }

    private void SetDestination(Vector3 destination)
    {
        if (navMeshAgent == null)
        {
            return;
        }

        if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(destination);
    }

    private void HandleInvestigateSequenceCompletion()
    {
        if (investigateSequenceNode == null)
        {
            return;
        }

        NodeState currentState = investigateSequenceNode.CurrentState;
        if (previousInvestigateSequenceState == NodeState.Running
            && (currentState == NodeState.Success || currentState == NodeState.Failure))
        {
            ResetInvestigationMemory();
            ResetSearchTimer();

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }
        }

        previousInvestigateSequenceState = currentState;
    }

    private void ResetInvestigationMemory()
    {
        hasLastKnownPosition = false;
        visionSystem?.ClearLastKnownPosition();
    }

    private void ResetSearchTimer()
    {
        searchTimer = 0f;
        searchTimerActive = false;
        searchCenterPosition = Vector3.zero;
        searchAngleDegrees = 0f;
    }

    private void UpdateSearchCircleMovement()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        float radius = Mathf.Max(0.5f, searchCircleRadius);
        searchAngleDegrees = (searchAngleDegrees + Mathf.Max(1f, searchCircleAngularSpeed) * Time.deltaTime) % 360f;

        Vector3 radial = Quaternion.Euler(0f, searchAngleDegrees, 0f) * Vector3.forward;
        Vector3 desiredPoint = searchCenterPosition + radial * radius;

        bool shouldSetDestination =
            !navMeshAgent.hasPath
            || (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + 0.15f);

        if (!shouldSetDestination)
        {
            return;
        }

        if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit sampledHit, 1.5f, NavMesh.AllAreas))
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(sampledHit.position);
        }
    }

    private bool HasReachedDestination()
    {
        if (navMeshAgent == null)
        {
            return true;
        }

        if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (navMeshAgent.pathPending)
        {
            return false;
        }

        float threshold = Mathf.Max(navMeshAgent.stoppingDistance, destinationEpsilon);
        if (navMeshAgent.remainingDistance > threshold)
        {
            return false;
        }

        return !navMeshAgent.hasPath || navMeshAgent.velocity.sqrMagnitude <= 0.0001f;
    }

    private bool TryGetPlayerPosition(out Vector3 position)
    {
        position = default;

        if (visionSystem == null)
        {
            return false;
        }

        if (TryGetTransformMember(
            visionSystem,
            out Transform playerTransform,
            "PlayerTransform",
            "CurrentTarget",
            "Target",
            "Player"))
        {
            position = playerTransform.position;
            return true;
        }

        return TryGetVector3Member(
            visionSystem,
            out position,
            "PlayerPosition",
            "SuspicionFocusPosition",
            "VisibleTargetPosition",
            "LastSeenPosition",
            "LastKnownPosition");
    }

    private bool TryGetSuspicionFocusPosition(out Vector3 focusPosition)
    {
        focusPosition = default;

        if (visionSystem != null && visionSystem.HasSuspicionFocus)
        {
            focusPosition = visionSystem.SuspicionFocusPosition;
            return true;
        }

        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            focusPosition = playerPosition;
            return true;
        }

        if (hasLastKnownPosition)
        {
            focusPosition = lastKnownPosition;
            return true;
        }

        return false;
    }

    private void SetAgentSpeedMultiplier(float multiplier)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        float globalMultiplier = GuardAlertSystem.CurrentAlert == GuardAlertSystem.AlertLevel.FullAlert ? 1.2f : 1f;
        navMeshAgent.speed = defaultAgentSpeed * Mathf.Max(0f, multiplier) * globalMultiplier;
    }

    private void TryRecoverAgentOnNavMesh()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            hasLoggedNavMeshIssue = false;
            return;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            navMeshAgent.Warp(hit.position);
            hasLoggedNavMeshIssue = false;
            return;
        }

        if (!hasLoggedNavMeshIssue)
        {
            Debug.LogWarning($"[GuardAI] {name} is not on NavMesh and could not recover within 20 units.");
            hasLoggedNavMeshIssue = true;
        }
    }

    private static bool TryGetVector3Member(object instance, out Vector3 value, params string[] memberNames)
    {
        value = default;
        if (instance == null)
        {
            return false;
        }

        Type type = instance.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        for (int i = 0; i < memberNames.Length; i++)
        {
            PropertyInfo property = type.GetProperty(memberNames[i], flags);
            if (property != null && property.PropertyType == typeof(Vector3) && property.GetIndexParameters().Length == 0)
            {
                value = (Vector3)property.GetValue(instance);
                return true;
            }

            FieldInfo field = type.GetField(memberNames[i], flags);
            if (field != null && field.FieldType == typeof(Vector3))
            {
                value = (Vector3)field.GetValue(instance);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTransformMember(object instance, out Transform value, params string[] memberNames)
    {
        value = null;
        if (instance == null)
        {
            return false;
        }

        Type type = instance.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        for (int i = 0; i < memberNames.Length; i++)
        {
            PropertyInfo property = type.GetProperty(memberNames[i], flags);
            if (property != null && typeof(Transform).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(instance) as Transform;
                if (value != null)
                {
                    return true;
                }
            }

            FieldInfo field = type.GetField(memberNames[i], flags);
            if (field != null && typeof(Transform).IsAssignableFrom(field.FieldType))
            {
                value = field.GetValue(instance) as Transform;
                if (value != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Condition leaf node backed by an inline delegate.
    /// </summary>
    private sealed class LambdaConditionNode : ConditionNode
    {
        private readonly Func<NodeState> evaluator;

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaConditionNode"/> class.
        /// </summary>
        /// <param name="nodeName">The node name used in debug output.</param>
        /// <param name="evaluator">The delegate that evaluates this condition.</param>
        public LambdaConditionNode(string nodeName, Func<NodeState> evaluator) : base(nodeName)
        {
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Evaluates this condition for the current behavior tree tick.
        /// </summary>
        /// <returns>The result of this condition evaluation.</returns>
        public override NodeState Evaluate()
        {
            CurrentState = evaluator();
            return CurrentState;
        }
    }

    /// <summary>
    /// Action leaf node backed by an inline delegate.
    /// </summary>
    private sealed class LambdaActionNode : ActionNode
    {
        private readonly Func<NodeState> evaluator;

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaActionNode"/> class.
        /// </summary>
        /// <param name="nodeName">The node name used in debug output.</param>
        /// <param name="evaluator">The delegate that evaluates this action.</param>
        public LambdaActionNode(string nodeName, Func<NodeState> evaluator) : base(nodeName)
        {
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Evaluates this action for the current behavior tree tick.
        /// </summary>
        /// <returns>The result of this action evaluation.</returns>
        public override NodeState Evaluate()
        {
            CurrentState = evaluator();
            return CurrentState;
        }
    }
}
