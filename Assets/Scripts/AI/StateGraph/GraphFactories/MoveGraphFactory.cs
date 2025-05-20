public class MoveGraphConfiguration : AbstractGraphFactoryConfig
{
    public MoveToStateConfiguration moveToStateConfig;

    public float SayBubbleDuration = 3f;
    public string ArrivedMessage = "";
    public string TargetDestinationInvalidMessage = "";
    public string MovementExecutionFailedMessage = "";
    public string NavigationTimeoutMessage = "";
    public string DoorRoleFailedMessage = "";
    
    public string PreStartMessage = "";
}

public class MoveGraphFactory : GenericAbstractGraphFactory<MoveGraphConfiguration>
{
    public MoveGraphFactory(MoveGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }
    private void AddSayState(StateGraph graph, MoveToStateNode moveToStateNode, string message, float duration, MoveToStateOutcome outcome)
    {
        if (string.IsNullOrEmpty(message))
        {
            // We always exit after the say state
            graph.ConnectStateFlow(moveToStateNode, outcome, new ExitNode());
        }
        else
        {
            SayStateNode sayStateNode = new(new SayStateConfiguration()
            {
                m_logLevel = LogLevel.Info,
                m_textDuration = duration,
                m_textToSay = message
            });
            graph.AddNode(sayStateNode);
            graph.ConnectStateFlow(moveToStateNode, outcome, sayStateNode);
        
            // We always exit after the say state
            graph.ConnectStateFlow(sayStateNode, SayStateOutcome.Timeout, new ExitNode());
        }
    }

    protected override void ConstructGraphInternal(StateGraph graph)
    {
        MoveToStateNode moveToStateNode;
        if (!string.IsNullOrEmpty(config.PreStartMessage))
        {
            // Then we start with a message while we start moving
            SayStateNode sayStateNode = new(new SayStateConfiguration()
            {
                m_logLevel = LogLevel.Info,
                m_textDuration = config.SayBubbleDuration,
                m_waitDuration = 0,  // Say, but start moving immediately
                m_textToSay = config.PreStartMessage
            });
            graph.AddNode(sayStateNode);
            graph.ConnectStateFlow(new StartNode(), sayStateNode);
            
            moveToStateNode = new(config.moveToStateConfig);
            graph.AddNode(moveToStateNode);
            graph.ConnectStateFlow(sayStateNode, SayStateOutcome.Timeout, moveToStateNode);
        }
        else
        {
            // Then we just start with the move
            moveToStateNode = new(config.moveToStateConfig);
            graph.AddNode(moveToStateNode);
            graph.ConnectStateFlow(new StartNode(), moveToStateNode);
        }
        
        
        // Connect up the move outcomes
        AddSayState(graph, moveToStateNode, config.ArrivedMessage, config.SayBubbleDuration, MoveToStateOutcome.Arrived);
        AddSayState(graph, moveToStateNode, config.TargetDestinationInvalidMessage, config.SayBubbleDuration, MoveToStateOutcome.Error);
        AddSayState(graph, moveToStateNode, config.DoorRoleFailedMessage, config.SayBubbleDuration, MoveToStateOutcome.DoorRoleFailed);
    }
}