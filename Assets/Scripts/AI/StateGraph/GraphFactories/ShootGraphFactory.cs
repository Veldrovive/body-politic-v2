

using System;
using UnityEngine;

/// Shoot Steps:
/// 1. Attempt to gain role that will allow shooting by managing inventory
///     We require a new state for this that searches for the held item ability role
///     If fails: exit the graph
/// 2. Enter the follow state to gain a line of sight to the target
/// 3. Enter the interaction state to shoot the target


[Serializable]
public class ShootGraphConfiguration : AbstractGraphFactoryConfig
{
    public NpcRoleSO RequiredRoleForShootInteraction;
    public InteractionDefinitionSO ShootInteractionDefinition;
    public float MaxShootDistance => ShootInteractionDefinition?.RequiredProximity ?? 10f;
    public float DistanceMargin = 2f;
    public float MaxChaseDuration = 20f;  // How long to chase the target before giving up
    public float MaxChaseDurationWithoutLoS = 10f; // How long to chase the target without line of sight before giving up
    
    [NonSerialized] public InteractableNpc TargetInteractable;
}

public enum ShootGraphExitConnection
{
    ShootCompleted,
    MovementManagerError,
    GunNotFound,
    LostTarget,  // Duration exceeded without finding the target
    ChaseRoleDoorFailed,
    TriedToShootWithoutRole,
    InteractionErrorGeneric
}

public class ShootGraphFactory : GenericAbstractGraphFactory<ShootGraphConfiguration, ShootGraphExitConnection>
{
    public ShootGraphFactory(ShootGraphConfiguration  configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        HoldItemGateStateNode getShootRoleState = new (new HoldItemGateStateConfiguration()
        {
            HeldRole = config.RequiredRoleForShootInteraction
        });
        // First, attach the start point here
        graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, getShootRoleState, StateNode.IN_PORT_NAME);
        // ItemHeld outcome moves to chase state - See below chaseState definition
        // ItemNotFound outcome exits the graph with GunNotFound
        AddExitConnection(ShootGraphExitConnection.GunNotFound,
            getShootRoleState, nameof(HoldItemGateStateOutcome.ItemNotFound), "I can't find my gun.");

        // Construct the chase state as a follow state that exits once within both distance and line of sight
        FollowStateNode chaseState = new(new FollowStateConfiguration()
        {
            TargetingType = FollowStateTargetingType.Transform,
            TargetTransform = new TransformReference(config.TargetInteractable.transform),

            DistanceConfiguration = FollowStateDistanceConfiguration.ExitWhenWithinDistance,
            HorizontalDistanceParameter = Mathf.Max(config.MaxShootDistance - config.DistanceMargin, 1),
            VerticalDistanceParameter = Mathf.Max(config.MaxShootDistance - config.DistanceMargin, 1),
            
            MaxDuration = config.MaxChaseDuration,
            
            LoSConfiguration = FollowStateLoSConfiguration.ExitWhenWithinLoS,
            MaxDurationWithoutLoS = config.MaxChaseDurationWithoutLoS,  // But still exit if LoS is lost for too long
            
            LoSObstacleLayerMask = LayerMask.GetMask("Default")
        });
        // Connect from the getShootRoleState to the chase state
        graph.ConnectStateFlow(getShootRoleState, nameof(HoldItemGateStateOutcome.ItemHeld), chaseState, StateNode.IN_PORT_NAME);
        // Completed outcome moves to shoot state - See below shootState definition
        // Timeout outcome exits the graph with LostTarget
        AddExitConnection(ShootGraphExitConnection.LostTarget,
            chaseState, nameof(FollowStateOutcome.Timeout), "I lost the target.");
        // RoleDoorFailed outcome exits the graph with ChaseRoleDoorFailed
        AddExitConnection(ShootGraphExitConnection.ChaseRoleDoorFailed,
            chaseState, nameof(FollowStateOutcome.RoleDoorFailed), "Damn, blocked.");
        // MovementManagerError outcome exits the graph with GenericError
        AddExitConnection(ShootGraphExitConnection.MovementManagerError,
            chaseState, nameof(FollowStateOutcome.MovementManagerError), "Something went wrong while moving.");
        
        InteractionStateNode shootState = new(new InteractionStateConfiguration(config.TargetInteractable.gameObject, config.ShootInteractionDefinition));
        // Connect from the chase state to the shoot state
        graph.ConnectStateFlow(chaseState, nameof(FollowStateOutcome.Completed), shootState, StateNode.IN_PORT_NAME);
        // ProximityCheckFailed moves back to chase state
        graph.ConnectStateFlow(shootState, nameof(InteractionStateOutcome.ProximityCheckFailed), chaseState, StateNode.IN_PORT_NAME);
        // CompletedInteraction outcome exits the graph with ShootCompleted
        AddExitConnection(ShootGraphExitConnection.ShootCompleted,
            shootState, nameof(InteractionStateOutcome.CompletedInteraction));
        // RoleCheckFailed outcome exits the graph with GenericError
        AddExitConnection(ShootGraphExitConnection.TriedToShootWithoutRole,
            shootState, nameof(InteractionStateOutcome.RoleCheckFailed), "Where'd my gun go?");
        // Error outcome exits the graph with GenericError
        AddExitConnection(ShootGraphExitConnection.InteractionErrorGeneric,
            shootState, nameof(InteractionStateOutcome.Error), "Something went wrong while shooting.");
    }
}