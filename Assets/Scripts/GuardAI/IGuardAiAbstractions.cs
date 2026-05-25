using StealthAI.BehaviorTree;
using UnityEngine;

/// <summary>
/// Provides read access to vision signals used by guard behavior nodes.
/// </summary>
public interface IGuardVisionService
{
    float Suspicion { get; }
    VisionSystem.SuspicionLevel CurrentSuspicionLevel { get; }
    bool HasLastKnownPosition { get; }
    Vector3 LastKnownPosition { get; }
    Transform PlayerTransform { get; }
    bool HasSuspicionFocus { get; }
    Vector3 SuspicionFocusPosition { get; }
    bool TryGetPlayerPosition(out Vector3 playerPosition);
    void ClearLastKnownPosition();
}

/// <summary>
/// Provides read/write access to hearing signals used by guard behavior nodes.
/// </summary>
public interface IGuardHearingService
{
    bool HasHeardNoise { get; }
    bool TryGetNoiseSourcePosition(out Vector3 noisePosition);
    void ClearNoise();
}

/// <summary>
/// Provides patrol behavior as an interchangeable service.
/// </summary>
public interface IGuardPatrolService
{
    NodeState TickPatrol();
    void ResetPatrol();
}

/// <summary>
/// Provides search behavior as an interchangeable service.
/// </summary>
public interface IGuardSearchService
{
    void BeginSearch(Vector3 lastKnownPosition);
    bool Tick();
    void Reset();
}

/// <summary>
/// Provides global alert coordination for guards.
/// </summary>
public interface IGuardAlertService
{
    GuardAlertSystem.AlertLevel CurrentAlert { get; }
    Vector3 SharedLastKnownPosition { get; }
    void Tick();
    void ReportPlayerSeen(Vector3 position);
}

/// <summary>
/// Provides navigation and movement controls independent of concrete NavMeshAgent usage.
/// </summary>
public interface IGuardNavigationService
{
    bool IsAvailable { get; }
    float AngularSpeed { get; }
    float StoppingDistance { get; }
    float Radius { get; }
    bool PathPending { get; }
    float RemainingDistance { get; }
    bool HasPath { get; }
    float VelocitySqrMagnitude { get; }
    void SetSpeed(float speed);
    void SetStopped(bool stopped);
    void SetDestination(Vector3 destination);
    void ResetPath();
}

/// <summary>
/// Resolves the current player location using a strategy that can be replaced without modifying guard logic.
/// </summary>
public interface IPlayerLocator
{
    bool TryGetPlayerPosition(out Vector3 playerPosition);
}

/// <summary>
/// Provides a behavior branch that can be inserted into the guard root selector.
/// </summary>
public interface IGuardBehaviorBranchProvider
{
    int Order { get; }
    bool IsInvestigateBranch { get; }
    Node CreateBranch(GuardRuntimeContext context);
}
