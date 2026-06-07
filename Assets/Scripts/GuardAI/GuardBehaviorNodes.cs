using StealthAI.BehaviorTree;
using UnityEngine;

/// <summary>
/// Base class for guard condition nodes with shared context and leaf tracking behavior.
/// </summary>
public abstract class GuardConditionNode : ConditionNode
{
    protected GuardConditionNode(string nodeName, GuardRuntimeContext context) : base(nodeName)
    {
        Context = context;
    }

    protected GuardRuntimeContext Context { get; }

    public override NodeState Evaluate()
    {
        Context.MarkLeafActive(NodeName);
        CurrentState = EvaluateCondition();
        return CurrentState;
    }

    protected abstract NodeState EvaluateCondition();
}

/// <summary>
/// Base class for guard action nodes with shared context and leaf tracking behavior.
/// </summary>
public abstract class GuardActionNode : ActionNode
{
    protected GuardActionNode(string nodeName, GuardRuntimeContext context) : base(nodeName)
    {
        Context = context;
    }

    protected GuardRuntimeContext Context { get; }

    public override NodeState Evaluate()
    {
        Context.MarkLeafActive(NodeName);
        CurrentState = EvaluateAction();
        return CurrentState;
    }

    protected abstract NodeState EvaluateAction();
}

public sealed class CanSeePlayerConditionNode : GuardConditionNode
{
    public CanSeePlayerConditionNode(GuardRuntimeContext context) : base("CanSeePlayer", context)
    {
    }

    protected override NodeState EvaluateCondition()
    {
        bool hasDirectVisualContact = Context.Vision.PlayerTransform != null;
        if (Context.Vision.Suspicion < 80f || !hasDirectVisualContact)
        {
            return NodeState.Failure;
        }

        if (Context.TryGetPlayerPosition(out Vector3 playerPosition))
        {
            Context.SetLastKnownPosition(playerPosition);
            Context.Alert.ReportPlayerSeen(playerPosition);
        }
        else if (Context.Vision.HasLastKnownPosition)
        {
            Context.SetLastKnownPosition(Context.Vision.LastKnownPosition);
            Context.Alert.ReportPlayerSeen(Context.Vision.LastKnownPosition);
        }

        return NodeState.Success;
    }
}

public sealed class IsSuspiciousConditionNode : GuardConditionNode
{
    public IsSuspiciousConditionNode(GuardRuntimeContext context) : base("IsSuspicious", context)
    {
    }

    protected override NodeState EvaluateCondition()
    {
        return Context.Vision.CurrentSuspicionLevel == VisionSystem.SuspicionLevel.Suspicious
            ? NodeState.Success
            : NodeState.Failure;
    }
}

public sealed class TurnTowardSuspicionActionNode : GuardActionNode
{
    public TurnTowardSuspicionActionNode(GuardRuntimeContext context) : base("TurnTowardSuspicion", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        Context.SetState(GuardAI.GuardState.Suspicious);
        Context.ResetSearchState();
        Context.SetAgentSpeedMultiplier(0.6f);
        Context.Navigation.SetStopped(false);

        if (!Context.TryGetSuspicionFocusPosition(out Vector3 focusPosition))
        {
            return NodeState.Failure;
        }

        Vector3 toFocus = focusPosition - Context.Transform.position;
        toFocus.y = 0f;
        if (toFocus.sqrMagnitude <= 0.0001f)
        {
            return NodeState.Running;
        }

        Context.FaceToward(focusPosition, Time.deltaTime);
        return NodeState.Running;
    }
}

public sealed class ChasePlayerActionNode : GuardActionNode
{
    public ChasePlayerActionNode(GuardRuntimeContext context) : base("ChasePlayer", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        Context.SetState(GuardAI.GuardState.Chasing);
        Context.ResetSearchState();
        Context.SetAgentSpeedMultiplier(1f);

        bool hasLineOfSight = Context.Vision.PlayerTransform != null;
        bool hasLivePlayerPosition = false;
        Vector3 playerPosition = Vector3.zero;

        if (Context.Vision.PlayerTransform != null)
        {
            playerPosition = Context.Vision.PlayerTransform.position;
            hasLivePlayerPosition = true;
        }

        if (!hasLivePlayerPosition)
        {
            if (Context.Vision.HasLastKnownPosition)
            {
                playerPosition = Context.Vision.LastKnownPosition;
            }
            else if (Context.HasLastKnownPosition)
            {
                playerPosition = Context.LastKnownPosition;
            }
            else
            {
                playerPosition = Context.Transform.position;
            }
        }

        float closeProximityThreshold = Mathf.Max(
            1.5f,
            Context.Navigation.StoppingDistance + Context.Navigation.Radius + Context.DestinationEpsilon);
        bool playerIsClose = Vector3.Distance(Context.Transform.position, playerPosition) <= closeProximityThreshold;

        if (!hasLineOfSight)
        {
            return NodeState.Failure;
        }

        Context.SetDestination(playerPosition);
        Context.SetLastKnownPosition(playerPosition);
        Context.Alert.ReportPlayerSeen(playerPosition);

        if (playerIsClose)
        {
            Context.Navigation.SetStopped(true);
            Context.Navigation.ResetPath();
        }
        else
        {
            Context.Navigation.SetStopped(false);
        }

        Context.FaceToward(playerPosition, Time.deltaTime);
        return NodeState.Running;
    }
}

