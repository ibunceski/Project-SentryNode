using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives local area search movement by generating random NavMesh points around a position.
/// </summary>
public class SearchSystem : MonoBehaviour
{
    [SerializeField]
    private float searchRadius = 4f;

    [SerializeField]
    private float pointTolerance = 0.6f;

    private NavMeshAgent agent;
    private readonly Vector3[] searchPoints = new Vector3[3];
    private int currentSearchIndex;
    private bool initialized;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// Initializes a new search pass centered around the provided last known position.
    /// </summary>
    public void BeginSearch(Vector3 lastKnownPosition)
    {
        currentSearchIndex = 0;
        initialized = true;

        int generated = 0;
        int attempts = 0;
        while (generated < searchPoints.Length && attempts < 20)
        {
            attempts++;
            Vector2 randomOffset = Random.insideUnitCircle * searchRadius;
            Vector3 candidate = lastKnownPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                searchPoints[generated] = hit.position;
                generated++;
            }
        }

        for (int i = generated; i < searchPoints.Length; i++)
        {
            searchPoints[i] = lastKnownPosition;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(searchPoints[0]);
        }
    }

    /// <summary>
    /// Advances search movement and returns true when all generated points are visited.
    /// </summary>
    public bool Tick()
    {
        if (!initialized || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        if (!agent.pathPending && agent.remainingDistance < pointTolerance)
        {
            currentSearchIndex++;
            if (currentSearchIndex >= searchPoints.Length)
            {
                initialized = false;
                return true;
            }

            agent.SetDestination(searchPoints[currentSearchIndex]);
        }

        return false;
    }

    /// <summary>
    /// Clears current search state.
    /// </summary>
    public void Reset()
    {
        initialized = false;
        currentSearchIndex = 0;
    }

    private void OnDrawGizmos()
    {
        if (!initialized)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < searchPoints.Length; i++)
        {
            Gizmos.DrawSphere(searchPoints[i], 0.25f);
            if (i < searchPoints.Length - 1)
            {
                Gizmos.DrawLine(searchPoints[i], searchPoints[i + 1]);
            }
        }

        if (currentSearchIndex < searchPoints.Length)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(searchPoints[currentSearchIndex], 0.4f);
        }
    }
}
