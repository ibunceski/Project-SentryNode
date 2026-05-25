using System.Collections.Generic;
using StealthAI.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Coordinates guard behavior by composing interchangeable AI services and behavior branches.
/// </summary>
public class GuardAI : MonoBehaviour
{
    /// <summary>
    /// Represents the high-level guard behavior state for debugging and external UI.
    /// </summary>
    public enum GuardState
    {
        Patrolling,
        Suspicious,
        Chasing,
        Investigating,
        Searching
    }

    [Header("Injected Systems")]
    [SerializeField]
    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private VisionSystem visionSystem;

    [SerializeField]
    private HearingSystem hearingSystem;

    [SerializeField]
    private PatrolSystem patrolSystem;

    [SerializeField]
    private SearchSystem searchSystem;

    [Header("Behavior Extensions")]
    [SerializeField]
    private MonoBehaviour[] branchProviderBehaviours;

    [Header("Search Tuning")]
    [SerializeField]
    private float destinationEpsilon = 0.15f;

    [Header("Runtime Debug")]
    [SerializeField]
    private string lastActiveLeafNodeName = "None";

    [SerializeField]
    private GuardState currentGuardState = GuardState.Patrolling;

    private Node root;
    private Sequence investigateSequence;
    private GuardRuntimeContext runtimeContext;
    private IGuardAlertService alertService;
    private IGuardSearchService searchService;
    private IGuardNavigationService navigationService;
    private float defaultAgentSpeed = 3.5f;

    /// <summary>
    /// Gets the last leaf node name evaluated by the behavior tree.
    /// </summary>
    public string LastActiveLeafNodeName => lastActiveLeafNodeName;

    /// <summary>
    /// Gets the active behavior tree node name used for debug visualization.
    /// </summary>
    public string ActiveNodeName => lastActiveLeafNodeName;

    /// <summary>
    /// Gets the current high-level state of the guard.
    /// </summary>
    public GuardState CurrentGuardState => currentGuardState;

    private void Start()
    {
        EnsureSupportingComponents();
        InitializeRuntimeContext();
        BuildBehaviorTree();
    }

    private void Update()
    {
        if (alertService != null)
        {
            alertService.Tick();
        }

        if (root == null || runtimeContext == null)
        {
            return;
        }

        root.Evaluate();
        SyncDebugState();
        HandleInvestigationCompletion();
    }

    private void EnsureSupportingComponents()
    {
        if (GetComponent<GuardVisionRenderer>() == null)
        {
            gameObject.AddComponent<GuardVisionRenderer>();
        }

        if (navMeshAgent != null)
        {
            defaultAgentSpeed = navMeshAgent.speed;
        }

        if (searchSystem == null)
        {
            searchSystem = GetComponent<SearchSystem>();
            if (searchSystem == null)
            {
                searchSystem = gameObject.AddComponent<SearchSystem>();
            }
        }
    }

    private void InitializeRuntimeContext()
    {
        navigationService = new GuardNavigationServiceAdapter(navMeshAgent);
        IGuardVisionService visionService = new GuardVisionServiceAdapter(visionSystem);
        IGuardHearingService hearingService = new GuardHearingServiceAdapter(hearingSystem);
        IGuardPatrolService patrolService = new GuardPatrolServiceAdapter(patrolSystem, navMeshAgent);
        searchService = new GuardSearchServiceAdapter(searchSystem);
        alertService = new GuardAlertServiceAdapter();
        IPlayerLocator playerLocator = new TagPlayerLocator("Player");

        runtimeContext = new GuardRuntimeContext(
            transform,
            navigationService,
            visionService,
            hearingService,
            patrolService,
            searchService,
            alertService,
            playerLocator,
            destinationEpsilon,
            defaultAgentSpeed);

        runtimeContext.SetState(currentGuardState);
        runtimeContext.MarkLeafActive(lastActiveLeafNodeName);
    }

    private void BuildBehaviorTree()
    {
        List<IGuardBehaviorBranchProvider> providers = new List<IGuardBehaviorBranchProvider>();
        foreach (IGuardBehaviorBranchProvider provider in DefaultGuardBehaviorBranches.Create())
        {
            providers.Add(provider);
        }

        foreach (IGuardBehaviorBranchProvider provider in ResolveExtensionProviders())
        {
            providers.Add(provider);
        }

        GuardBehaviorTreeFactory treeFactory = new GuardBehaviorTreeFactory(providers);
        GuardBehaviorTreeBuildResult tree = treeFactory.Build(runtimeContext);
        root = tree.Root;
        investigateSequence = tree.InvestigateSequence;
    }

    private IEnumerable<IGuardBehaviorBranchProvider> ResolveExtensionProviders()
    {
        if (branchProviderBehaviours == null)
        {
            yield break;
        }

        for (int i = 0; i < branchProviderBehaviours.Length; i++)
        {
            MonoBehaviour behavior = branchProviderBehaviours[i];
            if (behavior == null)
            {
                continue;
            }

            if (behavior is IGuardBehaviorBranchProvider provider)
            {
                yield return provider;
            }
        }
    }

    private void HandleInvestigationCompletion()
    {
        if (investigateSequence == null)
        {
            return;
        }

        NodeState investigateState = investigateSequence.CurrentState;
        bool completedSearchThisTick = investigateState == NodeState.Success && lastActiveLeafNodeName == "SearchArea";
        bool failedDuringActiveSearch = investigateState == NodeState.Failure && runtimeContext.SearchStarted;
        if (!completedSearchThisTick && !failedDuringActiveSearch)
        {
            return;
        }

        if (completedSearchThisTick)
        {
            Debug.Log("[GuardAI] Search complete - returning to patrol");
        }
        else
        {
            Debug.Log("[GuardAI] Investigate failed - returning to patrol");
        }

        runtimeContext.Vision.ClearLastKnownPosition();
        runtimeContext.ClearLastKnownPositionMemory();
        runtimeContext.ResetSearchState();
        searchService.Reset();
        navigationService.ResetPath();
        SyncDebugState();
    }

    private void SyncDebugState()
    {
        if (runtimeContext == null)
        {
            return;
        }

        lastActiveLeafNodeName = runtimeContext.ActiveLeafNodeName;
        currentGuardState = runtimeContext.CurrentState;
    }
}