public sealed class HasLastKnownPositionConditionNode : GuardConditionNode
{
    public HasLastKnownPositionConditionNode(GuardRuntimeContext context) : base("HasLastKnownPosition", context)
    {
    }

    protected override NodeState EvaluateCondition()
    {
        Context.SyncLastKnownPositionFromSensors();
        return Context.HasLastKnownPosition ? NodeState.Success : NodeState.Failure;
    }
}

public sealed class MoveToLastKnownPositionActionNode : GuardActionNode
{
    public MoveToLastKnownPositionActionNode(GuardRuntimeContext context) : base("MoveToLastKnownPosition", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        if (Context.SearchStarted)
        {
            return NodeState.Success;
        }

        Context.SetState(GuardAI.GuardState.Investigating);
        Context.SetAgentSpeedMultiplier(1f);
        Context.SyncLastKnownPositionFromSensors();
        if (!Context.HasLastKnownPosition)
        {
            return NodeState.Failure;
        }

        Context.SetDestination(Context.LastKnownPosition);
        return Context.HasReachedDestination() ? NodeState.Success : NodeState.Running;
    }
}

public sealed class SearchAreaActionNode : GuardActionNode
{
    public SearchAreaActionNode(GuardRuntimeContext context) : base("SearchArea", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        Context.SetState(GuardAI.GuardState.Searching);
        Context.SetAgentSpeedMultiplier(0.75f);

        if (!Context.SearchStarted)
        {
            Context.StartSearch();
        }

        Context.AddSearchTime(Time.deltaTime);

        bool allPointsVisited = Context.Search.Tick();
        if (allPointsVisited || Context.SearchTimer >= 8f)
        {
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}

public sealed class HeardNoiseConditionNode : GuardConditionNode
{
    public HeardNoiseConditionNode(GuardRuntimeContext context) : base("HeardNoise", context)
    {
    }

    protected override NodeState EvaluateCondition()
    {
        if (!Context.Hearing.HasHeardNoise)
        {
            return NodeState.Failure;
        }

        if (Context.Hearing.TryGetNoiseSourcePosition(out Vector3 noisePosition))
        {
            Context.SetNoiseSource(noisePosition);
        }
        else if (Context.TryGetPlayerPosition(out Vector3 playerPosition))
        {
            Context.SetNoiseSource(playerPosition);
        }

        return Context.HasNoiseSource ? NodeState.Success : NodeState.Failure;
    }
}

public sealed class MoveToNoiseSourceActionNode : GuardActionNode
{
    public MoveToNoiseSourceActionNode(GuardRuntimeContext context) : base("MoveToNoiseSource", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        Context.SetState(GuardAI.GuardState.Investigating);
        Context.SetAgentSpeedMultiplier(1f);

        if (!Context.HasNoiseSource)
        {
            return NodeState.Failure;
        }

        Context.SetDestination(Context.NoiseSourcePosition);
        if (Context.HasReachedDestination())
        {
            Context.SetLastKnownPosition(Context.NoiseSourcePosition);
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}

public sealed class ClearNoiseActionNode : GuardActionNode
{
    public ClearNoiseActionNode(GuardRuntimeContext context) : base("ClearNoise", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        Context.Hearing.ClearNoise();
        Context.ClearNoiseSource();
        return NodeState.Success;
    }
}

public sealed class PatrolActionNode : GuardActionNode
{
    public PatrolActionNode(GuardRuntimeContext context) : base("Patrol", context)
    {
    }

    protected override NodeState EvaluateAction()
    {
        if (Context.CurrentState == GuardAI.GuardState.Chasing
            || Context.CurrentState == GuardAI.GuardState.Suspicious
            || Context.CurrentState == GuardAI.GuardState.Investigating
            || Context.CurrentState == GuardAI.GuardState.Searching)
        {
            Context.Patrol.ResetPatrol();
        }

        Context.SetState(GuardAI.GuardState.Patrolling);
        Context.ResetSearchState();
        Context.SetAgentSpeedMultiplier(1f);
        return Context.Patrol.TickPatrol();
    }
}
