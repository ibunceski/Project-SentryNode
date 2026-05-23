using System;
using System.Collections.Generic;

namespace StealthAI.BehaviorTree
{
    /// <summary>
    /// Composite node that evaluates children in order and succeeds on the first child success.
    /// </summary>
    public class Selector : Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Selector"/> class.
        /// </summary>
        /// <param name="nodeName">The name used for debugging and visualization.</param>
        public Selector(string nodeName) : base(nodeName)
        {
            Children = new List<Node>();
        }

        /// <summary>
        /// Gets the child nodes evaluated by this selector.
        /// </summary>
        public List<Node> Children { get; }

        /// <summary>
        /// Adds a child node to this selector.
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
        /// Evaluates each child in order and returns success when the first child succeeds.
        /// </summary>
        /// <returns>
        /// <see cref="NodeState.Success"/> if a child succeeds;
        /// <see cref="NodeState.Running"/> if a child is running and none before it succeeded;
        /// otherwise <see cref="NodeState.Failure"/>.
        /// </returns>
        public override NodeState Evaluate()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                NodeState childState = Children[i].Evaluate();
                if (childState == NodeState.Success)
                {
                    CurrentState = NodeState.Success;
                    return CurrentState;
                }

                if (childState == NodeState.Running)
                {
                    CurrentState = NodeState.Running;
                    return CurrentState;
                }
            }

            CurrentState = NodeState.Failure;
            return CurrentState;
        }
    }
}
