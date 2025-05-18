public class AbstractGraphFactoryConfig
{
    public string GraphId = null;
    
    public AbstractGraphFactoryConfig(string graphId = null)
    {
        this.GraphId = graphId;
    }
}

public abstract class AbstractGraphFactory
{
    protected bool configured = false;
    protected abstract AbstractGraphFactoryConfig abstractConfig { get; }
    public AbstractGraphFactoryConfig AbstractConfig => abstractConfig;
    
    protected abstract void ConstructGraphInternal(StateGraph graph);
    public void ConstructGraph(StateGraph graph)
    {
        if (!configured)
        {
            throw new System.Exception("GraphFactory not configured");
        }
        
        if (graph == null)
        {
            throw new System.Exception("Graph is null");
        }

        if (!string.IsNullOrEmpty(this.abstractConfig.GraphId))
        {
            graph.SetGUID(this.abstractConfig.GraphId);
        }
        
        ConstructGraphInternal(graph);
    }
    
    public void SetGraphId(string graphId)
    {
        if (abstractConfig == null)
        {
            throw new System.Exception("GraphFactory not configured. Cannot set Id unless configuration is complete.");
        }
        
        this.abstractConfig.GraphId = graphId;
    }
}

public abstract class GenericAbstractGraphFactory<TConfig> : AbstractGraphFactory
    where TConfig : AbstractGraphFactoryConfig
{
    protected TConfig config;
    protected override AbstractGraphFactoryConfig abstractConfig => config;
    
    public GenericAbstractGraphFactory(TConfig config, string graphId = null)
    {
        this.config = config;
        this.configured = true;
    }

    protected void AddConnectionThroughSay(StateGraph graph, StateGraphNode source, string sourcePortName,
        StateGraphNode destination, string destinationPortName, string message, float messageDuration)
    {
        // We automatically add the source and destination nodes to the graph if they are no already present
        if (graph.GetNodeById(source.id) == null)
        {
            graph.AddNode(source);
        }
        if (graph.GetNodeById(destination.id) == null)
        {
            graph.AddNode(destination);
        }
        
        SayStateNode sayStateNode = new(new SayStateConfiguration()
        {
            m_logLevel = LogLevel.Info,
            m_textDuration = messageDuration,
            m_textToSay = message,
            m_waitDuration = messageDuration
        });
        
        graph.AddNode(sayStateNode);
        graph.ConnectStateFlow(
            source.id, new NodePortInfo(sourcePortName, PortType.StateTransitionOut),
            sayStateNode.id, new NodePortInfo(StateNode.IN_PORT_NAME, PortType.StateTransitionIn)
        );
        graph.ConnectStateFlow(
            sayStateNode.id, new NodePortInfo(nameof(SayStateOutcome.Timeout), PortType.StateTransitionOut),
            destination.id, new NodePortInfo(destinationPortName, PortType.StateTransitionIn)
        );
    }
}