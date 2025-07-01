using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Composite = Unity.Behavior.Composite;
using Unity.Properties;
using System.Linq;

/// <summary>
/// TODO: Figure out the desired logic. Perhaps I am having this node do too much. It's just useful to have
/// a single node that does a lot to save space in the behavior tree.
/// Just checking if an item is in the inventory is not enough for me.
/// But then if we are doing things like desired role matching that is getting a bit complex.
/// Maybe if you want to do that you need to use a specific node.
/// Or maybe we have a node for each filter type so that they can specialize, but share most of the logic.
/// Like for desired role it depends where you are moving the item to since you get different roles
/// for it being in the hand or in the inventory.
///
/// Maybe we do have two nodes: one that gets a list of items in the inventory that match a filter and one that
/// modifies the inventory. But then we have to create a blackboard variable to pass that info around and that
/// annoying.
/// I do think that combining them is the way to go.
/// Filters are:
/// 1. Targeting a specific GameObject
/// 2. Targeting instances of a prefab
/// 3. Targeting a type (consumable or holdable)
/// 4. Targeting a held role
/// 5. targeting an inventory role
///
/// Movement options are:
/// 1. None - Just puts filtered items into a List<GameObject> variable.
/// 2. MoveToHeld - If any already in held, do nothing. If only in inventory, move to held.
/// 3. MoveToInventory - If any already in inventory, do nothing. If only in held, move to inventory.
///
/// Outcomes are:
/// 1. Held - Item is now in the held slot. Also used if moved to held.
/// 2. Inventory - Item is now in the inventory. Also used if moved to inventory.
/// 3. NotFound - No item found that matches the filter.
/// </summary>


[BlackboardEnum]
public enum InventoryDesiredPosition
{
    Held,
    Inventory
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Check Inventory", story: "[Self] check's inventory", category: "Flow", id: "4d8073b03d390dfb6b93cc2e67d3226b")]
public partial class CheckInventorySequence : Composite
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<List<GameObject>> FoundGameObjects = new(new List<GameObject>());
    [SerializeReference] public BlackboardVariable<GameObject> FoundGameObject;

    [Tooltip("Allows you to move the matching item to the held slot or inventory.")]
    [SerializeReference] public BlackboardVariable<bool> MoveToDesiredPosition;
    [SerializeReference] public BlackboardVariable<InventoryDesiredPosition> DesiredPosition;
    
