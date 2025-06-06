using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; // For Linq operations

/// <summary>
/// Package to hold the data for an entire inventory. Used for UI mostly.
/// </summary>
public class InventoryData
{
    public int InventorySize { get; private set; }
    public Holdable HeldItem { get; private set; }
    public List<Holdable> InventorySlots { get; private set; }
    public InventoryData(int inventorySize, Holdable heldItem, List<Holdable> inventorySlots)
    {
        InventorySize = inventorySize;
        HeldItem = heldItem;
        InventorySlots = inventorySlots;
    }
}

public class NpcInventorySaveableData : SaveableData
{
    public string HeldItemProducerId;  // References the saveable ID of the held item, if any.
    public List<string> InventorySlotsProducerIds;  // References the saveable IDs of the items in the inventory.
}

/// <summary>
/// Manages the held item and inventory slots for an NPC.
/// Acts as the IRoleProvider for roles conferred by items.
/// </summary>
public class NpcInventory : SaveableGOConsumer, IRoleProvider
{
    [Header("Inventory Settings")]
    [Tooltip("Maximum number of items that can be stored in the inventory slots (excluding the held item).")]
    [SerializeField] private int inventoryCapacity = 5;

    [Header("Attachment Points")]
    [Tooltip("The default transform where held items will be attached visually.")]
    [SerializeField] private Transform _handAttachPoint; // Use backing field for SerializeField

    // --- Runtime State ---
    private Holdable _heldItem = null;
    private List<Holdable> _inventorySlots = new List<Holdable>();

    // --- Role Provider Cache ---
    private HashSet<NpcRoleSO> _currentProvidedRoles = new HashSet<NpcRoleSO>();
    private bool _isCacheDirty = true;

    // --- IRoleProvider Events ---
    /// <summary>
    /// Fired when the potential set of provided roles may have increased.
    /// Consumers should call GetCurrentRoles() to refresh. Argument may be null for general change.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleAdded;
    /// <summary>
    /// Fired when the potential set of provided roles may have decreased.
    /// Consumers should call GetCurrentRoles() to refresh. Argument may be null for general change.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleRemoved;

