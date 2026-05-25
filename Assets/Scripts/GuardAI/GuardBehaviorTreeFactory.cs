using System.Collections.Generic;
using StealthAI.BehaviorTree;
using UnityEngine;

/// <summary>
/// Builds a guard behavior tree from ordered branch providers.
/// </summary>
public sealed class GuardBehaviorTreeFactory
{
    private readonly List<IGuardBehaviorBranchProvider> providers = new List<IGuardBehaviorBranchProvider>();

    public GuardBehaviorTreeFactory(IEnumerable<IGuardBehaviorBranchProvider> providers)
    {
        if (providers == null)
        {
            return;
        }

        foreach (IGuardBehaviorBranchProvider provider in providers)
        {
            if (provider != null)
            {
                this.providers.Add(provider);
            }
        }

        this.providers.Sort((left, right) => left.Order.CompareTo(right.Order));
    }

    public GuardBehaviorTreeBuildResult Build(GuardRuntimeContext context)
    {
        Selector rootSelector = new Selector("ROOT");
        Sequence investigateSequence = null;

        for (int i = 0; i < providers.Count; i++)
        {
            IGuardBehaviorBranchProvider provider = providers[i];
            Node branch = provider.CreateBranch(context);
            if (branch == null)
            {
                continue;
            }

            rootSelector.AddChild(branch);

            if (provider.IsInvestigateBranch)
            {
                if (branch is Sequence sequenceBranch)
                {
                    investigateSequence = sequenceBranch;
                }
                else
                {
                    Debug.LogWarning("[GuardBehaviorTreeFactory] Investigate branch provider must return Sequence for cleanup tracking.");
                }
            }
        }

        return new GuardBehaviorTreeBuildResult(rootSelector, investigateSequence);
    }
}

/// <summary>
/// Output container for tree and important branch handles.
/// </summary>
public struct GuardBehaviorTreeBuildResult
{
    public GuardBehaviorTreeBuildResult(Node root, Sequence investigateSequence)
    {
        Root = root;
        InvestigateSequence = investigateSequence;
    }

    public Node Root { get; }
    public Sequence InvestigateSequence { get; }
}

/// <summary>
/// Default ordered branch providers matching legacy behavior priorities.
/// </summary>
public static class DefaultGuardBehaviorBranches
{
    public static IEnumerable<IGuardBehaviorBranchProvider> Create()
    {
        yield return new ChaseBranchProvider();
        yield return new SuspiciousBranchProvider();
        yield return new InvestigateBranchProvider();
        yield return new NoiseBranchProvider();
        yield return new PatrolBranchProvider();
    }
}

public sealed class ChaseBranchProvider : IGuardBehaviorBranchProvider
{
    public int Order => 100;

    public bool IsInvestigateBranch => false;

    public Node CreateBranch(GuardRuntimeContext context)
    {
        Sequence sequence = new Sequence("Chase Sequence");
        sequence.AddChild(new CanSeePlayerConditionNode(context));
        sequence.AddChild(new ChasePlayerActionNode(context));
        return sequence;
    }
}

public sealed class SuspiciousBranchProvider : IGuardBehaviorBranchProvider
{
    public int Order => 200;

    public bool IsInvestigateBranch => false;

    public Node CreateBranch(GuardRuntimeContext context)
    {
        Sequence sequence = new Sequence("Suspicious Sequence");
        sequence.AddChild(new IsSuspiciousConditionNode(context));
        sequence.AddChild(new TurnTowardSuspicionActionNode(context));
        return sequence;
    }
}

public sealed class InvestigateBranchProvider : IGuardBehaviorBranchProvider
{
    public int Order => 300;

    public bool IsInvestigateBranch => true;

    public Node CreateBranch(GuardRuntimeContext context)
    {
        Sequence sequence = new Sequence("Investigate Sequence");
        sequence.AddChild(new HasLastKnownPositionConditionNode(context));
        sequence.AddChild(new MoveToLastKnownPositionActionNode(context));
        sequence.AddChild(new SearchAreaActionNode(context));
        return sequence;
    }
}

public sealed class NoiseBranchProvider : IGuardBehaviorBranchProvider
{
    public int Order => 400;

    public bool IsInvestigateBranch => false;

    public Node CreateBranch(GuardRuntimeContext context)
    {
        Sequence sequence = new Sequence("Noise Sequence");
        sequence.AddChild(new HeardNoiseConditionNode(context));
        sequence.AddChild(new MoveToNoiseSourceActionNode(context));
        sequence.AddChild(new ClearNoiseActionNode(context));
        return sequence;
    }
}

public sealed class PatrolBranchProvider : IGuardBehaviorBranchProvider
{
    public int Order => 500;

    public bool IsInvestigateBranch => false;

    public Node CreateBranch(GuardRuntimeContext context)
    {
        return new PatrolActionNode(context);
    }
}
