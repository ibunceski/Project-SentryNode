using StealthAI.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles waypoint-based patrol behavior for a guard.
/// </summary>
public class PatrolSystem : MonoBehaviour
{
    [SerializeField]
    private Transform[] patrolPoints;

    [SerializeField]
    private float waypointTolerance = 0.5f;

    [SerializeField]
    private float waitAtWaypointDuration = 1f;

    [SerializeField]
    private int currentWaypointIndex;

    private float waitEndTime = -1f;
    private bool pendingAdvanceAfterWait;

    /// <summary>
    /// Gets the current patrol target point in world space.
    /// </summary>
    public Vector3 CurrentPatrolPoint
    {
        get
        {
            if (!HasPatrolPoints())
            {
                return transform.position;
            }

            return patrolPoints[currentWaypointIndex].position;
        }
    }

    /// <summary>
    /// Ticks patrol logic for the provided agent.
    /// </summary>
    /// <param name="agent">The guard's existing NavMeshAgent.</param>
    /// <returns>Always <see cref="NodeState.Running"/> because patrol is a continuous fallback behavior.</returns>
    public NodeState Patrol(NavMeshAgent agent)
    {
        if (!HasPatrolPoints())
        {
            return NodeState.Running;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return NodeState.Running;
        }

        Vector3 target = patrolPoints[currentWaypointIndex].position;
        bool currentlyWaiting = waitEndTime > Time.time;
        if (currentlyWaiting)
        {
            agent.isStopped = true;
            return NodeState.Running;
        }

        if (pendingAdvanceAfterWait)
        {
            pendingAdvanceAfterWait = false;
            AdvanceWaypoint();
            target = patrolPoints[currentWaypointIndex].position;
        }

        agent.isStopped = false;
        agent.SetDestination(target);

        if (HasReachedWaypoint(agent, target))
        {
            waitEndTime = Time.time + Mathf.Max(0f, waitAtWaypointDuration);
            pendingAdvanceAfterWait = true;
        }

        return NodeState.Running;
    }

    /// <summary>
    /// Resets patrol to the nearest patrol point from the guard's current position.
    /// </summary>
    public void ResetPatrol()
    {
        if (!HasPatrolPoints())
        {
            return;
        }

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;
        Vector3 guardPosition = transform.position;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
            {
                continue;
            }

            float distance = Vector3.Distance(guardPosition, point.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        currentWaypointIndex = nearestIndex;
        waitEndTime = -1f;
        pendingAdvanceAfterWait = false;
    }

    private bool HasPatrolPoints()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasReachedWaypoint(NavMeshAgent agent, Vector3 waypoint)
    {
        if (agent.pathPending)
        {
            return false;
        }

        float effectiveTolerance = Mathf.Max(0.01f, waypointTolerance);
        float toWaypoint = Vector3.Distance(agent.transform.position, waypoint);
        if (toWaypoint > effectiveTolerance)
        {
            return false;
        }

        return !agent.hasPath || agent.velocity.sqrMagnitude <= 0.0001f || agent.remainingDistance <= effectiveTolerance;
    }

    private void AdvanceWaypoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        int nextIndex = currentWaypointIndex;
        int attempts = patrolPoints.Length;

        for (int i = 0; i < attempts; i++)
        {
            nextIndex = (nextIndex + 1) % patrolPoints.Length;
            if (patrolPoints[nextIndex] != null)
            {
                currentWaypointIndex = nextIndex;
                return;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!HasPatrolPoints())
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

            Transform nextPoint = FindNextValidPoint(i);
            if (nextPoint != null)
            {
                Gizmos.DrawLine(point.position, nextPoint.position);
            }
        }

        Transform currentTarget = GetCurrentTargetTransform();
        if (currentTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }

    private Transform GetCurrentTargetTransform()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return null;
        }

        if (currentWaypointIndex < 0 || currentWaypointIndex >= patrolPoints.Length)
        {
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, patrolPoints.Length - 1);
        }

        if (patrolPoints[currentWaypointIndex] != null)
        {
            return patrolPoints[currentWaypointIndex];
        }

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] != null)
            {
                currentWaypointIndex = i;
                return patrolPoints[i];
            }
        }

        return null;
    }

    private Transform FindNextValidPoint(int fromIndex)
    {
        if (patrolPoints == null || patrolPoints.Length <= 1)
        {
            return null;
        }

        for (int offset = 1; offset <= patrolPoints.Length; offset++)
        {
            int index = (fromIndex + offset) % patrolPoints.Length;
            Transform point = patrolPoints[index];
            if (point != null)
            {
                return point;
            }
        }

        return null;
    }
}
