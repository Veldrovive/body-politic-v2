using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using Composite = Unity.Behavior.Composite;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "HoldItem", story: "[Self] holds item", category: "Flow", id: "c3e471ddf810c44261cee47973f12cb4")]
public partial class HoldItemSequence : Composite
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Holdable;
    [SerializeReference] public BlackboardVariable<NpcRoleSO> DesiredRole;
    
    [SerializeReference] public Node Held;
    [SerializeReference] public Node NotHeld;
    
    protected override Status OnStart()
    {
        // BUG IN UNITY BEHAVIOR: Nodes are not properly deserialized so we need to grab them from the children.
        Held = Children[0];
        NotHeld = Children[1];

        NpcContext selfContext = Self.Value.GetComponent<NpcContext>();
        if (selfContext == null)
        {
            Debug.LogError($"HoldItemSequence: Self does not have a NpcContext component.");
            return StartNode(NotHeld);
        }

        GameObject itemToHold = FindItemToHold(selfContext, Holdable.Value, DesiredRole.Value);
        if (itemToHold == null)
        {
            Debug.LogWarning($"Game object reference for {Holdable.Value.name} is missing.");
            return StartNode(NotHeld);
        }
        
        // Check if the item is already in out hand
        InventoryData invData = selfContext.Inventory.GetInventoryData();
        if (invData.HeldItem?.gameObject == itemToHold)
        {
            // The item is already held so we can exit the state
            return StartNode(Held);
        }
        
        // Otherwise we need to check through the inventory slots and see if it is there
        foreach (Holdable inventoryItem in invData.InventorySlots)
        {
            if (inventoryItem?.gameObject == itemToHold)
            {
                // We found it. We now need to put it into the hand
                Holdable heldItem = selfContext.Inventory.TryRetrieveItem(inventoryItem, storeHeldFirst: true);
                if (heldItem != null)
                {
                    // The item is now in the hand
                    return StartNode(Held);
                }
                else
                {
                    // Something failed. This is probably a bug as I currently have no way for this to happen.
                    Debug.LogWarning($"Tried to bring {inventoryItem?.name} to hand, but it failed.");
                    return StartNode(NotHeld);
                }
            }
        }
        
        // We looked through the inventory and didn't find the item. This is a failure.
        return StartNode(NotHeld);
    }
    
    private GameObject FindItemToHold(NpcContext selfContext, GameObject itemToHold, NpcRoleSO desiredRole)
    {
        InventoryData invData = selfContext.Inventory.GetInventoryData();
        
        // Since we basically have filters, we can start with a list of all potential items and narrow it down
        // until we have only items that match the criteria.
        List<GameObject> potentialItems = new List<GameObject>();
        if (invData.HeldItem != null)
        {
            potentialItems.Add(invData.HeldItem.gameObject);
        }
        potentialItems.AddRange(invData.InventorySlots.Select(holdableSlot => holdableSlot.gameObject));
        
        if (itemToHold != null)
        {
            // Then we are looking for a specific item
            potentialItems = potentialItems
                .Where(item => item == itemToHold)
                .ToList();
        }

        if (desiredRole != null)
        {
            // Then we need to look on the holdable's InteractableDefinition to see if it has the role
            potentialItems = potentialItems
                .Where(item =>
                {
                    Holdable holdable = item.GetComponent<Holdable>();
                    if (holdable == null)
                    {
                        return false; // Not a holdable item
                    }
                    if (holdable.InteractableDefinition is not HoldableDefinitionSO holdableDefinition)
                    {
                        return false; // Does not give us access to roles
                    }
                    return holdableDefinition.HeldRole == desiredRole;
                }).ToList();
        }

        if (potentialItems.Count == 0)
        {
            return null;
        }
        return potentialItems[0];
    }
}

