

using UnityEngine;

public class MoveAndUseGraphConfiguration : AbstractGraphFactoryConfig
{
    public Interactable TargetInteractable;
    public InteractionDefinitionSO TargetInteractionDefinition;
    public Transform MoveToTargetTransform;
    public bool RequireExactPosition;
    public bool RequireFinalAlignment;

    public ActionCamSource ActionCamConfig = null;
}

public enum MoveAndUseGraphExitConnection
{
    InteractionErrorGeneric,
    InteractionErrorProximityCheckFailed,
    InteractionErrorRoleCheckFailed,
    InteractionCompleted,
    
    MoveErrorDoorRoleFailed,
    MoveError
}

public class MoveAndUseGraphFactory : GenericAbstractGraphFactory<MoveAndUseGraphConfiguration, MoveAndUseGraphExitConnection>
{
    public MoveAndUseGraphFactory(MoveAndUseGraphConfiguration configuration, string graphId = null) : base(
        configuration, graphId)
    {
    }
    
    protected override void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint)
    {
        InteractionStateNode interactionStateNode = new(new InteractionStateConfiguration(config.TargetInteractable.gameObject, config.TargetInteractionDefinition));
        // InteractionState failures
        AddExitConnection(MoveAndUseGraphExitConnection.InteractionErrorGeneric,
            interactionStateNode, nameof(InteractionStateOutcome.Error), "Something went wrong.");
        AddExitConnection(MoveAndUseGraphExitConnection.InteractionErrorProximityCheckFailed,
            interactionStateNode, nameof(InteractionStateOutcome.ProximityCheckFailed), "I can't reach that.");
        AddExitConnection(MoveAndUseGraphExitConnection.InteractionErrorRoleCheckFailed,
            interactionStateNode, nameof(InteractionStateOutcome.RoleCheckFailed), "I can't do that.");
        
        if (config.MoveToTargetTransform == null)
        {
            // Then we don't actually do the MoveToState. We go directly to the interaction state.
            // Connect the main flow "Start -> Interact -> Exit"
            if (config.ActionCamConfig != null)
            {
                // Then we insert an action camera node before we start interacting
                ActionCameraStartStateNode cameraStartNode = new(new ActionCameraStartStateConfiguration(config.ActionCamConfig));
                graph.AddNode(cameraStartNode);
                ActionCameraEndStateNode cameraEndNode = new(new ActionCameraEndStateConfiguration(config.ActionCamConfig.SourceKey));
                graph.AddNode(cameraEndNode);
                
                graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, cameraStartNode, StateNode.IN_PORT_NAME);
                graph.ConnectStateFlow(cameraStartNode, ActionCameraStartStateOutcome.SourceAdded, interactionStateNode);
                graph.ConnectStateFlow(interactionStateNode, InteractionStateOutcome.CompletedInteraction, cameraEndNode);
                AddExitConnection(MoveAndUseGraphExitConnection.InteractionCompleted,
                    cameraEndNode, nameof(InteractionStateOutcome.CompletedInteraction));
            }
            else
            {
                // It's just the interaction
                graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, interactionStateNode, StateNode.IN_PORT_NAME);
                AddExitConnection(MoveAndUseGraphExitConnection.InteractionCompleted,
                    interactionStateNode, nameof(InteractionStateOutcome.CompletedInteraction));
            }
        }
        else
        {
            // Then we also need to construct our MoveToState
            // Create the main action nodes and add them to the graph
            MoveToStateNode moveToStateNode = new(new MoveToStateConfiguration(config.MoveToTargetTransform)
            {
                RequireExactPosition = config.RequireExactPosition,
                RequireFinalAlignment = config.RequireFinalAlignment,
                AcceptanceRadius = 1.0f,
            });
            graph.AddNode(moveToStateNode);
            
            SayRoleMissingListenerNode roleAlertNode = new();
            graph.AddNode(roleAlertNode);
            graph.ConnectEvent(moveToStateNode, MoveToState.ON_ROLE_DOOR_FAILED_PORT_NAME, roleAlertNode,
                SayRoleMissingListenerNode.SAY_ROLE_MISSING_PORT_NAME);
            
            // MoveToState failures
            AddExitConnection(MoveAndUseGraphExitConnection.MoveErrorDoorRoleFailed,
                moveToStateNode, nameof(MoveToStateOutcome.DoorRoleFailed), "Looks like I can't open that door.");
            AddExitConnection(MoveAndUseGraphExitConnection.MoveError,
                moveToStateNode, nameof(MoveToStateOutcome.Error), "I can't figure out where I'm going.");
        
            // Connect the main flow
            if (config.ActionCamConfig != null)
            {
                // "Start -> Camera Activate -> MoveTo -> Interact -> Camera End -> Exit"
                ActionCameraStartStateNode cameraStartNode = new(new ActionCameraStartStateConfiguration(config.ActionCamConfig));
                graph.AddNode(cameraStartNode);
                ActionCameraEndStateNode cameraEndNode = new(new ActionCameraEndStateConfiguration(config.ActionCamConfig.SourceKey));
                graph.AddNode(cameraEndNode);
                
                graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, cameraStartNode, StateNode.IN_PORT_NAME);
                graph.ConnectStateFlow(cameraStartNode, ActionCameraStartStateOutcome.SourceAdded, moveToStateNode);
                graph.ConnectStateFlow(moveToStateNode, MoveToStateOutcome.Arrived, interactionStateNode);
                graph.ConnectStateFlow(interactionStateNode, InteractionStateOutcome.CompletedInteraction, cameraEndNode);
                AddExitConnection(MoveAndUseGraphExitConnection.InteractionCompleted,
                    cameraEndNode, nameof(InteractionStateOutcome.CompletedInteraction));
            }
            else
            {
                // "Start -> MoveTo -> Interact -> Exit"
                graph.ConnectStateFlow(startPoint.GraphNode, startPoint.PortName, moveToStateNode, StateNode.IN_PORT_NAME);
                graph.ConnectStateFlow(moveToStateNode, MoveToStateOutcome.Arrived, interactionStateNode);
                AddExitConnection(MoveAndUseGraphExitConnection.InteractionCompleted,
                    interactionStateNode, nameof(InteractionStateOutcome.CompletedInteraction));
            }
        }
    }

}