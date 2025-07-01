using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "InspectBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Inspect Behavior Factory")]
public class InspectBehaviorFactory : AbstractCustomActionBehaviorFactory
{
    [Header("Graph Configuration")]
    [SerializeField] private float inspectDistance = 2f;
    [SerializeField] private float inspectMaxDuration = 20f;
    [SerializeField] private MovementSpeed desiredSpeed = MovementSpeed.NpcSpeed;

    [Header("Message Configuration")]
    [SerializeField] private string entryMessage = "Huh?";
    [SerializeField] private string exitMessage = "Fine.";
    [SerializeField] private List<string> inspectMessages = new List<string>()
    {
        "What's going on here?",
        "I'm not comfortable with this.",
    };

    public override InterruptBehaviorDefinition GetInterruptDefinition(CustomActionBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;

        // Internally to the graph we will use a FollowAction to look at the target
        // We can decide at runtime whether to target a GameObject or a position by using the FollowActionTargetingType
        // The custom behavior parameters also include a way to specify the target type so we need to translate from one to the other.
        FollowActionTargetingType targetingType = interruptParameters.TargetType switch
        {
            CustomActionTargetType.GameObject => FollowActionTargetingType.Transform,
            CustomActionTargetType.Position => FollowActionTargetingType.Position,
            _ => throw new System.ArgumentOutOfRangeException(nameof(interruptParameters.TargetType), "Invalid target type for curious behavior.")
        };
        GameObject targetGO = interruptParameters.TargetGO;
        Vector3 targetPosition = interruptParameters.TargetPosition;

        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            {
                { "Target Type", targetingType },
                { "Target GameObject", targetGO },
                { "Target Position", targetPosition },
                
                { "Inspect Distance", inspectDistance },
                { "Max Duration", inspectMaxDuration },
                { "Desired Speed", desiredSpeed },
                
                { "Entry Message", entryMessage },
                { "Exit Message", exitMessage },
                { "Inspect Messages", inspectMessages }
            },

            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}