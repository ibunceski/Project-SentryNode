namespace StealthAI.BehaviorTree
{
    /// <summary>
    /// Base class for action leaf nodes that perform behavior logic.
    /// </summary>
    public abstract class ActionNode : Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActionNode"/> class.
        /// </summary>
        /// <param name="nodeName">The name used for debugging and visualization.</param>
        protected ActionNode(string nodeName) : base(nodeName)
        {
        }
    }
}
