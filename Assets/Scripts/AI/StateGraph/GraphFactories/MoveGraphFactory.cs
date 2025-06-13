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

public enum MoveGraphExitConnection
{
    MoveErrorDoorRoleFailed,
    MoveError,
    MoveCompleted
}

public class MoveGraphFactory : GenericAbstractGraphFactory<MoveGraphConfiguration, MoveGraphExitConnection>
{
    public MoveGraphFactory(MoveGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        MoveToStateNode moveToStateNode = new(config.moveToStateConfig);
        graph.AddNode(moveToStateNode);
        SayRoleMissingListenerNode roleAlertNode = new();
        graph.AddNode(roleAlertNode);
        graph.ConnectEvent(moveToStateNode, MoveToState.ON_ROLE_DOOR_FAILED_PORT_NAME, roleAlertNode,
            SayRoleMissingListenerNode.SAY_ROLE_MISSING_PORT_NAME);
        
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
            graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, sayStateNode, StateNode.IN_PORT_NAME);
            
            graph.ConnectStateFlow(sayStateNode, SayStateOutcome.Timeout, moveToStateNode);
        }
        else
        {
            graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, moveToStateNode, StateNode.IN_PORT_NAME);
        }
        
        
        // Connect up the move outcomes
        AddExitConnection(MoveGraphExitConnection.MoveCompleted,
            moveToStateNode, nameof(MoveToStateOutcome.Arrived), config.ArrivedMessage);
        AddExitConnection(MoveGraphExitConnection.MoveErrorDoorRoleFailed,
            moveToStateNode, nameof(MoveToStateOutcome.DoorRoleFailed), config.DoorRoleFailedMessage);
        AddExitConnection(MoveGraphExitConnection.MoveError,
            moveToStateNode, nameof(MoveToStateOutcome.Error), config.MovementExecutionFailedMessage);
    }
}