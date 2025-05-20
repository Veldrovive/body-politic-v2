

using UnityEngine;

public class MoveAndUseGraphConfiguration : AbstractGraphFactoryConfig
{
    public Interactable TargetInteractable;
    public InteractionDefinitionSO TargetInteractionDefinition;
    public Transform MoveToTargetTransform;
    public bool RequireExactPosition;
    public bool RequireFinalAlignment;
}

public class MoveAndUseGraphFactory : GenericAbstractGraphFactory<MoveAndUseGraphConfiguration>
{
    public MoveAndUseGraphFactory(MoveAndUseGraphConfiguration configuration, string graphId = null) : base(
        configuration, graphId)
    {
    }
    
    protected override void ConstructGraphInternal(StateGraph graph)
    {
        if (!graph.IsEmpty())
        {
            Debug.LogWarning($"Constructing StateGraph using factory with a non-empty state graph input. May cause duplicate start nodes.");
        }

        InteractionStateNode interactionStateNode = new(new InteractionStateConfiguration(config.TargetInteractable.gameObject, config.TargetInteractionDefinition));
        graph.AddNode(interactionStateNode);
        // InteractionState failures
        AddConnectionThroughSay(graph, interactionStateNode, nameof(InteractionStateOutcome.Error),
            new ExitNode(), ExitNode.IN_PORT_NAME, "Something went wrong.", 3f);
        AddConnectionThroughSay(graph, interactionStateNode, nameof(InteractionStateOutcome.ProximityCheckFailed),
            new ExitNode(), ExitNode.IN_PORT_NAME, "I can't reach that.", 3f);
        AddConnectionThroughSay(graph, interactionStateNode, nameof(InteractionStateOutcome.RoleCheckFailed),
            new ExitNode(), ExitNode.IN_PORT_NAME, "I can't do that.", 3f);
        
        if (config.MoveToTargetTransform == null)
        {
            // Then we don't actually do the MoveToState. We go directly to the interaction state.
            // Connect the main flow "Start -> Interact -> Exit"
            graph.ConnectStateFlow(new StartNode(), interactionStateNode);
            graph.ConnectStateFlow(interactionStateNode, InteractionStateOutcome.CompletedInteraction, new ExitNode());
        }
        else
        {
            // Then we also need to construct our MoveToState
            // Create the main action nodes and add them to the graph
            MoveToStateNode moveToStateNode = new(new MoveToStateConfiguration(config.MoveToTargetTransform)
            {
                RequireExactPosition = config.RequireExactPosition,
                RequireFinalAlignment = config.RequireFinalAlignment
            });
            graph.AddNode(moveToStateNode);
            
            // MoveToState failures
            AddConnectionThroughSay(graph, moveToStateNode, nameof(MoveToStateOutcome.DoorRoleFailed),
                new ExitNode(), ExitNode.IN_PORT_NAME, "Looks like I can't open that door.", 3f);
            AddConnectionThroughSay(graph, moveToStateNode, nameof(MoveToStateOutcome.Error),
                new ExitNode(), ExitNode.IN_PORT_NAME, "I can't figure out where I'm going.", 3f);
        
            // Connect the main flow "Start -> MoveTo -> Interact -> Exit"
            graph.ConnectStateFlow(new StartNode(), moveToStateNode);
            graph.ConnectStateFlow(moveToStateNode, MoveToStateOutcome.Arrived, interactionStateNode);
            graph.ConnectStateFlow(interactionStateNode, InteractionStateOutcome.CompletedInteraction, new ExitNode());
        }
    }

}