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

    [SerializeField]
    private int searchPointCount = 6;

    [SerializeField]
    private float minPointSpacing = 1.2f;

    private NavMeshAgent agent;
    private Vector3[] searchPoints;
    private int currentSearchIndex;
    private bool initialized;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        int pointCount = Mathf.Max(3, searchPointCount);
        searchPoints = new Vector3[pointCount];
    }

    /// <summary>
    /// Initializes a new search pass centered around the provided last known position.
    /// </summary>
    public void BeginSearch(Vector3 lastKnownPosition)
    {
        currentSearchIndex = 0;
        initialized = true;

        if (searchPoints == null || searchPoints.Length != Mathf.Max(3, searchPointCount))
        {
            searchPoints = new Vector3[Mathf.Max(3, searchPointCount)];
        }

        float outerRadius = Mathf.Max(0.5f, searchRadius);
        float innerRadius = Mathf.Max(0.25f, outerRadius * 0.45f);
        float baseAngle = Random.Range(0f, 360f);
        int generated = 0;
        int maxAttemptsPerPoint = 12;
        Vector3 referencePoint = agent != null ? agent.transform.position : lastKnownPosition;

        for (int i = 0; i < searchPoints.Length; i++)
        {
            bool found = false;

            for (int attempt = 0; attempt < maxAttemptsPerPoint && !found; attempt++)
            {
                float sweepAngle = baseAngle + (360f / searchPoints.Length) * i + Random.Range(-18f, 18f);
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                Vector3 radialOffset = Quaternion.Euler(0f, sweepAngle, 0f) * Vector3.forward * radius;
                Vector3 candidate = lastKnownPosition + new Vector3(radialOffset.x, 0f, radialOffset.z);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    Vector2 randomOffset = Random.insideUnitCircle * outerRadius;
                    Vector3 randomCandidate = lastKnownPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
                    if (!NavMesh.SamplePosition(randomCandidate, out hit, 2.5f, NavMesh.AllAreas))
                    {
                        continue;
                    }
                }

                float minSpacing = Mathf.Max(pointTolerance * 2f, minPointSpacing);
                if (Vector3.Distance(hit.position, referencePoint) < minSpacing)
                {
                    continue;
                }

                searchPoints[generated] = hit.position;
                referencePoint = hit.position;
                generated++;
                found = true;
            }
        }

        for (int i = generated; i < searchPoints.Length; i++)
        {
            float fallbackAngle = baseAngle + (360f / searchPoints.Length) * i;
            Vector3 fallbackOffset = Quaternion.Euler(0f, fallbackAngle, 0f) * Vector3.forward * outerRadius;
            Vector3 fallbackCandidate = lastKnownPosition + new Vector3(fallbackOffset.x, 0f, fallbackOffset.z);
            if (NavMesh.SamplePosition(fallbackCandidate, out NavMeshHit fallbackHit, 2.5f, NavMesh.AllAreas))
            {
                searchPoints[i] = fallbackHit.position;
            }
            else
            {
                searchPoints[i] = lastKnownPosition;
            }
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && searchPoints.Length > 0)
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
