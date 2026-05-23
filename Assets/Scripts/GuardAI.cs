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

    [Header("Runtime Debug")]
    [SerializeField]
    private string lastActiveLeafNodeName = "None";

    [SerializeField]
    private GuardState currentGuardState = GuardState.Patrolling;

    private Node root;
    private Vector3 lastKnownPosition;
    private bool hasLastKnownPosition;
    private Vector3 noiseSourcePosition;
    private bool hasNoiseSource;
    private float searchEndTime = -1f;
    private float defaultAgentSpeed = 3.5f;

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
        if (navMeshAgent != null)
        {
            defaultAgentSpeed = navMeshAgent.speed;
        }

        root = BuildTree();
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

        root.Evaluate();
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

        Sequence investigateSequence = new Sequence("Investigate Sequence");
        investigateSequence.AddChild(new LambdaConditionNode("HasLastKnownPosition", EvaluateHasLastKnownPosition));
        investigateSequence.AddChild(new LambdaActionNode("MoveToLastKnownPosition", EvaluateMoveToLastKnownPosition));
        investigateSequence.AddChild(new LambdaActionNode("SearchArea", EvaluateSearchArea));

        Sequence noiseSequence = new Sequence("Noise Sequence");
        noiseSequence.AddChild(new LambdaConditionNode("HeardNoise", EvaluateHeardNoise));
        noiseSequence.AddChild(new LambdaActionNode("MoveToNoiseSource", EvaluateMoveToNoiseSource));
        noiseSequence.AddChild(new LambdaActionNode("ClearNoise", EvaluateClearNoise));

        LambdaActionNode patrolNode = new LambdaActionNode("Patrol", EvaluatePatrol);

        rootSelector.AddChild(chaseSequence);
        rootSelector.AddChild(suspiciousSequence);
        rootSelector.AddChild(investigateSequence);
        rootSelector.AddChild(noiseSequence);
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
        searchEndTime = -1f;
        SetAgentSpeedMultiplier(0.6f);
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
        }

        if (!TryGetSuspicionFocusPosition(out Vector3 focusPosition))
        {
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
        searchEndTime = -1f;
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

        SetDestination(lastKnownPosition);

        if (HasReachedDestination())
        {
            searchEndTime = -1f;
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState EvaluateSearchArea()
    {
        MarkLeafActive("SearchArea");
        currentGuardState = GuardState.Searching;
        SetAgentSpeedMultiplier(1f);

        if (searchEndTime < 0f)
        {
            searchEndTime = Time.time + Mathf.Max(0.1f, searchDurationSeconds);
        }

        if (Time.time < searchEndTime)
        {
            return NodeState.Running;
        }

        searchEndTime = -1f;
        hasLastKnownPosition = false;
        visionSystem?.ClearLastKnownPosition();
        return NodeState.Success;
    }

    private NodeState EvaluateHeardNoise()
    {
        MarkLeafActive("HeardNoise");

        bool heardNoise = ReadBoolMember(
            hearingSystem,
            "HeardNoise",
            "HasHeardNoise",
            "HasPendingNoise");

        if (!heardNoise)
        {
            return NodeState.Failure;
        }

        if (TryGetVector3Member(
            hearingSystem,
            out Vector3 sensedNoisePosition,
            "NoiseSourcePosition",
            "LastHeardPosition",
            "LastNoisePosition"))
        {
            noiseSourcePosition = sensedNoisePosition;
            hasNoiseSource = true;
        }
        else if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            noiseSourcePosition = playerPosition;
            hasNoiseSource = true;
        }

        return hasNoiseSource ? NodeState.Success : NodeState.Failure;
    }

    private NodeState EvaluateMoveToNoiseSource()
    {
        MarkLeafActive("MoveToNoiseSource");
        currentGuardState = GuardState.Investigating;
        SetAgentSpeedMultiplier(1f);

        if (!hasNoiseSource)
        {
            return NodeState.Failure;
        }

        SetDestination(noiseSourcePosition);

        if (HasReachedDestination())
        {
            lastKnownPosition = noiseSourcePosition;
            hasLastKnownPosition = true;
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState EvaluateClearNoise()
    {
        MarkLeafActive("ClearNoise");

        InvokeFirstMethod(hearingSystem, "ClearNoise", "ResetNoise", "ConsumeNoise");
        hasNoiseSource = false;
        return NodeState.Success;
    }

    private NodeState EvaluatePatrol()
    {
        MarkLeafActive("Patrol");
        if (currentGuardState == GuardState.Chasing
            || currentGuardState == GuardState.Suspicious
            || currentGuardState == GuardState.Investigating
            || currentGuardState == GuardState.Searching)
        {
            patrolSystem?.ResetPatrol();
        }

        currentGuardState = GuardState.Patrolling;
        searchEndTime = -1f;
        SetAgentSpeedMultiplier(1f);

        if (patrolSystem != null)
        {
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

    private static bool ReadBoolMember(object instance, params string[] memberNames)
    {
        if (instance == null)
        {
            return false;
        }

        Type type = instance.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        for (int i = 0; i < memberNames.Length; i++)
        {
            PropertyInfo property = type.GetProperty(memberNames[i], flags);
            if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
            {
                return (bool)property.GetValue(instance);
            }

            MethodInfo method = type.GetMethod(memberNames[i], flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool))
            {
                return (bool)method.Invoke(instance, null);
            }
        }

        return false;
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

    private static void InvokeFirstMethod(object instance, params string[] methodNames)
    {
        if (instance == null)
        {
            return;
        }

        Type type = instance.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = type.GetMethod(methodNames[i], flags, null, Type.EmptyTypes, null);
            if (method == null)
            {
                continue;
            }

            method.Invoke(instance, null);
            return;
        }
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
