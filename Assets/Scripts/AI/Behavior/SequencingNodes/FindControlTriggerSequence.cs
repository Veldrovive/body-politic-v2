using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using Composite = Unity.Behavior.Composite;
using Unity.Properties;

[BlackboardEnum]
public enum InteractableType
{
    Interactable,
    Holdable,
    Consumable
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "FindControlTrigger", story: "Find [Type] in [FeasibleAreaGO] near [Self]", category: "Flow", id: "3c046a617cc2107fbde4fb403e3e4d1b")]
public partial class FindControlTriggerSequence : Composite
{
    [SerializeReference] public BlackboardVariable<InteractableType> Type;
    [SerializeReference] public BlackboardVariable<GameObject> FeasibleAreaGO;
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    
    [SerializeReference] public BlackboardVariable<GameObject> FoundGameObject = new(null);
    [SerializeReference] public BlackboardVariable<string> ControlTriggerLayerName = new("PlayerControlTrigger");
    
    [SerializeReference] public Node Found;
    [SerializeReference] public Node NotFound;
    
    protected override Status OnStart()
    {
        // BUG IN UNITY BEHAVIOR: Nodes are not properly deserialized so we need to grab them from the children.
        Found = Children[0];
        NotFound = Children[1];
        
        // Find the collider that we will be checking
        if (FeasibleAreaGO == null)
        {
            Debug.LogError("FindControlTriggerGateState: FeasibleAreaGO is null");
            FoundGameObject.Value = null;
            return StartNode(NotFound);
        }
        Collider feasibleArea = FeasibleAreaGO.Value.GetComponent<Collider>();
        if (feasibleArea == null)
        {
            Debug.LogWarning($"Zone for find control trigger state on {FeasibleAreaGO.Value.name} is null.");
            FoundGameObject.Value = null;
            return StartNode(NotFound);
        }
        else if (!feasibleArea.isTrigger)
        {
            Debug.LogWarning($"Zone for find control trigger state on {FeasibleAreaGO.Value.name} is not a trigger collider.");
            FoundGameObject.Value = null;
            return StartNode(NotFound);
        }
        else if (feasibleArea.GetType() != typeof(BoxCollider))
        {
            Debug.LogWarning($"Zone for find control trigger state on {FeasibleAreaGO.Value.name} should be a BoxCollider.");
            // This isn't actually an error. The physics overlap will just not be the right shape.
        }
        
        // Find the layermask of the control trigger layer
        LayerMask layerMask;
        int layerId = LayerMask.NameToLayer(ControlTriggerLayerName);
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
            Debug.LogWarning($"Layer '{ControlTriggerLayerName}' not found. Please ensure the layer exists in the Tag Manager.");
            layerMask = LayerMask.NameToLayer("Default");
        }
        
        // Search for control triggers within the feasible zone
        HashSet<PlayerControlTrigger> triggers = SearchFeasibleZoneForTriggers(feasibleArea, layerMask);
        
        // Filter down to those player control triggers that are of the correct type
        List<Interactable> interactables = triggers.Where(trigger =>
        {
            // Check if the interactable can be cast to the target type
            if (trigger.TargetInteractable as Consumable)
            {
                // We have to check in this order because Consumables are also Holdables, but if we specify Holdable then
                // we want to exclude consumables.
                return Type == InteractableType.Consumable;
            }
            else if (trigger.TargetInteractable as Holdable)
            {
                // Similarly, Holdables are Interactables so we have to check this first.
                return Type == InteractableType.Holdable;
            }
            else if (trigger.TargetInteractable as Interactable)
            {
                return Type == InteractableType.Interactable;
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
            // interactableVariable.Value = null; 
            // TriggerExit(FindControlTriggerGateStateOutcome.TriggerNotFound);
            FoundGameObject.Value = null;
            return StartNode(NotFound);
        }
        else // interactables.Count > 0
        {
            // At least one interactable of the correct type was found.
            // We need to find the one closest to the NPC operating this state.
            Interactable closestInteractable = interactables
                .OrderBy(interactable => Vector3.Distance(interactable.transform.position, Self.Value.transform.position))
                .First(); // .First() is safe here because interactables.Count > 0 is established.
            
            // As per the requirement, always set the interactable variable if one (or more) is found.
            // This makes the found item available to subsequent states even if this state "fails" to skip.
            // FoundGameObject = closestInteractable.gameObject;
            if (FoundGameObject != null)
            {
                FoundGameObject.Value = closestInteractable.gameObject;
            }
            // else: This node is just telling us that a game object was found, but is not providing a reference to it.
            // TriggerExit(FindControlTriggerGateStateOutcome.TriggerFound);
            return StartNode(Found);
        }
    }
    
    /// <summary>
    /// Searches the feasible zone for any GameObjects that have a PlayerControlTrigger component.
    /// </summary>
    /// <returns>A HashSet of PlayerControlTrigger components found within the zone.</returns>
    private HashSet<PlayerControlTrigger> SearchFeasibleZoneForTriggers(Collider feasibleArea, LayerMask layerMask)
    {
        HashSet<PlayerControlTrigger> foundTriggers = new HashSet<PlayerControlTrigger>();

        // Perform an overlap check within the bounds of the feasibleZone.
        // Physics.OverlapBox is used here as it works well with any Collider type by using its bounds.
        // The feasibleZone's rotation is also taken into account.
        Collider[] hitColliders = Physics.OverlapBox(
            feasibleArea.bounds.center, 
            feasibleArea.bounds.extents, 
            feasibleArea.transform.rotation, 
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

