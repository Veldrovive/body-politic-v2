using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class PlayerUIManager : MonoBehaviour
{

    private NpcContext focusedNpc;
    private List<NpcContext> controlledNpcs = new List<NpcContext>();
    private PlayerManager playerManager;

    void OnEnable()
    {
        // Ensure the PlayerManager component is present
        playerManager = GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("PlayerManager component not found on this GameObject.", this);
            return;
        }
    }

    void Start()
    {
        playerManager.OnFocusChanged += HandleNpcFocusChanged;
        playerManager.OnControlledNpcsChanged += HandleControlledNpcsChanged;
    }

    void HandleControlledNpcsChanged(List<NpcContext> newControlledNpcs)
    {
        controlledNpcs = newControlledNpcs;
    }

    void HandleNpcFocusChanged(NpcContext oldFocusedNpc, NpcContext newFocusedNpc)
    {
        focusedNpc = newFocusedNpc;
        SetupInventoryEvents(oldFocusedNpc, newFocusedNpc);
    }

    void SetupInventoryEvents(NpcContext oldNpcContext, NpcContext npcContext)
    {
        // Remove events from old npc
        NpcInventory oldInventory = oldNpcContext?.Inventory;
        if (oldInventory != null)
        {
            oldInventory.OnInventoryChanged -= HandleInventoryChanged;
        }

        // Add events to new npc
        NpcInventory newInventory = npcContext?.Inventory;
        if (newInventory != null)
        {
            newInventory.OnInventoryChanged += HandleInventoryChanged;
            HandleInventoryChanged(newInventory?.GetInventoryData());
        }
    }

    void HandleInventoryChanged(InventoryData inventoryData)
    {
        if (inventoryData == null)
        {
            Debug.LogWarning("InventoryData is null. Cannot update UI.", this);
            return;
        }
    }
}