    [Header("Filters")]
    [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;
    [SerializeReference] public BlackboardVariable<InteractableType> DesiredType;
    [SerializeReference] public BlackboardVariable<HoldableType> DesiredPrefabType;
    [SerializeReference] public BlackboardVariable<NpcRoleSO> DesiredHeldRole;
    [SerializeReference] public BlackboardVariable<NpcRoleSO> DesiredInventoryRole;
    [SerializeReference] public BlackboardVariable<NpcRoleSO> DesiredConsumedRole;

    [SerializeReference] public Node Found;
    [SerializeReference] public Node NotFound;

    private NpcContext selfContext;
    private InventoryData invData;
    
    protected override Status OnStart()
    {
        // BUG IN UNITY BEHAVIOR: Nodes are not properly deserialized so we need to grab them from the children.
        Found = Children[0];
        NotFound = Children[1];
        
        selfContext = Self.Value.GetComponent<NpcContext>();
        if (selfContext == null)
        {
            Debug.LogError($"CheckInventorySequence: Self does not have a NpcContext component.");
            return StartNode(NotFound);
        }

        invData = selfContext.Inventory.GetInventoryData();

        bool heldMeetsFilter = HoldableMeetsFilter(invData.HeldItem);
        List<Holdable> filteredInventorySlots = invData.InventorySlots
            .Where(item => item != null && HoldableMeetsFilter(item)).ToList();

        // Fill in the FoundGameObjects list based on the filters.
        if (FoundGameObjects.Value == null)
        {
            FoundGameObjects.Value = new List<GameObject>();
        }
        else
        {
            FoundGameObjects.Value.Clear();
        }
        
        if (heldMeetsFilter)
        {
            FoundGameObjects.Value.Add(invData.HeldItem.gameObject);
        }
        foreach (Holdable item in filteredInventorySlots)
        {
            if (item != null)
            {
                FoundGameObjects.Value.Add(item.gameObject);
            }
        }

        FoundGameObject.Value = null;
        if (MoveToDesiredPosition)
        {
            bool filterMet = MoveToMeetFilter(invData, heldMeetsFilter, filteredInventorySlots);
            return StartNode(filterMet ?
                // Then we found the desired item and moved it to the desired position
                Found :
                // Then we didn't find the desired item or couldn't move it to the desired position
                NotFound);
        }
        else
        {
            // Then we just check if items are in the desired position
            if (DesiredPosition == InventoryDesiredPosition.Held)
            {
                if (heldMeetsFilter)
                {
                    FoundGameObject.Value = invData.HeldItem.gameObject;
                }
                return StartNode(heldMeetsFilter ?
                    // Then we found the desired item in the held slot
                    Found :
                    // Then we didn't find the desired item in the held slot
                    NotFound);
            }
            else if (DesiredPosition == InventoryDesiredPosition.Inventory)
            {
                if (filteredInventorySlots.Count > 0)
                {
                    FoundGameObject.Value = filteredInventorySlots[0].gameObject;
                }
                return StartNode(filteredInventorySlots.Count > 0 ?
                    // Then we found the desired item in the inventory
                    Found :
                    // Then we didn't find the desired item in the inventory
                    NotFound);
            }
            else
            {
                Debug.LogError($"CheckInventorySequence: DesiredPosition is not set to a valid value: {DesiredPosition.Value}");
                return StartNode(NotFound);
            }
        }
    }

    private bool MoveToMeetFilter(InventoryData inventoryData, bool heldMeetsFilter, List<Holdable> filteredInventorySlots)
    {
        // First we can check if we already meet the filter
        if (DesiredPosition == InventoryDesiredPosition.Held && heldMeetsFilter)
        {
            // We already have the item in the held slot
            FoundGameObject.Value = inventoryData.HeldItem.gameObject;
            return true;
        }
        else if (DesiredPosition == InventoryDesiredPosition.Inventory && filteredInventorySlots.Count > 0)
        {
            // We already have the item in the inventory
            FoundGameObject.Value = filteredInventorySlots[0].gameObject;
            return true;
        }
        
        // Otherwise we can try to move items to meet the filter
        if (DesiredPosition == InventoryDesiredPosition.Held)
        {
            if (filteredInventorySlots.Count == 0)
            {
                // Then there is nothing we can do
                return false;
            }

            // Otherwise we can try to move the first item in the inventory to the held slot
            Holdable itemToMove = filteredInventorySlots[0];
            // We found it. We now need to put it into the hand
            Holdable heldItem = selfContext.Inventory.TryRetrieveItem(itemToMove, storeHeldFirst: true);
            if (heldItem == null)
            {
                // Something failed. This is probably a bug as I currently have no way for this to happen.
                Debug.LogWarning($"Tried to bring {itemToMove?.name} to hand, but it failed.");
                return false;
            }
            else
            {
                // The item is now in the hand
                FoundGameObject.Value = heldItem.gameObject;
                return true;
            }
        }
        else if (DesiredPosition == InventoryDesiredPosition.Inventory)
        {
            if (!heldMeetsFilter)
            {
                // Then there is nothing we can do
                return false;
            }

            // Otherwise, moving the held item to the inventory will meet the filter
            FoundGameObject.Value = inventoryData.HeldItem.gameObject;
            return selfContext.Inventory.TryStoreHeldItem();
        }
        else
        {
            Debug.LogError($"CheckInventorySequence: DesiredPosition is not set to a valid value: {DesiredPosition.Value}");
            return false;
        }
    }

    #region Filtering
    
    private bool HoldableMeetsFilter(Holdable holdable)
    {
        if (holdable == null)
        {
            return false;
        }
        
        NpcRoleSO heldRole = null;
        NpcRoleSO inventoryRole = null;
        NpcRoleSO consumedRole = null;
        
        // Step 1: Determine the type of holdable and get the roles from its definition.
        InteractableType interactableType = InteractableType.Holdable;
        // Check if the holdable is a consumable.
        if (holdable is Consumable consumable)
        {
            interactableType = InteractableType.Consumable;
        }
        HoldableDefinitionSO holdableDefinition = holdable.InteractableDefinition as HoldableDefinitionSO;
        if (holdableDefinition != null)
        {
            heldRole = holdableDefinition.HeldRole;
            inventoryRole = holdableDefinition.InventoryRole;
            if (holdableDefinition is ConsumableDefinitionSO consumableDefinition)
            {
                consumedRole = consumableDefinition.ConsumedRole;
            }
        }
        
        // Step 2: Get the holdable prefab type.
        HoldableType holdablePrefabType;
        if (!SaveableDataManager.Instance.HoldablePrefabTypeMap.TryGetValue(holdable.gameObject, out holdablePrefabType))
        {
            Debug.LogWarning($"CheckInventorySequence: Holdable {holdable.name} does not have a HoldableType defined in the HoldablePrefabTypeMap. Defaulting to None.");
            holdablePrefabType = HoldableType.None;
        }
        
        // Step 3: Check the filters.
        bool meetsTargetGameObject = TargetGameObject.Value == null || holdable.gameObject == TargetGameObject.Value;
        bool meetsDesiredType = DesiredType.Value == InteractableType.Interactable || DesiredType.Value == interactableType;
        bool meetsDesiredPrefabType = DesiredPrefabType.Value == HoldableType.None || DesiredPrefabType.Value == holdablePrefabType;
        bool meetsDesiredHeldRole = DesiredHeldRole.Value == null || heldRole == DesiredHeldRole.Value;
        bool meetsDesiredInventoryRole = DesiredInventoryRole.Value == null || inventoryRole == DesiredInventoryRole.Value;
        bool meetsDesiredConsumedRole = DesiredConsumedRole.Value == null || consumedRole == DesiredConsumedRole.Value;
        
        return meetsTargetGameObject && meetsDesiredType && meetsDesiredPrefabType &&
               meetsDesiredHeldRole && meetsDesiredInventoryRole && meetsDesiredConsumedRole;
    }
    
    #endregion
}

