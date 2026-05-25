using StealthAI.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Wraps <see cref="VisionSystem"/> behind <see cref="IGuardVisionService"/>.
/// </summary>
public sealed class GuardVisionServiceAdapter : IGuardVisionService
{
    private readonly VisionSystem visionSystem;

    public GuardVisionServiceAdapter(VisionSystem visionSystem)
    {
        this.visionSystem = visionSystem;
    }

    public float Suspicion => visionSystem != null ? visionSystem.Suspicion : 0f;

    public VisionSystem.SuspicionLevel CurrentSuspicionLevel => visionSystem != null
        ? visionSystem.CurrentSuspicionLevel
        : VisionSystem.SuspicionLevel.Unaware;

    public bool HasLastKnownPosition => visionSystem != null && visionSystem.HasLastKnownPosition;

    public Vector3 LastKnownPosition => visionSystem != null ? visionSystem.LastKnownPosition : Vector3.zero;

    public Transform PlayerTransform => visionSystem != null ? visionSystem.PlayerTransform : null;

    public bool HasSuspicionFocus => visionSystem != null && visionSystem.HasSuspicionFocus;

    public Vector3 SuspicionFocusPosition => visionSystem != null ? visionSystem.SuspicionFocusPosition : Vector3.zero;

    public bool TryGetPlayerPosition(out Vector3 playerPosition)
    {
        playerPosition = Vector3.zero;
        if (visionSystem == null)
        {
            return false;
        }

        if (visionSystem.PlayerTransform != null)
        {
            playerPosition = visionSystem.PlayerTransform.position;
            return true;
        }

        // Preserve legacy behavior where PlayerPosition was always available
        // through reflection-based access in GuardAI.
        playerPosition = visionSystem.PlayerPosition;
        return true;
    }

    public void ClearLastKnownPosition()
    {
        visionSystem?.ClearLastKnownPosition();
    }
}

/// <summary>
/// Wraps <see cref="HearingSystem"/> behind <see cref="IGuardHearingService"/>.
/// </summary>
public sealed class GuardHearingServiceAdapter : IGuardHearingService
{
    private readonly HearingSystem hearingSystem;

    public GuardHearingServiceAdapter(HearingSystem hearingSystem)
    {
        this.hearingSystem = hearingSystem;
    }

    public bool HasHeardNoise => hearingSystem != null && hearingSystem.HeardNoise;

    public bool TryGetNoiseSourcePosition(out Vector3 noisePosition)
    {
        noisePosition = Vector3.zero;
        if (hearingSystem == null || !hearingSystem.HeardNoise)
        {
            return false;
        }

        noisePosition = hearingSystem.NoiseSourcePosition;
        return true;
    }

    public void ClearNoise()
    {
        hearingSystem?.ClearNoise();
    }
}

/// <summary>
/// Wraps <see cref="PatrolSystem"/> behind <see cref="IGuardPatrolService"/>.
/// </summary>
public sealed class GuardPatrolServiceAdapter : IGuardPatrolService
{
    private readonly PatrolSystem patrolSystem;
    private readonly NavMeshAgent navMeshAgent;

    public GuardPatrolServiceAdapter(PatrolSystem patrolSystem, NavMeshAgent navMeshAgent)
    {
        this.patrolSystem = patrolSystem;
        this.navMeshAgent = navMeshAgent;
    }

    public NodeState TickPatrol()
    {
        if (patrolSystem == null)
        {
            return NodeState.Running;
        }

        return patrolSystem.Patrol(navMeshAgent);
    }

    public void ResetPatrol()
    {
        patrolSystem?.ResetPatrol();
    }
}

/// <summary>
/// Wraps <see cref="SearchSystem"/> behind <see cref="IGuardSearchService"/>.
/// </summary>
public sealed class GuardSearchServiceAdapter : IGuardSearchService
{
    private readonly SearchSystem searchSystem;

    public GuardSearchServiceAdapter(SearchSystem searchSystem)
    {
        this.searchSystem = searchSystem;
    }

    public void BeginSearch(Vector3 lastKnownPosition)
    {
        searchSystem?.BeginSearch(lastKnownPosition);
    }

    public bool Tick()
    {
        return searchSystem != null && searchSystem.Tick();
    }

    public void Reset()
    {
        searchSystem?.Reset();
    }
}

/// <summary>
/// Wraps static <see cref="GuardAlertSystem"/> behind <see cref="IGuardAlertService"/>.
/// </summary>
public sealed class GuardAlertServiceAdapter : IGuardAlertService
{
    public GuardAlertSystem.AlertLevel CurrentAlert => GuardAlertSystem.CurrentAlert;

    public Vector3 SharedLastKnownPosition => GuardAlertSystem.SharedLastKnownPosition;

    public void Tick()
    {
        GuardAlertSystem.Tick();
    }

    public void ReportPlayerSeen(Vector3 position)
    {
        GuardAlertSystem.ReportPlayerSeen(position);
    }
}

/// <summary>
/// Wraps <see cref="NavMeshAgent"/> behind <see cref="IGuardNavigationService"/>.
/// </summary>
public sealed class GuardNavigationServiceAdapter : IGuardNavigationService
{
    private readonly NavMeshAgent agent;

    public GuardNavigationServiceAdapter(NavMeshAgent agent)
    {
        this.agent = agent;
    }

    public bool IsAvailable => agent != null && agent.enabled && agent.isOnNavMesh;

    public float AngularSpeed => agent != null ? agent.angularSpeed : 360f;

    public float StoppingDistance => agent != null ? agent.stoppingDistance : 0f;

    public float Radius => agent != null ? agent.radius : 0f;

    public bool PathPending => agent != null && agent.pathPending;

    public float RemainingDistance => agent != null ? agent.remainingDistance : 0f;

    public bool HasPath => agent != null && agent.hasPath;

    public float VelocitySqrMagnitude => agent != null ? agent.velocity.sqrMagnitude : 0f;

    public void SetSpeed(float speed)
    {
        if (agent == null || !agent.enabled)
        {
            return;
        }

        agent.speed = speed;
    }

    public void SetStopped(bool stopped)
    {
        if (IsAvailable)
        {
            agent.isStopped = stopped;
        }
    }

    public void SetDestination(Vector3 destination)
    {
        if (IsAvailable)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    public void ResetPath()
    {
        if (IsAvailable)
        {
            agent.ResetPath();
        }
    }
}

/// <summary>
/// Resolves player position using the player tag.
/// </summary>
public sealed class TagPlayerLocator : IPlayerLocator
{
    private readonly string playerTag;

    public TagPlayerLocator(string playerTag)
    {
        this.playerTag = string.IsNullOrWhiteSpace(playerTag) ? "Player" : playerTag;
    }

    public bool TryGetPlayerPosition(out Vector3 playerPosition)
    {
        playerPosition = Vector3.zero;

        GameObject playerObject = GameObject.FindWithTag(playerTag);
        if (playerObject == null)
        {
            return false;
        }

        playerPosition = playerObject.transform.position;
        return true;
    }
}
