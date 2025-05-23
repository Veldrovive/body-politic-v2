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

public class AnimateGraphFactory : GenericAbstractGraphFactory<AnimateGraphConfiguration>
{
    public AnimateGraphFactory(AnimateGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph)
    {
        // "Start -> AnimateState -> Exit"
        AnimateStateNode animateStateNode = new(config.animateStateConfig);
        graph.AddNode(animateStateNode);
        
        graph.ConnectStateFlow(new StartNode(), animateStateNode);
        graph.ConnectStateFlow(animateStateNode, AnimateStateOutcome.Timeout, new ExitNode());
    }
}