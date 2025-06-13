using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class HoldItemGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(HoldItemGateState);
    
    /// <summary>
    /// Specify the exact item to hold in the hand.
    /// </summary>
    public GameObjectReference ItemToHold = new GameObjectReference((GameObject)null);
    
    /// <summary>
    /// Specify the desired role that is desired for the NPC holding the item.
    /// </summary>
    public NpcRoleSO HeldRole = null;
}

public enum HoldItemGateStateOutcome
{
    ItemHeld,
    ItemNotFound
}

public class HoldItemGateState : GenericAbstractState<HoldItemGateStateOutcome, HoldItemGateStateConfiguration>
{

    [SerializeField] private GameObject itemToHold;

    private GameObject FindItemToHold(HoldItemGateStateConfiguration config)
    {
        InventoryData invData = npcContext.Inventory.GetInventoryData();
        
        // Since we basically have filters, we can start with a list of all potential items and narrow it down
        // until we have only items that match the criteria.
        List<GameObject> potentialItems = new List<GameObject>();
        if (invData.HeldItem != null)
        {
            potentialItems.Add(invData.HeldItem.gameObject);
        }
        potentialItems.AddRange(invData.InventorySlots.Select(holdableSlot => holdableSlot.gameObject));
        
        if (config.ItemToHold.Value != null)
        {
            // Then we are looking for a specific item
            potentialItems = potentialItems
                .Where(item => item == config.ItemToHold.Value)
                .ToList();
        }

        if (config.HeldRole != null)
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
                    return holdableDefinition.HeldRole == config.HeldRole;
                }).ToList();
        }

        if (potentialItems.Count == 0)
        {
            return null;
        }
        return potentialItems[0];
    }
    
    public override void ConfigureState(HoldItemGateStateConfiguration config)
    {
        itemToHold = FindItemToHold(config);
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        if (itemToHold == null)
        {
            Debug.LogWarning($"Game object reference for {gameObject.name} is missing.");
            TriggerExit(HoldItemGateStateOutcome.ItemNotFound);
            return;
        }
        if (itemToHold == null)
        {
            Debug.LogWarning($"Game object reference for {gameObject.name} is null.");
            TriggerExit(HoldItemGateStateOutcome.ItemNotFound);
            return;
        }
        
        // Check if the item is already in out hand
        InventoryData invData = npcContext.Inventory.GetInventoryData();
        if (invData.HeldItem?.gameObject == itemToHold)
        {
            // The item is already held so we can exit the state
            TriggerExit(HoldItemGateStateOutcome.ItemHeld);
            return;
        }
        
        // Otherwise we need to check through the inventory slots and see if it is there
        foreach (Holdable inventoryItem in invData.InventorySlots)
        {
            if (inventoryItem?.gameObject == itemToHold)
            {
                // We found it. We now need to put it into the hand
                Holdable heldItem = npcContext.Inventory.TryRetrieveItem(inventoryItem);
                if (heldItem != null)
                {
                    // The item is now in the hand
                    TriggerExit(HoldItemGateStateOutcome.ItemHeld);
                    return;
                }
                else
                {
                    // Something failed. This is probably a bug as I currently have no way for this to happen.
                    Debug.LogWarning($"Tried to bring {inventoryItem?.name} to hand, but it failed.");
                    TriggerExit(HoldItemGateStateOutcome.ItemNotFound);
                    return;
                }
            }
        }
        
        // We looked through the inventory and didn't find the item. This is a failure.
        TriggerExit(HoldItemGateStateOutcome.ItemNotFound);
        return;
    }
}