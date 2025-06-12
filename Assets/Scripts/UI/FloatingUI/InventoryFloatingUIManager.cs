// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UIElements;
//
// public class InventoryFloatingUIConfig : AbstractFloatingUIConfig
// {
//     public InteractableDefinitionSO TargetInteractableDefinition;
//     public Interactable TargetInteractable;
//     public List<PlayerControlTrigger> Triggers;
//     public InteractionMenuInstanceManager InstanceManager;
// }
//
// public class InventoryFloatingUIManager : AbstractFloatingUIManager<InventoryFloatingUIConfig>
// {
//     [SerializeField] [Tooltip("UXML template asset for a single inventory slot")]
//     private VisualTreeAsset slotUITemplate;
//     
//     [SerializeField] private VisualTreeAsset actionButtonTemplate;
//     
//     [SerializeField]
//     [Tooltip("Sprite to show on an inventory item when hovering if it can be transferred to the hand.")]
//     private Sprite transferToHandSprite;
//
//     [SerializeField]
//     [Tooltip("Sprite to show on the held item when hovering if it can be transferred to the inventory.")]
//     private Sprite transferToInventorySprite;
//     
//     
//     private NpcInventory focusedNpcInventory => PlayerManager.Instance.CurrentFocusedNpc?.Inventory;
//     
//     private VisualElement _rootVisualElement;
//     private InventoryData curInventoryData = null;
//     private bool isHoveringInteractableMenu = false;
//
//     protected override bool OnSetupFloater(VisualElement floaterRoot, InventoryFloatingUIConfig floaterConfig)
//     {
//         throw new System.NotImplementedException();
//     }
//
//     protected override void OnUpdateFloater(VisualElement floaterRoot, InventoryFloatingUIConfig floaterConfig)
//     {
//         throw new System.NotImplementedException();
//     }
//
//     protected override void OnRemoveFloater(VisualElement floaterRoot, InventoryFloatingUIConfig floaterConfig)
//     {
//         throw new System.NotImplementedException();
//     }
//
//     protected override void Awake()
//     {
//         base.Awake();
//         
//         _rootVisualElement = uiDocument.rootVisualElement;
//         if (slotUITemplate == null)
//         {
//             Debug.LogError("Inventory UI Manager is missing required UI Document or Template Assets.", this);
//         }
//         if (actionButtonTemplate == null)
//         {
//             Debug.LogError("Inventory UI Manager is missing required Action Button Template Asset.", this);
//         }
//         
//         // Initially hide the inventory UI
//         _rootVisualElement.style.display = DisplayStyle.None;
//         
//         PlayerManager.Instance.OnFocusChanged += HandleFocusedNpcChange;
//         if (PlayerManager.Instance.CurrentFocusedNpc != null)
//         {
//             HandleFocusedNpcChange(null, PlayerManager.Instance.CurrentFocusedNpc);
//         }
//     }
//     
//     void OnDestroy()
//     {
//         // Unsubscribe from events to prevent memory leaks
//         if (PlayerManager.Instance != null)
//         {
//             PlayerManager.Instance.OnFocusChanged -= HandleFocusedNpcChange;
//         }
//         // Clean up any open menus if the object is destroyed
//         RemoveControlMenus();
//     }
//
//     /// <summary>
//     /// Handles the change in the NPC the player is focused on. Updates the inventory display accordingly.
//     /// </summary>
//     /// <param name="previousFocusedNpcContext">The context of the previously focused NPC.</param>
//     /// <param name="npcContext">The context of the newly focused NPC.</param>
//     void HandleFocusedNpcChange(NpcContext previousFocusedNpcContext, NpcContext npcContext)
//     {
//         // Unsubscribe from the previous inventory's events
//         if (focusedNpcInventory != null)
//         {
//             focusedNpcInventory.OnInventoryChanged -= HandleInventoryUpdated;
//         }
//         
//         if (focusedNpcInventory != null)
//         {
//             focusedNpcInventory.OnInventoryChanged += HandleInventoryUpdated;
//             // Immediately update the UI with the new inventory data
//             HandleInventoryUpdated(focusedNpcInventory.GetInventoryData());
//             // Show the inventory UI
//             _rootVisualElement.style.display = DisplayStyle.Flex;
//         }
//         else
//         {
//             // If no NPC is focused or the focused NPC has no inventory, clear and hide the UI
//             ClearInventoryData();
//             BuildUIFromCurrentData(); // Build an empty UI
//             _rootVisualElement.style.display = DisplayStyle.None; // Hide the inventory UI
//         }
//     }
//     
//     /// <summary>
//     /// Retrieves all PlayerControlTrigger components attached to the direct children of a Holdable's GameObject.
//     /// </summary>
//     /// <param name="holdable">The Holdable object whose children's triggers we want to find.</param>
//     /// <returns>A list of PlayerControlTrigger components found on the children.</returns>
//     private List<PlayerControlTrigger> GetHoldableControlTriggers(Holdable holdable)
//     {
//         // Initialize a list to store the triggers we find.
//         List<PlayerControlTrigger> triggers = new List<PlayerControlTrigger>();
//
//         // Check if the provided holdable is null to prevent errors.
//         if (holdable == null)
//         {
//             // This is expected for empty slots, so no warning needed.
//             return triggers; // Return the empty list if holdable is null.
//         }
//
//         // Get the GameObject associated with the Holdable.
//         GameObject holdableGo = holdable.gameObject;
//
//         // Iterate through each direct child transform of the holdable's GameObject.
//         // We iterate through transforms because it's a common way to access children in Unity.
//         foreach (Transform childTransform in holdableGo.transform)
//         {
//             // Try to get the PlayerControlTrigger component from the child's GameObject.
//             // We look specifically on children because the design specifies triggers are located there,
//             // not on the parent Holdable object itself.
//             // if (childTransform.TryGetComponent<PlayerControlTrigger>(out PlayerControlTrigger trigger))
//             // {
//             //     // If a trigger component is found, add it to our list.
//             //     triggers.Add(trigger);
//             // }
//             
//             PlayerControlTrigger[] childTriggers = childTransform.gameObject.GetComponents<PlayerControlTrigger>();
//             triggers.AddRange(childTriggers);
//         }
//
//         // Return the populated list of triggers.
//         return triggers;
//     }
//     
//     /// <summary>
//     /// Clears the internal inventory data structures.
//     /// </summary>
//     private void ClearInventoryData()
//     {
//         // Debug.Log($"Nulling _currentlyHoveredHoldable on clear inventory data.");
//         curInventoryData = null;
//         RemoveControlMenus(); // Ensure menus are closed when data is cleared
//     }
//
//     /// <summary>
//     /// Called when the focused NPC's inventory changes. Updates internal data and rebuilds the UI.
//     /// </summary>
//     /// <param name="inventoryData">The updated inventory data.</param>
//     void HandleInventoryUpdated(InventoryData inventoryData)
//     {
//         curInventoryData = inventoryData;
//         
//         BuildUIFromCurrentData();
//     }
// }