    public event Action<InventoryData> OnInventoryChanged;
    public InventoryData GetInventoryData()
    {
        return new InventoryData(inventoryCapacity, _heldItem, _inventorySlots);
    }
    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke(GetInventoryData());
    }

    /// <summary>
    /// Gets the save data for this object.
    /// </summary>
    /// <returns>The save data.</returns>
    public override SaveableData GetSaveData()
    {
        if (SaveableDataManager.Instance == null)
        {
            throw new InvalidOperationException("ResourceDataManager is not initialized. Cannot get save data.");
        }
        
        string heldItemProducerId = _heldItem?.GetProducerId();
        List<string> inventorySlotsProduderIds = _inventorySlots
            .Select(item => item?.GetProducerId())
            .Where(id => !string.IsNullOrEmpty(id)) // Filter out null or empty IDs
            .ToList();

        return new NpcInventorySaveableData()
        {
            // HeldItemSaveableId = HeldItemSaveableId,
            // InventorySlotsSaveableIds = InventorySlotsSaveableIds
            HeldItemProducerId = heldItemProducerId,
            InventorySlotsProducerIds = inventorySlotsProduderIds
        };
    }

    /// <summary>
    /// Sets the save data for this object.
    /// To do this we need to get the gameobjects corresponding to the saveable IDs from the ResourceDataManager
    /// and try to acquire each of them.
    /// To do this, we need to call `HandlePickUp` on the Holdable component of the gameobject with a constructed
    /// InteractionContext that has the NPC gameobject as the initiator.
    /// </summary>
    /// <param name="data">The save data to set.</param>
    public override void LoadSaveData(SaveableData data)
    {
        if (data is not NpcInventorySaveableData npcData)
        {
            Debug.LogError($"NpcInventory: LoadSaveData received data of type {data.GetType().Name}, expected NpcInventorySaveableData.", this);
            return;
        }
        
        if (SaveableDataManager.Instance == null)
        {
            throw new InvalidOperationException("ResourceDataManager is not initialized. Cannot set save data.");
        }

        void TryPickUpItem(Holdable item)
        {
            InteractionContext dummyContext = new(
                this.gameObject,
                item,
                item.GetPickUpDefinition()
            );
            // Attempt to acquire the item, which will handle the visual attachment and role updates.
            item.HandlePickUp(dummyContext);
        }
        
        // If there is a held item, acquire it first so that it goes into the hand slot.
        if (!string.IsNullOrEmpty(npcData.HeldItemProducerId))
        {
            GameObject heldItemGO = SaveableDataManager.Instance.GetProducerObject(npcData.HeldItemProducerId);
            Holdable heldItem = heldItemGO?.GetComponent<Holdable>();
            if (heldItem == null)
            {
                Debug.LogWarning("NpcInventory: SetSaveData could not find held item with ID " + npcData.HeldItemProducerId, this);
            }
            else
            {
                TryPickUpItem(heldItem);
            }
        }
        
        // Then we can loop through the inventory slots and acquire each item.
        foreach (string producerId in npcData.InventorySlotsProducerIds)
        {
            GameObject holdableGO = SaveableDataManager.Instance.GetProducerObject(producerId);
            Holdable item = holdableGO?.GetComponent<Holdable>();
            if (item == null)
            {
                Debug.LogWarning($"NpcInventory: SetSaveData could not find inventory item with ID {producerId}", this);
            }
            else
            {
                TryPickUpItem(item);
            }
        }
        
        // And finally if the currently held item is not the same as the one in the save data, we store it in the inventory.
        // This will happen when there is no held item as the first inventory slot will then go into the hand instead.
        if (_heldItem != null && _heldItem.GetProducerId() != npcData.HeldItemProducerId)
        {
            // Store the held item in the inventory
            TryStoreHeldItem();
        }
    }

    /// <summary>
    /// Initializes the inventory slots list.
    /// </summary>
    void Awake()
    {
        _inventorySlots = new List<Holdable>(inventoryCapacity);

        if (_handAttachPoint == null)
        {
            Debug.LogWarning($"NpcInventory on {gameObject.name} does not have a Hand Attach Point assigned. Held items may not attach correctly.", this);
        }
    }

    /// <summary>
    /// Helper method to signal potential role changes to consumers.
    /// Marks the cache dirty and fires the required IRoleProvider events.
    /// </summary>
    private void NotifyPotentialRoleChange()
    {
        _isCacheDirty = true;
        // Fire both events with null to indicate a general change,
        // prompting NPCIdentity to call GetCurrentRoles().
        OnRoleAdded?.Invoke(null);
        OnRoleRemoved?.Invoke(null);
    }


    /// <summary>
    /// Attempts to acquire a holdable item, placing it either in the hand or inventory.
    /// </summary>
    /// <param name="item">The Holdable item instance to acquire.</param>
    /// <param name="initiator">The GameObject acquiring the item (used for context, should be this NPC).</param>
    /// <returns>A tuple indicating: success, if the item was placed in hand (true) or inventory (false), and the attach point used.</returns>
    public (bool success, bool wasHeld, Transform handAttachPoint) TryAcquireItem(Holdable item, GameObject initiator)
    {
        if (item == null)
        {
            Debug.LogError($"NpcInventory on {gameObject.name} received null item in TryAcquireItem.", this);
            return (false, false, null);
        }
        // Allow pickup even if initiator isn't self, maybe log warning
        // if (initiator != this.gameObject) { ... }

        // --- Determine Placement: Hand or Inventory ---
        if (_heldItem == null)
        {
            // Hand slot is free
            _heldItem = item;
            NotifyPotentialRoleChange(); // Signal change *before* Holdable updates visuals
            NotifyInventoryChanged(); // Notify UI of inventory change
            return (true, true, _handAttachPoint); // Success, was held
        }
        else if (_inventorySlots.Count < inventoryCapacity)
        {
            // Inventory has space
            _inventorySlots.Add(item);
            NotifyPotentialRoleChange(); // Signal change
            NotifyInventoryChanged(); // Notify UI of inventory change
            return (true, false, null); // Success, was not held (in inventory)
        }
        else
        {
            // No space available
            return (false, false, null); // Failure
        }
    }

    /// <summary>
    /// Releases the item currently held in the hand.
    /// </summary>
    /// <returns>The Holdable item that was released, or null if no item was held.</returns>
    public Holdable ReleaseHeldItem()
    {
        if (_heldItem != null)
        {
            Holdable releasedItem = _heldItem;
            _heldItem = null;
            NotifyPotentialRoleChange(); // Signal change
            NotifyInventoryChanged(); // Notify UI of inventory change
            return releasedItem;
        }
        else
        {
            return null; // Nothing held
        }
    }

    // --- Optional Methods from Design Doc ---

    /// <summary>
    /// Attempts to move the currently held item into an inventory slot if space is available.
    /// </summary>
    /// <returns>True if the item was successfully stored, false otherwise.</returns>
    public bool TryStoreHeldItem()
    {
        if (_heldItem == null) return false; // Nothing to store

        if (_inventorySlots.Count < inventoryCapacity)
        {
            Holdable itemToStore = _heldItem;
            _heldItem = null;
            _inventorySlots.Add(itemToStore);

            // Update visual state AFTER internal state change but BEFORE notification
            itemToStore.SetVisualState(HoldableVisualState.InInventory);

            NotifyPotentialRoleChange(); // Roles might change
            NotifyInventoryChanged(); // Notify UI of inventory change
            return true;
        }
        else
        {
            return false; // Inventory full
        }
    }

    /// <summary>
    /// Attempts to retrieve an item from a specific inventory slot and place it in the hand.
    /// Behavior depends on whether the hand is already occupied and the value of storeHeldFirst:
    /// - If hand is empty: Retrieves the item.
    /// - If hand is occupied and storeHeldFirst is false: Fails.
    /// - If hand is occupied and storeHeldFirst is true:
    ///     - If inventory has space: Stores the currently held item and retrieves the target item.
    ///     - If inventory is full: Swaps the currently held item with the target item in the slot.
    /// </summary>
    /// <param name="slotIndex">The index of the inventory slot to retrieve from.</param>
    /// <param name="storeHeldFirst">If true, allows storing or swapping the held item to make space. If false, requires the hand to be empty.</param>
    /// <returns>The Holdable item retrieved and now held, or null if retrieval failed.</returns>
    public Holdable TryRetrieveItem(int slotIndex, bool storeHeldFirst = false)
    {
        // --- Validate Input ---
        if (slotIndex < 0 || slotIndex >= _inventorySlots.Count || _inventorySlots[slotIndex] == null)
        {
            Debug.LogWarning($"NpcInventory: TryRetrieveItem called with invalid slotIndex {slotIndex}. Inventory count: {_inventorySlots.Count}", this);
            return null; // Invalid index or empty slot
        }

        Holdable itemToRetrieve = _inventorySlots[slotIndex];
        Holdable currentHeldItem = _heldItem;

        // --- Pre-condition Check: Hand occupied and not allowed to store/swap ---
        if (currentHeldItem != null && !storeHeldFirst)
        {
            // Hand is full, and we are explicitly told not to store the held item first.
            // Debug.Log($"NpcInventory: TryRetrieveItem failed. Hand is full and storeHeldFirst is false.", this);
            return null;
        }

        // --- Handle Retrieval Logic ---
        if (currentHeldItem == null)
        {
            // Hand is empty, simple retrieval (works regardless of storeHeldFirst)
            _inventorySlots.RemoveAt(slotIndex); // Remove from inventory list
            _heldItem = itemToRetrieve;          // Place it in the hand

            // Update visual state AFTER internal state change
            itemToRetrieve.SetVisualState(HoldableVisualState.InHand, _handAttachPoint); // Now held
        }
        else // Hand is occupied AND storeHeldFirst is true
        {
            bool canStore = _inventorySlots.Count < inventoryCapacity;

            if (canStore)
            {
                // Store held item first (inventory has space)
                _inventorySlots.RemoveAt(slotIndex);    // Remove the item we are retrieving (important to do before adding, in case count = capacity - 1)
                _inventorySlots.Add(currentHeldItem);   // Add the currently held item to the end of inventory
                _heldItem = itemToRetrieve;             // Put the retrieved item in the hand

                // Update visual states AFTER internal state changes
                currentHeldItem.SetVisualState(HoldableVisualState.InInventory); // Old held item visually moves to inventory slot
                itemToRetrieve.SetVisualState(HoldableVisualState.InHand, _handAttachPoint); // New held item visually moves to hand
            }
            else
            {
                // Swap items (Inventory is full, but storeHeldFirst allowed the attempt)
                _inventorySlots[slotIndex] = currentHeldItem; // Put the currently held item into the target slot
                _heldItem = itemToRetrieve;                   // Put the retrieved item into the hand

                // Update visual states AFTER internal state changes
                currentHeldItem.SetVisualState(HoldableVisualState.InInventory); // Old held item visually moves to inventory slot
                itemToRetrieve.SetVisualState(HoldableVisualState.InHand, _handAttachPoint); // New held item visually moves to hand
            }
        }

        // --- Notify ---
        // Notify consumers about potential role changes and general inventory update
        NotifyPotentialRoleChange();
        NotifyInventoryChanged();

        return itemToRetrieve; // Return the item that is now held
    }

    /// <summary>
    /// Attempts to retrieve a specific item from the inventory and place it in the hand.
    /// Finds the item in the inventory slots first.
    /// If an item is already held, behavior depends on storeHeldFirst (see overload).
    /// </summary>
    /// <param name="item">The specific Holdable instance to retrieve.</param>
    /// <param name="storeHeldFirst">If true, attempt to store the currently held item first if space allows, otherwise swap.</param>
    /// <returns>The Holdable item retrieved and now held, or null if the item wasn't found or retrieval failed.</returns>
    public Holdable TryRetrieveItem(Holdable item, bool storeHeldFirst = false)
    {
        if (item == null) return null; // Cannot retrieve a null item

        // Find the index of the item in the inventory
        int slotIndex = _inventorySlots.IndexOf(item);

        if (slotIndex == -1)
        {
            Debug.LogWarning($"NpcInventory: TryRetrieveItem failed. Item {item.name} not found in inventory.", this);
            return null; // Item not found in inventory slots
        }

        // Call the index-based overload to perform the actual retrieval/swap logic
        return TryRetrieveItem(slotIndex, storeHeldFirst);
    }

    public bool HasItemInInventory(Interactable item)
    {
        return _inventorySlots.Contains(item);
    }

    public bool HasItemInHand(Interactable item)
    {
        return _heldItem == item;
    }

    // --- IRoleProvider Implementation ---

    /// <summary>
    /// Gets the collection of roles currently provided by the items in this inventory (held or stored).
    /// Recalculates and caches roles if necessary. This method does NOT fire events.
    /// </summary>
    /// <returns>A read-only collection of NpcRoleSO provided by items.</returns>
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles()
    {
        if (_isCacheDirty)
        {
            // --- Recalculate Roles ---
            // No comparison or event firing needed here, just calculate the current state.
            _currentProvidedRoles.Clear();

            // Add role from held item
            if (_heldItem != null)
            {
                HoldableDefinitionSO heldDef = _heldItem.GetItemDefinition();
                if (heldDef != null && heldDef.HeldRole != null)
                {
                    _currentProvidedRoles.Add(heldDef.HeldRole);
                }
            }

            // Add roles from items in inventory slots
            foreach (Holdable itemInSlot in _inventorySlots)
            {
                if (itemInSlot != null)
                {
                    HoldableDefinitionSO slotDef = itemInSlot.GetItemDefinition();
                    if (slotDef != null && slotDef.InventoryRole != null)
                    {
                        _currentProvidedRoles.Add(slotDef.InventoryRole);
                    }
                }
            }

            // --- Mark Cache as Clean ---
            _isCacheDirty = false;
            // Debug.Log($"Inventory Roles Recalculated for {gameObject.name}. Count: {_currentProvidedRoles.Count}");
        }
        // Return the cached set
        return _currentProvidedRoles;
    }
}