using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FindControlTriggerGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(FindControlTriggerGateState);
    
    public GameObject FeasibleAreaGO;
    public string LayerName = "PlayerControlTrigger";
    public InteractableType TargetType;
    public GameObjectVariableSO InteractableVariable;
}

public enum FindControlTriggerGateStateOutcome
{
    TriggerFound,
    TriggerNotFound
}

public enum InteractableType
{
    Interactable,
    Holdable,
    Consumable
}

public class FindControlTriggerGateState : GenericAbstractState<FindControlTriggerGateStateOutcome, FindControlTriggerGateStateConfiguration>
{
    [SerializeField] private GameObject feasibleAreaGO;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private InteractableType targetType;
    [SerializeField] private GameObjectVariableSO interactableVariable;

    private Collider feasibleZone;

    public override void ConfigureState(FindControlTriggerGateStateConfiguration config)
    {
        int layerId = LayerMask.NameToLayer(config.LayerName);
        // Check if the layer was found.
        if (layerId != -1)
        {
            // Assign the layer to the LayerMask.
            // The LayerMask value is a bitmask, so we use the bitwise left shift operator
            // to set the bit corresponding to the layerId.
            layerMask = 1 << layerId;
        }
        else
        {
            // Log a warning if the specified layer name does not exist.
            // This helps in debugging if the layer name is misspelled or not yet created.
            Debug.LogWarning($"Layer '{config.LayerName}' not found. Please ensure the layer exists in the Tag Manager.");
        }
        
        feasibleAreaGO = config.FeasibleAreaGO;
        targetType = config.TargetType;
        interactableVariable = config.InteractableVariable;
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        if (feasibleAreaGO == null)
        {
            Debug.LogError("FindControlTriggerGateState: FeasibleAreaGO is null");
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
            return;
        }
        feasibleZone = feasibleAreaGO?.GetComponent<Collider>();
        if (feasibleZone == null)
        {
            Debug.LogWarning($"Zone for find control trigger state on {gameObject.name} is null.");
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
            return;
        }
        else if (!feasibleZone.isTrigger)
        {
            Debug.LogWarning($"Zone for find control trigger state on {gameObject.name} is not a trigger collider.");
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
            return;
        }
        else if (feasibleZone.GetType() != typeof(BoxCollider))
        {
            Debug.LogWarning($"Zone for find control trigger state on {gameObject.name} should be a BoxCollider.");
            // This isn't actually an error. The physics overlap will just not be the right shape.
        }

        if (interactableVariable == null)
        {
            Debug.LogWarning($"Interactable variable for {gameObject.name} is null.");
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
            return;
        }
        
        // Search for control triggers within the feasible zone
        HashSet<PlayerControlTrigger> triggers = SearchFeasibleZoneForTriggers();
        
        // Filter down to those player control triggers that are of the correct type
        List<Interactable> interactables = triggers.Where(trigger =>
        {
            // Check if the interactable can be cast to the target type
            if (trigger.TargetInteractable as Consumable)
            {
                // We have to check in this order because Consumables are also Holdables, but if we specify Holdable then
                // we want to exclude consumables.
                return targetType == InteractableType.Consumable;
            }
            else if (trigger.TargetInteractable as Holdable)
            {
                // Similarly, Holdables are Interactables so we have to check this first.
                return targetType == InteractableType.Holdable;
            }
            else if (trigger.TargetInteractable as Interactable)
            {
                return targetType == InteractableType.Interactable;
            }
            else
            {
                Debug.LogError($"Control trigger on {trigger.gameObject.name} was not on an interactable.",
                    trigger.gameObject);
                return false;
            }
        }).Select(trigger => trigger.TargetInteractable).ToList();

        if (interactables.Count == 0)
        {
            // No interactables of the correct type were found.
            // Ensure the variable is cleared if nothing is found, maintaining a consistent state for the variable.
            interactableVariable.Value = null; 
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
        }
        else // interactables.Count > 0
        {
            // At least one interactable of the correct type was found.
            // We need to find the one closest to the NPC operating this state.
            Interactable closestInteractable = interactables
                .OrderBy(interactable => Vector3.Distance(interactable.transform.position, transform.position))
                .First(); // .First() is safe here because interactables.Count > 0 is established.
            
            // As per the requirement, always set the interactable variable if one (or more) is found.
            // This makes the found item available to subsequent states even if this state "fails" to skip.
            interactableVariable.Value = closestInteractable.gameObject;
            TriggerExit(FindControlTriggerGateStateOutcome.TriggerFound);
        }
    }

    /// <summary>
    /// Searches the feasible zone for any GameObjects that have a PlayerControlTrigger component.
    /// </summary>
    /// <returns>A HashSet of PlayerControlTrigger components found within the zone.</returns>
    private HashSet<PlayerControlTrigger> SearchFeasibleZoneForTriggers()
    {
        HashSet<PlayerControlTrigger> foundTriggers = new HashSet<PlayerControlTrigger>();

        // Perform an overlap check within the bounds of the feasibleZone.
        // Physics.OverlapBox is used here as it works well with any Collider type by using its bounds.
        // The feasibleZone's rotation is also taken into account.
        Collider[] hitColliders = Physics.OverlapBox(
            feasibleZone.bounds.center, 
            feasibleZone.bounds.extents, 
            feasibleZone.transform.rotation, 
            layerMask
        );

        foreach (var hitCollider in hitColliders)
        {
            // Attempt to get the PlayerControlTrigger component from the hit collider's GameObject.
            // We check on the collider's attached Rigidbody or the GameObject itself,
            // as triggers might be on a child GameObject of the main interactable Rigidbody.
            PlayerControlTrigger trigger = hitCollider.GetComponent<PlayerControlTrigger>();
            if (trigger == null && hitCollider.attachedRigidbody != null)
            {
                trigger = hitCollider.attachedRigidbody.GetComponent<PlayerControlTrigger>();
            }
            
            if (trigger != null)
            {
                // If a PlayerControlTrigger is found, add it to our set.
                // HashSet automatically handles duplicates.
                foundTriggers.Add(trigger);
            }
        }
        return foundTriggers;
    }
}