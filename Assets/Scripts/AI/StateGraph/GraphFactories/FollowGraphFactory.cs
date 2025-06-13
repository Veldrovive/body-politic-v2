
using UnityEngine;

public class FollowGraphConfiguration : AbstractGraphFactoryConfig
{
    [Header("Targeting")]
    public FollowStateTargetingType TargetingType = FollowStateTargetingType.Transform;
    public TransformReference TargetTransform = new TransformReference();
    public Vector3Reference TargetPosition = new Vector3Reference();

    [Header("Configuration")]
    public float FollowDistance = 5f;
    public float MaxDuration = 10f;
    public float MaxDurationWithoutLoS = 5f;
    public MovementSpeed Speed = MovementSpeed.Walk;

    [Header("Messages")]
    public string EntryMessage = "";
    public float EntryMessageDuration = 3f;
    public float EntryWaitDuration = 0f; // Wait before starting to look
    
    public string ExitMessage = "";
    public float ExitMessageDuration = 3f;
    public float ExitWaitDuration = 0f; // Wait before exiting the graph
}

public enum FollowStateExitConnection
{
    FollowCompleted,
    FollowErrorGeneric,
    FollowErrorRoleDoorFailed,
}

/// <summary>
/// The curious reaction is a follow state that causes an NPC to look at another NPC until it passes out of view
/// or a duration is exceeded.
///
/// We can construct the follow state with distance set to exit if it exceeds a threshold and LoS set to exit if it is lost.
/// In effect, this makes the NPC visually track the other NPC 
/// </summary>
public class FollowGraphFactory : GenericAbstractGraphFactory<FollowGraphConfiguration, FollowStateExitConnection>
{
    public FollowGraphFactory(FollowGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        FollowStateNode followState = new(new FollowStateConfiguration()
        {
            TargetingType = config.TargetingType,
            TargetTransform = config.TargetTransform,
            TargetPosition = config.TargetPosition,

            DistanceConfiguration = FollowStateDistanceConfiguration.KeepWithinDistance,
            HorizontalDistanceParameter = config.FollowDistance,
            
            MaxDuration = config.MaxDuration,
            
            LoSConfiguration = FollowStateLoSConfiguration.KeepWithinLoS,
            MaxDurationWithoutLoS = config.MaxDurationWithoutLoS,  // But still exit if LoS is lost for too long
            
            MovementSpeed = config.Speed,
            
            LoSObstacleLayerMask = LayerMask.GetMask("Default")
        });
        
        graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, followState, StateNode.IN_PORT_NAME);
        
        AddExitConnection(FollowStateExitConnection.FollowCompleted,
            followState, nameof(FollowStateOutcome.Completed), config.ExitMessage);
        
        AddExitConnection(FollowStateExitConnection.FollowErrorRoleDoorFailed,
            followState, nameof(FollowStateOutcome.RoleDoorFailed), "I can't follow that.");
        AddExitConnection(FollowStateExitConnection.FollowErrorGeneric,
            followState, nameof(FollowStateOutcome.MovementManagerError), "I can't follow that.");
    }
}