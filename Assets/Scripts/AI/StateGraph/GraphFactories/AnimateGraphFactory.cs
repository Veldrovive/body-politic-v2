public class AnimateGraphConfiguration : AbstractGraphFactoryConfig
{
    public AnimateStateConfiguration animateStateConfig;

    public AnimateGraphConfiguration()
    {
        animateStateConfig = new();
    }

    public AnimateGraphConfiguration(AnimateStateConfiguration config)
    {
        animateStateConfig = config;
    }
}

public enum AnimateGraphExitConnection
{
    AnimationCompleted,
}

public class AnimateGraphFactory : GenericAbstractGraphFactory<AnimateGraphConfiguration, AnimateGraphExitConnection>
{
    public AnimateGraphFactory(AnimateGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        // "Start -> AnimateState -> Exit"
        AnimateStateNode animateStateNode = new(config.animateStateConfig);
        
        graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, animateStateNode, StateNode.IN_PORT_NAME);
        AddExitConnection(AnimateGraphExitConnection.AnimationCompleted, animateStateNode, nameof(AnimateStateOutcome.Timeout));
    }
}