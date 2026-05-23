using UnityEngine;
using UnityEngine.AI;
using StealthAI.BehaviorTree;

/// <summary>
/// Handles waypoint-based patrol routing for a guard.
/// </summary>
public class PatrolSystem : MonoBehaviour
{
    [SerializeField]
    private Transform[] patrolPoints;

    [SerializeField]
    private float waypointTolerance = 0.5f;

    [SerializeField]
    private float waitAtWaypointDuration = 1f;

    private int currentPatrolIndex;
    private float waitTimer;
    private float noPathTimer;
    private bool patrolInitialized;
    private int patrolStep = 1;
    private int startPhaseOffset;

    /// <summary>
    /// Moves the agent along patrol points in a loop and returns <see cref="NodeState.Running"/>.
    /// </summary>
    /// <param name="agent">Agent to move along the patrol route.</param>
    /// <returns>Always returns <see cref="NodeState.Running"/>.</returns>
    public NodeState Patrol(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return NodeState.Running;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return NodeState.Running;
        }

        if (!patrolInitialized)
        {
            ForceStartPatrol(agent);
            return NodeState.Running;
        }

        Transform currentTarget = GetCurrentTargetPoint();
        if (currentTarget == null)
        {
            return NodeState.Running;
        }

        float tolerance = Mathf.Max(0.05f, waypointTolerance);
        bool reachedWaypoint = HasReachedCurrentWaypoint(agent, currentTarget.position, tolerance);

        if (!reachedWaypoint)
        {
            waitTimer = 0f;
            EnsureDestinationIsCurrentTarget(agent, currentTarget.position);

            if (!agent.pathPending && !agent.hasPath)
            {
                noPathTimer += Time.deltaTime;
                if (noPathTimer >= 0.6f)
                {
                    noPathTimer = 0f;
                    currentPatrolIndex = WrapIndex(currentPatrolIndex + patrolStep);
                    SetCurrentDestination(agent);
                }
            }
            else
            {
                noPathTimer = 0f;
            }

            return NodeState.Running;
        }

        noPathTimer = 0f;
        waitTimer += Time.deltaTime;
        if (waitTimer < Mathf.Max(0f, waitAtWaypointDuration))
        {
            return NodeState.Running;
        }

        waitTimer = 0f;
        currentPatrolIndex = WrapIndex(currentPatrolIndex + patrolStep);
        SetCurrentDestination(agent);
        return NodeState.Running;
    }

    /// <summary>
    /// Forces immediate patrol initialization and sets the first waypoint destination.
    /// </summary>
    /// <param name="agent">Agent to initialize for patrol.</param>
    public void ForceStartPatrol(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        InitializePatrolIndexFromNearest(transform.position);

        if (startPhaseOffset != 0)
        {
            currentPatrolIndex = WrapIndex(currentPatrolIndex + startPhaseOffset);
            startPhaseOffset = 0;
        }

        waitTimer = 0f;
        noPathTimer = 0f;
        patrolInitialized = true;
        SetCurrentDestination(agent);
    }

    /// <summary>
    /// Resets patrol progress and resumes from the nearest patrol point.
    /// </summary>
    public void ResetPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolInitialized = false;
            waitTimer = 0f;
            return;
        }

        InitializePatrolIndexFromNearest(transform.position);
        waitTimer = 0f;
        noPathTimer = 0f;
        patrolInitialized = false;
    }

    /// <summary>
    /// Sets patrol traversal direction and optional initial phase offset.
    /// </summary>
    /// <param name="clockwise"><see langword="true"/> to move forward through indices; otherwise reverse.</param>
    /// <param name="phaseOffset">Initial index offset applied once on first patrol tick.</param>
    public void SetPatrolPattern(bool clockwise, int phaseOffset)
    {
        patrolStep = clockwise ? 1 : -1;
        startPhaseOffset = phaseOffset;
        patrolInitialized = false;
        waitTimer = 0f;
        noPathTimer = 0f;
    }

    /// <summary>
    /// Marks patrol movement as needing reinitialization after path interruption.
    /// </summary>
    public void NotifyMovementReset()
    {
        patrolInitialized = false;
        waitTimer = 0f;
        noPathTimer = 0f;
    }

    private void InitializePatrolIndexFromNearest(Vector3 origin)
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(origin, patrolPoints[i].position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        currentPatrolIndex = nearestIndex;
    }

    private int WrapIndex(int index)
    {
        int count = patrolPoints.Length;
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = index % count;
        if (wrapped < 0)
        {
            wrapped += count;
        }

        return wrapped;
    }

    private void SetCurrentDestination(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Transform target = GetCurrentTargetPoint();
        if (target == null)
        {
            return;
        }

        EnsureDestinationIsCurrentTarget(agent, target.position);
    }

    private void EnsureDestinationIsCurrentTarget(NavMeshAgent agent, Vector3 rawTargetPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 destination = rawTargetPosition;
        if (NavMesh.SamplePosition(rawTargetPosition, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            destination = hit.position;
        }

        bool needsDestinationUpdate = !agent.hasPath || Vector3.Distance(agent.destination, destination) > 0.15f;
        if (needsDestinationUpdate)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    private bool HasReachedCurrentWaypoint(NavMeshAgent agent, Vector3 targetPosition, float tolerance)
    {
        if (agent == null)
        {
            return true;
        }

        if (agent.pathPending)
        {
            return false;
        }

        if (agent.hasPath)
        {
            return agent.remainingDistance <= tolerance;
        }

        return Vector3.Distance(transform.position, targetPosition) <= tolerance;
    }

    private Transform GetCurrentTargetPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return null;
        }

        int checkedCount = 0;
        while (checkedCount < patrolPoints.Length)
        {
            currentPatrolIndex = WrapIndex(currentPatrolIndex);
            Transform point = patrolPoints[currentPatrolIndex];
            if (point != null)
            {
                return point;
            }

            currentPatrolIndex = WrapIndex(currentPatrolIndex + patrolStep);
            checkedCount++;
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        DrawPatrolGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawPatrolGizmos();
    }

    private void DrawPatrolGizmos()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
            {
                continue;
            }

            Gizmos.DrawSphere(point.position, 0.15f);

            Transform next = patrolPoints[(i + 1) % patrolPoints.Length];
            if (next != null)
            {
                Gizmos.DrawLine(point.position, next.position);
            }
        }

        if (currentPatrolIndex < 0 || currentPatrolIndex >= patrolPoints.Length)
        {
            return;
        }

        Transform currentTarget = patrolPoints[currentPatrolIndex];
        if (currentTarget == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, currentTarget.position);
    }
}
