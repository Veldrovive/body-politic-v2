using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PanicBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Panic Behavior Factory")]
public class PanicBehaviorFactory : AbstractCustomActionBehaviorFactory
{
    [Header("Graph Configuration")]
    [SerializeField] private float panicMaxDuration = 20f;
    [Tooltip("If this variable becomes false, the NPC will stop panicking and return to normal behavior.")]
    [SerializeField] private BoolVariableSO shouldContinuePanicking = null;
    [SerializeField] private MovementSpeed desiredSpeed = MovementSpeed.Run;

    [Header("Message Configuration")]
    [SerializeField] private string entryMessage = "Ahh!";
    [SerializeField] private string exitMessage = "";
    [SerializeField] private List<string> panicMessages = new List<string>()
    {
        "Oh, this is bad!",
        "Are they gone?"
    };

    public override InterruptBehaviorDefinition GetInterruptDefinition(CustomActionBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;
        
        GameObject initiatorGO = interruptParameters.InitiatorGO;  // This is the NPC that will panic.
        if (initiatorGO == null)
        {
            Debug.LogError("Initiator GameObject is not set.");
            return null;
        }
        
        NpcContext initiatorNpcContext = initiatorGO.GetComponent<NpcContext>();
        Zone panicZone = initiatorNpcContext?.PanicZone;
        if (panicZone == null)
        {
            Debug.LogError("Panic zone is not set for the initiator NPC.");
            return null;
        }
        
        GameObject targetGO = interruptParameters.TargetGO;  // This is the target that the NPC is panicking about.

        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            {
                { "Panic Zone", panicZone },
                { "Scary Target", targetGO },
                { "Desired Speed", desiredSpeed },
                
                { "Max Panic Duration", panicMaxDuration },
                { "Should Continue Panicking", shouldContinuePanicking },

                { "Entry Message", entryMessage },
                { "Exit Message", exitMessage },
                { "Panic Messages", panicMessages }
            },

            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}