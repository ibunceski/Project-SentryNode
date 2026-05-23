using System;
using System.Collections.Generic;

namespace StealthAI.BehaviorTree
{
    /// <summary>
    /// Composite node that evaluates children in order and fails on the first child failure.
    /// </summary>
    public class Sequence : Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Sequence"/> class.
        /// </summary>
        /// <param name="nodeName">The name used for debugging and visualization.</param>
        public Sequence(string nodeName) : base(nodeName)
        {
            Children = new List<Node>();
        }

        /// <summary>
        /// Gets the child nodes evaluated by this sequence.
        /// </summary>
        public List<Node> Children { get; }

        /// <summary>
        /// Adds a child node to this sequence.
        /// </summary>
        /// <param name="child">The child node to add.</param>
        public void AddChild(Node child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            Children.Add(child);
        }

        /// <summary>
        /// Evaluates each child in order and returns failure when the first child fails.
        /// </summary>
        /// <returns>
        /// <see cref="NodeState.Failure"/> if a child fails;
        /// <see cref="NodeState.Running"/> if a child is running and none before it failed;
        /// otherwise <see cref="NodeState.Success"/>.
        /// </returns>
        public override NodeState Evaluate()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                NodeState childState = Children[i].Evaluate();
                if (childState == NodeState.Failure)
                {
                    CurrentState = NodeState.Failure;
                    return CurrentState;
                }

                if (childState == NodeState.Running)
                {
                    CurrentState = NodeState.Running;
                    return CurrentState;
                }
            }

            CurrentState = NodeState.Success;
            return CurrentState;
        }
    }
}
