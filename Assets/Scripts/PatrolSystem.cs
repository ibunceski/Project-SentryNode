using StealthAI.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles dynamic random wandering patrol behavior for a guard.
/// </summary>
public class PatrolSystem : MonoBehaviour
{
    [SerializeField]
    private float minWanderRadius = 4f;

    [SerializeField]
    private float maxWanderRadius = 14f;

    [SerializeField]
    private float minStepDistance = 6f;

    [SerializeField]
    private float waypointTolerance = 0.5f;

    [SerializeField]
    private float minWaitDuration = 0.5f;

    [SerializeField]
    private float maxWaitDuration = 1.5f;

    [SerializeField]
    private int maxSampleAttempts = 12;

    [SerializeField]
    private float navMeshSampleRange = 1.5f;

    private bool hasWanderTarget;
    private Vector3 currentWanderTarget;
    private float waitEndTime = -1f;

    /// <summary>
    /// Gets the current patrol target point in world space.
    /// </summary>
    public Vector3 CurrentPatrolPoint
    {
        get => hasWanderTarget ? currentWanderTarget : transform.position;
    }

    /// <summary>
    /// Ticks patrol logic for the provided agent.
    /// </summary>
    /// <param name="agent">The guard's existing NavMeshAgent.</param>
    /// <returns>Always <see cref="NodeState.Running"/> because patrol is a continuous fallback behavior.</returns>
    public NodeState Patrol(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return NodeState.Running;
        }

        if (IsWaiting())
        {
            agent.isStopped = true;
            return NodeState.Running;
        }

        if (waitEndTime >= 0f)
        {
            waitEndTime = -1f;
            ClearCurrentTarget();
        }

        if (!hasWanderTarget)
        {
            hasWanderTarget = TryPickRandomWanderTarget(agent, out currentWanderTarget);
            if (!hasWanderTarget)
            {
                agent.isStopped = false;
                return NodeState.Running;
            }
        }

        agent.isStopped = false;
        agent.SetDestination(currentWanderTarget);

        if (HasReachedWaypoint(agent, currentWanderTarget))
        {
            waitEndTime = Time.time + GetRandomWaitDuration();
            agent.isStopped = true;
        }

        return NodeState.Running;
    }

    /// <summary>
    /// Clears current patrol target so a fresh point is chosen immediately.
    /// </summary>
    public void ResetPatrol()
    {
        waitEndTime = -1f;
        ClearCurrentTarget();
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

    private bool TryPickRandomWanderTarget(NavMeshAgent agent, out Vector3 target)
    {
        target = default;

        float clampedMin = Mathf.Max(0f, minWanderRadius);
        float clampedMax = Mathf.Max(clampedMin, maxWanderRadius);
        float clampedMinStep = Mathf.Clamp(minStepDistance, 0f, clampedMax);
        int attempts = Mathf.Max(1, maxSampleAttempts);
        float sampleRange = Mathf.Max(0.25f, navMeshSampleRange);

        for (int i = 0; i < attempts; i++)
        {
            // Bias toward farther radii so patrol covers more of the area.
            float radiusT = Mathf.Pow(Random.value, 0.35f);
            float radius = Mathf.Lerp(clampedMin, clampedMax, radiusT);
            Vector2 randomCircle = Random.insideUnitCircle.normalized * radius;
            if (randomCircle.sqrMagnitude <= 0.0001f)
            {
                randomCircle = Vector2.right * radius;
            }

            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRange, NavMesh.AllAreas))
            {
                float stepDistance = Vector3.Distance(transform.position, hit.position);
                if (stepDistance < clampedMinStep)
                {
                    continue;
                }

                target = hit.position;
                return true;
            }
        }

        // Relax only distance constraint first (still respects sample range) to avoid stalling.
        for (int i = 0; i < attempts; i++)
        {
            float radius = Random.Range(clampedMin, clampedMax);
            Vector2 randomCircle = Random.insideUnitCircle.normalized * radius;
            if (randomCircle.sqrMagnitude <= 0.0001f)
            {
                randomCircle = Vector2.right * radius;
            }

            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRange, NavMesh.AllAreas))
            {
                target = hit.position;
                return true;
            }
        }

        // Fallback sample near the guard so patrol can recover in sparse navmesh areas.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fallbackHit, Mathf.Max(1f, agent.radius * 2f), NavMesh.AllAreas))
        {
            target = fallbackHit.position;
            return true;
        }

        return false;
    }

    private float GetRandomWaitDuration()
    {
        float minWait = Mathf.Max(0f, minWaitDuration);
        float maxWait = Mathf.Max(minWait, maxWaitDuration);
        return Random.Range(minWait, maxWait);
    }

    private bool IsWaiting()
    {
        return waitEndTime >= 0f && Time.time < waitEndTime;
    }

    private void ClearCurrentTarget()
    {
        hasWanderTarget = false;
        currentWanderTarget = default;
    }

    private void OnDrawGizmos()
    {
        if (hasWanderTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentWanderTarget);
        }
    }
}
