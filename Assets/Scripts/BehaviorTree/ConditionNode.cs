namespace StealthAI.BehaviorTree
{
    /// <summary>
    /// Base class for condition leaf nodes that perform boolean checks.
    /// </summary>
    public abstract class ConditionNode : Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionNode"/> class.
        /// </summary>
        /// <param name="nodeName">The name used for debugging and visualization.</param>
        protected ConditionNode(string nodeName) : base(nodeName)
        {
        }
    }
}
