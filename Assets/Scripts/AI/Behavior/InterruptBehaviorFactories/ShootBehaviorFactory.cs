using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ShootBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Shoot Behavior Factory")]
public class ShootBehaviorFactory : AbstractCustomActionBehaviorFactory
{
    [FormerlySerializedAs("defaultShootInteraction")] [SerializeField] private InteractionDefinitionSO shootInteraction;
    
    [Header("Shoot Graph Configuration")]
    [SerializeField] private float shootDistanceMargin = 2f;
    [SerializeField] private float chaseDuration = 20f;
    
    [Header("Message Configuration")]
    [SerializeField] private string beforeShootMessage = "Weapons free!";
    [SerializeField] private string noWeaponMessage = "Where'd my gun go?";
    [SerializeField] private string shotMessage = "";  // Empty skips the message
    [SerializeField] private string lostTargetMessage = "Target lost!";
    [SerializeField] private string targetAcquiredMessage = "Target acquired!";

    public override InterruptBehaviorDefinition GetInterruptDefinition(CustomActionBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;
        
        if (shootInteraction == null)
        {
            Debug.LogError("Shoot interaction definition is not set.");
            return null;
        }
        
        float maxShootDistance = shootInteraction.RequiredProximity - shootDistanceMargin;
        NpcRoleSO requiredRole = shootInteraction.RolesCanExecuteNoSuspicion.FirstOrDefault();
        if (shootInteraction.RolesCanExecuteNoSuspicion.Count() > 1)
        {
            Debug.LogWarning("Multiple roles found for interaction definition, using the first one.");
        }

        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            {
                { "Target", interruptParameters.TargetGO },
                { "Max Shoot Distance", maxShootDistance },
                { "Chase Duration", chaseDuration },
                
                { "Before Shoot Message", beforeShootMessage },
                { "No Weapon Message", noWeaponMessage },
                { "Shot Message", shotMessage },
                { "Lost Target Message", lostTargetMessage },
                { "Target Acquired Message", targetAcquiredMessage },
                
                { "Shoot Interaction", shootInteraction },
                { "Required Role", requiredRole }
            },

            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}