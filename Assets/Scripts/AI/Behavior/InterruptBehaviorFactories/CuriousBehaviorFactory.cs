using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CuriousBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Curious Behavior Factory")]
public class CuriousBehaviorFactory : AbstractCustomActionBehaviorFactory
{
    [Header("Graph Configuration")]
    
    [SerializeField] private float SightDistance = 10f;
    [SerializeField] private float MaxDuration = 10f;
    [SerializeField] private float MaxDurationWithoutLoS = 2f;

    [Header("Message Configuration")]
    [SerializeField] private string entryMessage = "Hmm?";
    [SerializeField] private string exitMessage = "";

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
                { "Targeting Type", targetingType },
                { "Target GameObject", targetGO },
                { "Target Position", targetPosition },
                
                { "Sight Distance", SightDistance },
                { "Max Duration", MaxDuration },
                { "Max Duration Without LoS", MaxDurationWithoutLoS },

                { "Entry Message", entryMessage },
                { "Exit Message", exitMessage }
            },

            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}