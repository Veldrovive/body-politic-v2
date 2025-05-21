using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class HoldItemGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(HoldItemGateState);
    
    public GameObjectReference ItemToHold;
}

public enum HoldItemGateStateOutcome
{
    ItemHeld,
    ItemNotFound
}

public class HoldItemGateState : GenericAbstractState<HoldItemGateStateOutcome, HoldItemGateStateConfiguration>
{

    [SerializeField] private GameObjectReference itemToHold;

    public override void ConfigureState(HoldItemGateStateConfiguration config)
    {
        itemToHold = config.ItemToHold;
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
        if (itemToHold.Value == null)
        {
            Debug.LogWarning($"Game object reference for {gameObject.name} is null.");
            TriggerExit(HoldItemGateStateOutcome.ItemNotFound);
            return;
        }
        
        // Check if the item is already in out hand
        InventoryData invData = npcContext.Inventory.GetInventoryData();
        if (invData.HeldItem?.gameObject == itemToHold.Value)
        {
            // The item is already held so we can exit the state
            TriggerExit(HoldItemGateStateOutcome.ItemHeld);
            return;
        }
        
        // Otherwise we need to check through the inventory slots and see if it is there
        foreach (Holdable inventoryItem in invData.InventorySlots)
        {
            if (inventoryItem?.gameObject == itemToHold.Value)
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