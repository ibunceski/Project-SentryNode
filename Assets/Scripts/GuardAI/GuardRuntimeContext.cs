using UnityEngine;

/// <summary>
/// Holds guard runtime state and shared operations used by behavior nodes.
/// </summary>
public sealed class GuardRuntimeContext
{
    private readonly Transform guardTransform;

    public GuardRuntimeContext(
        Transform guardTransform,
        IGuardNavigationService navigation,
        IGuardVisionService vision,
        IGuardHearingService hearing,
        IGuardPatrolService patrol,
        IGuardSearchService search,
        IGuardAlertService alert,
        IPlayerLocator playerLocator,
        float destinationEpsilon,
        float defaultAgentSpeed)
    {
        this.guardTransform = guardTransform;
        Navigation = navigation;
        Vision = vision;
        Hearing = hearing;
        Patrol = patrol;
        Search = search;
        Alert = alert;
        PlayerLocator = playerLocator;
        DestinationEpsilon = Mathf.Max(0.01f, destinationEpsilon);
        DefaultAgentSpeed = Mathf.Max(0f, defaultAgentSpeed);
        CurrentState = GuardAI.GuardState.Patrolling;
        ActiveLeafNodeName = "None";
    }

    public IGuardNavigationService Navigation { get; }

    public IGuardVisionService Vision { get; }

    public IGuardHearingService Hearing { get; }

    public IGuardPatrolService Patrol { get; }

    public IGuardSearchService Search { get; }

    public IGuardAlertService Alert { get; }

    public IPlayerLocator PlayerLocator { get; }

    public GuardAI.GuardState CurrentState { get; private set; }

    public string ActiveLeafNodeName { get; private set; }

    public Vector3 LastKnownPosition { get; private set; }

    public bool HasLastKnownPosition { get; private set; }

    public Vector3 NoiseSourcePosition { get; private set; }

    public bool HasNoiseSource { get; private set; }

    public float SearchTimer { get; private set; }

    public bool SearchStarted { get; private set; }

    public float DestinationEpsilon { get; }

    public float DefaultAgentSpeed { get; }

    public Transform Transform => guardTransform;

    public void MarkLeafActive(string leafName)
    {
        ActiveLeafNodeName = string.IsNullOrWhiteSpace(leafName) ? "Unnamed" : leafName;
    }

    public void SetState(GuardAI.GuardState state)
    {
        CurrentState = state;
    }

    public void SetLastKnownPosition(Vector3 position)
    {
        LastKnownPosition = position;
        HasLastKnownPosition = true;
    }

    public void ClearLastKnownPositionMemory()
    {
        HasLastKnownPosition = false;
    }

    public void SetNoiseSource(Vector3 position)
    {
        NoiseSourcePosition = position;
        HasNoiseSource = true;
    }

    public void ClearNoiseSource()
    {
        HasNoiseSource = false;
    }

    public void StartSearch()
    {
        SearchTimer = 0f;
        SearchStarted = true;
        Search.BeginSearch(LastKnownPosition);
    }

    public void ResetSearchState()
    {
        SearchStarted = false;
        SearchTimer = 0f;
    }

    public void AddSearchTime(float deltaTime)
    {
        SearchTimer += Mathf.Max(0f, deltaTime);
    }

    public void SetAgentSpeedMultiplier(float multiplier)
    {
        float clampedMultiplier = Mathf.Max(0f, multiplier);
        float globalMultiplier = Alert.CurrentAlert == GuardAlertSystem.AlertLevel.FullAlert ? 1.2f : 1f;
        Navigation.SetSpeed(DefaultAgentSpeed * clampedMultiplier * globalMultiplier);
    }

    public void SetDestination(Vector3 destination)
    {
        Navigation.SetDestination(destination);
    }

    public bool HasReachedDestination()
    {
        if (!Navigation.IsAvailable)
        {
            return true;
        }

        if (Navigation.PathPending)
        {
            return false;
        }

        float threshold = Mathf.Max(Navigation.StoppingDistance, DestinationEpsilon);
        if (Navigation.RemainingDistance > threshold)
        {
            return false;
        }

        return !Navigation.HasPath || Navigation.VelocitySqrMagnitude <= 0.0001f;
    }

    public void FaceToward(Vector3 worldPoint, float deltaTime)
    {
        Vector3 toPoint = worldPoint - guardTransform.position;
        toPoint.y = 0f;
        if (toPoint.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toPoint.normalized, Vector3.up);
        guardTransform.rotation = Quaternion.RotateTowards(
            guardTransform.rotation,
            targetRotation,
            Navigation.AngularSpeed * Mathf.Max(0f, deltaTime));
    }

    public bool TryGetPlayerPosition(out Vector3 playerPosition)
    {
        playerPosition = Vector3.zero;

        if (Vision.TryGetPlayerPosition(out playerPosition))
        {
            return true;
        }

        if (HasLastKnownPosition)
        {
            playerPosition = LastKnownPosition;
            return true;
        }

        if (Vision.HasLastKnownPosition)
        {
            playerPosition = Vision.LastKnownPosition;
            return true;
        }

        return false;
    }

    public bool TryGetSuspicionFocusPosition(out Vector3 focusPosition)
    {
        focusPosition = Vector3.zero;

        if (Vision.HasSuspicionFocus)
        {
            focusPosition = Vision.SuspicionFocusPosition;
            return true;
        }

        if (TryGetPlayerPosition(out focusPosition))
        {
            return true;
        }

        if (HasLastKnownPosition)
        {
            focusPosition = LastKnownPosition;
            return true;
        }

        return false;
    }

    public void SyncLastKnownPositionFromSensors()
    {
        if (Vision.HasLastKnownPosition)
        {
            SetLastKnownPosition(Vision.LastKnownPosition);
        }
        else if (!HasLastKnownPosition && Alert.CurrentAlert == GuardAlertSystem.AlertLevel.FullAlert)
        {
            SetLastKnownPosition(Alert.SharedLastKnownPosition);
        }
    }
}
