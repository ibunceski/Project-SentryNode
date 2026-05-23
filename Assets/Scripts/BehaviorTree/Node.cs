using System;

namespace StealthAI.BehaviorTree
{
    /// <summary>
    /// Represents the possible results of evaluating a behavior tree node.
    /// </summary>
    public enum NodeState
    {
        /// <summary>
        /// The node finished successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The node finished with failure.
        /// </summary>
        Failure,

        /// <summary>
        /// The node is still running and has not finished yet.
        /// </summary>
        Running
    }

    /// <summary>
    /// Defines the common contract and state for all behavior tree nodes.
    /// </summary>
    public abstract class Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class.
        /// </summary>
        /// <param name="nodeName">The name used for debugging and visualization.</param>
        protected Node(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                throw new ArgumentException("Node name cannot be null, empty, or whitespace.", nameof(nodeName));
            }

            NodeName = nodeName;
            CurrentState = NodeState.Running;
        }

        /// <summary>
        /// Gets the name of the node for debug display.
        /// </summary>
        public string NodeName { get; }

        /// <summary>
        /// Gets the last returned state from <see cref="Evaluate"/>.
        /// </summary>
        public NodeState CurrentState { get; protected set; }

        /// <summary>
        /// Evaluates this node for the current tick.
        /// </summary>
        /// <returns>The result state of this node after evaluation.</returns>
        public abstract NodeState Evaluate();
    }
}
