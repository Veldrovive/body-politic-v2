using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryFloatingUIManager : AbstractFloatingUIManager<InteractionMenuFloatingUIConfig>
{
    [SerializeField] private InteractionMenuVisualDefinitionSO interactionMenuVisualDefinition;
    [SerializeField] [Tooltip("UXML template asset for a single inventory slot")]
    private VisualTreeAsset slotUITemplate;
    
    [SerializeField]
    [Tooltip("Sprite to show on an inventory item when hovering if it can be transferred to the hand.")]
    private Sprite transferToHandSprite;

    [SerializeField]
    [Tooltip("Sprite to show on the held item when hovering if it can be transferred to the inventory.")]
    private Sprite transferToInventorySprite;
    
    
    private NpcInventory focusedNpcInventory => PlayerManager.Instance.CurrentFocusedNpc?.Inventory;
    
    private VisualElement rootVisualElement;
    private InventoryData curInventoryData = null;

    private Holdable currentlyHoveredHoldable = null;
    private FloaterData currentFloaterData;
    private Holdable activeMenuHoldableGO = null;  // If not null, then there is a menu open right now. Might be redundant with currentFloaterData.
    private bool isHoveringInteractableMenu = false;

    private EventCallback<PointerEnterEvent> pointerEnterCallback;
    private EventCallback<PointerLeaveEvent> pointerLeaveCallback;
    
    protected override bool OnSetupFloater(VisualElement floaterRoot, InteractionMenuFloatingUIConfig floaterConfig)
    {
        // Store the lambdas in the class-level fields so that we can unregister them later.
        pointerEnterCallback = evt => isHoveringInteractableMenu = true;
        pointerLeaveCallback = evt => isHoveringInteractableMenu = false;
        floaterRoot.RegisterCallback(pointerEnterCallback);
        floaterRoot.RegisterCallback(pointerLeaveCallback);
        
        InteractionMenuInstanceManager instanceManager = new InteractionMenuInstanceManager(
            floaterRoot,
            floaterConfig.visualDefinition,
            floaterConfig.TargetInteractable,
            floaterConfig.TargetInteractableDefinition,
            floaterConfig.Triggers
        );
        instanceManager.UpdateActions(RemoveControlMenus);
        floaterConfig.InstanceManager = instanceManager;

        return true;
    }

    protected override void OnUpdateFloater(VisualElement floaterRoot, InteractionMenuFloatingUIConfig floaterConfig)
    {
        // We only update the interaction definition buttons as the title and description are expected to be static.
        floaterConfig.InstanceManager.UpdateActions(RemoveControlMenus);
    }

    protected override void OnRemoveFloater(VisualElement floaterRoot, InteractionMenuFloatingUIConfig floaterConfig)
    {
        // Always check if the delegates and floaterRoot are not null before unregistering
        if (floaterRoot == null) return;
        
        if (pointerEnterCallback != null)
        {
            floaterRoot.UnregisterCallback(pointerEnterCallback);
        }
        if (pointerLeaveCallback != null)
        {
            floaterRoot.UnregisterCallback(pointerLeaveCallback);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        
        rootVisualElement = uiDocument.rootVisualElement;
        if (slotUITemplate == null)
        {
            Debug.LogError("Inventory UI Manager is missing required UI Document or Template Assets.", this);
        }
        if (interactionMenuVisualDefinition == null)
        {
            Debug.LogError("Inventory UI Manager is missing required interaction menu visual definition asset.", this);
        }
        
        // Initially hide the inventory UI
        rootVisualElement.style.display = DisplayStyle.None;
        
        PlayerManager.Instance.OnFocusChanged += HandleFocusedNpcChange;
        if (PlayerManager.Instance.CurrentFocusedNpc != null)
        {
            HandleFocusedNpcChange(null, PlayerManager.Instance.CurrentFocusedNpc);
        }
    }

    private void Update()
    {
        // Update checks whether we should remove the control menus
        // If we are not hovering a holdable or menu and the floater data is not null, we remove the menus
        if (currentFloaterData != null && !isHoveringInteractableMenu && currentlyHoveredHoldable == null)
        {
            RemoveControlMenus();
        }
    }

    private void RemoveControlMenus()
    {
        if (currentFloaterData != null)
        {
            RemoveFloater(currentFloaterData.Id);

            currentFloaterData = null;
            activeMenuHoldableGO = null;
            isHoveringInteractableMenu = false;
        }
    }

    private bool ActivateMenuForHoldable(Holdable holdable, VisualElement slotElement)
    {
        // Creates a new menu for the holdable that hovers over the slotElement
        RemoveControlMenus();  // Start by ensuring that any existing menus are closed
        
        // Compute the position for the menu based on the slot element
        // The menu sits at the top center of the slot element
        Vector2 position = slotElement.worldBound.center;
        position.y = slotElement.worldBound.yMin; // Use the top edge Y coordinate
        
        // We have a utility to get the floater config for the holdable
        var config = InteractionMenuInstanceManager.GenerateFloatingUIConfig(
            holdable, interactionMenuVisualDefinition,

            positionType: FloatingUIPositionType.ScreenPosition,
            targetObject: position,

            anchor: FloatingUIAnchor.BottomCenter,
            keepOnScreen: true
        );

        string floaterId = CreateFloater(config);
        if (floaterId == null)
        {
            // Failed to create floater, log an error
            Debug.LogError("Failed to create Speech Bubble floater.", this);
            return false;
        }
        
        currentFloaterData = floaterDatas[floaterId];
        activeMenuHoldableGO = holdable;

        return true;
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnFocusChanged -= HandleFocusedNpcChange;
        }
        // Clean up any open menus if the object is destroyed
        RemoveControlMenus();
    }

    /// <summary>
    /// Handles the change in the NPC the player is focused on. Updates the inventory display accordingly.
    /// </summary>
    /// <param name="previousFocusedNpcContext">The context of the previously focused NPC.</param>
    /// <param name="npcContext">The context of the newly focused NPC.</param>
    void HandleFocusedNpcChange(NpcContext previousFocusedNpcContext, NpcContext npcContext)
    {
        // Unsubscribe from the previous inventory's events
        if (focusedNpcInventory != null)
        {
            focusedNpcInventory.OnInventoryChanged -= HandleInventoryUpdated;
        }
        
        if (focusedNpcInventory != null)
        {
            focusedNpcInventory.OnInventoryChanged += HandleInventoryUpdated;
            // Immediately update the UI with the new inventory data
            HandleInventoryUpdated(focusedNpcInventory.GetInventoryData());
            // Show the inventory UI
            rootVisualElement.style.display = DisplayStyle.Flex;
        }
        else
        {
            // If no NPC is focused or the focused NPC has no inventory, clear and hide the UI
            ClearInventoryData();
            BuildUIFromCurrentData(); // Build an empty UI
            rootVisualElement.style.display = DisplayStyle.None; // Hide the inventory UI
        }
    }
    
    /// <summary>
    /// Retrieves all PlayerControlTrigger components attached to the direct children of a Holdable's GameObject.
    /// </summary>
    /// <param name="holdable">The Holdable object whose children's triggers we want to find.</param>
    /// <returns>A list of PlayerControlTrigger components found on the children.</returns>
    private List<PlayerControlTrigger> GetHoldableControlTriggers(Holdable holdable)
    {
        return holdable.GetComponentsInChildren<PlayerControlTrigger>().ToList();
    }
    
    /// <summary>
    /// Clears the internal inventory data structures.
    /// </summary>
    private void ClearInventoryData()
    {
        // Debug.Log($"Nulling _currentlyHoveredHoldable on clear inventory data.");
        curInventoryData = null;
        RemoveControlMenus(); // Ensure menus are closed when data is cleared
    }

    /// <summary>
    /// Called when the focused NPC's inventory changes. Updates internal data and rebuilds the UI.
    /// </summary>
    /// <param name="inventoryData">The updated inventory data.</param>
    void HandleInventoryUpdated(InventoryData inventoryData)
    {
        curInventoryData = inventoryData;
        
        BuildUIFromCurrentData();
    }

    /// <summary>
    /// Called when the mouse pointer enters an inventory item slot.
    /// Responsible for displaying the interactable menu and changing the sprite if applicable.
    /// </summary>
    /// <param name="evt"></param>
    /// <param name="holdable"></param>
    private void HandleInventoryItemHover(PointerEnterEvent evt, Holdable holdable)
    {
        if (holdable != null)
        {
            // First, let's replace the icon if there is a holdable
            VisualElement slotElement = evt.target as VisualElement;
            Image itemIcon = slotElement.Q<Image>("ItemIcon");
            if (holdable == curInventoryData.HeldItem)
            {
                itemIcon.sprite = transferToInventorySprite;
            }
            else
            {
                itemIcon.sprite = transferToHandSprite;
            }
            
            // Then we can generate the control menu for the holdable
            if (activeMenuHoldableGO != holdable)
            {
                // Then we are hovering a new holdable
                RemoveControlMenus(); // Close any existing menus
                ActivateMenuForHoldable(holdable, evt.target as VisualElement);
            }
        }
        currentlyHoveredHoldable = holdable;
    }
    
    private void handleInventoryItemHoverExit(PointerLeaveEvent evt, Holdable holdable)
    {
        currentlyHoveredHoldable = null;
        // Reset the icon sprite to the default state
        VisualElement slotElement = evt.target as VisualElement;
        if (slotElement != null)
        {
            Image itemIcon = slotElement.Q<Image>("ItemIcon");
            itemIcon.sprite = holdable.InventorySprite;
        }
        // else the slot element has been destroyed which means that the inventory has been reset. No need to reset the icon.
    }

    private void HandleInventoryItemClick(ClickEvent evt, Holdable holdable)
    {
        if (holdable == null)
        {
            return;
        }

        focusedNpcInventory.TryRetrieveItem(holdable, storeHeldFirst: true);
    }

    private void HandleHeldItemClick(ClickEvent evt, Holdable holdable)
    {
        if (holdable == null)
        {
            return;
        }
        
        focusedNpcInventory.TryStoreHeldItem();
    }

    /// <summary>
    /// Reconstructs the inventory UI based on the current data.
    /// Clears previous elements and creates new ones.
    /// </summary>
    private void BuildUIFromCurrentData()
    {
        if (rootVisualElement == null || slotUITemplate == null)
        {
            Debug.LogError("Cannot build UI, root element or slot template is missing.");
            return;
        }
        
        RemoveControlMenus(); // Ensure menus are closed before rebuilding
        
        // Find the containers within the UXML structure
        VisualElement slotsContainer = rootVisualElement.Q<VisualElement>("SlotsContainer");
        VisualElement heldItemSlotContainer = rootVisualElement.Q<VisualElement>("HeldItemSlot"); // Assuming this is the container/element for the held item
        if (slotsContainer == null)
        {
            Debug.LogError("Could not find 'SlotsContainer' in the Inventory UI Document.", this);
            return;
        }
        if (heldItemSlotContainer == null)
        {
            Debug.LogError("Could not find 'HeldItemSlot' in the Inventory UI Document.", this);
            return;
        }
        
        // Clear previous slots from the container
        slotsContainer.Clear();

        for (int i = 0; i < curInventoryData.InventorySize; i++)
        {
            // Instantiate a new slot element from the template
            VisualElement slotInstance = slotUITemplate.Instantiate();
            slotInstance.name = $"InventorySlot_{i}";
            
            // Find the Image element within the slot instance to display the item icon
            Image itemIcon = slotInstance.Q<Image>("ItemIcon");
            if (itemIcon == null)
            {
                Debug.LogError($"Slot template '{slotUITemplate.name}' is missing an Image element named 'ItemIcon'.", slotUITemplate);
                continue; // Skip this slot if template is invalid
            }
            
            // Get the Holdable for the current slot index
            Holdable currentHoldable = (i < curInventoryData.InventorySlots.Count) ? curInventoryData.InventorySlots[i] : null;
            if (currentHoldable != null)
            {
                itemIcon.sprite = currentHoldable.InventorySprite;
                itemIcon.style.visibility = Visibility.Visible; // Make icon visible
                
                Holdable capturedHoldable = currentHoldable;
                slotInstance.RegisterCallback<PointerEnterEvent>(evt => HandleInventoryItemHover(evt, capturedHoldable));
                slotInstance.RegisterCallback<ClickEvent>(evt => HandleInventoryItemClick(evt, capturedHoldable));
                slotInstance.RegisterCallback<PointerLeaveEvent>(evt => handleInventoryItemHoverExit(evt, capturedHoldable));
            }
            else
            {
                // No item in this slot
                itemIcon.sprite = null;
                itemIcon.style.visibility = Visibility.Hidden; // Hide icon for empty slots
            }
            
            // Add the configured slot instance to the container
            slotsContainer.Add(slotInstance);
        }
        
        // --- Populate Held Item Slot ---
        // Find the Image element within the held item slot
        Image heldItemIcon = heldItemSlotContainer.Q<Image>("ItemIcon"); // Assuming same structure as regular slots
        if (heldItemIcon != null)
        {
            if (curInventoryData.HeldItem != null)
            {
                heldItemIcon.sprite = curInventoryData.HeldItem.InventorySprite;
                heldItemIcon.style.visibility = Visibility.Visible;

                // Register hover event listener for the held item slot
                heldItemSlotContainer.RegisterCallback<PointerEnterEvent>(evt => HandleInventoryItemHover(evt, curInventoryData.HeldItem));
                heldItemSlotContainer.RegisterCallback<ClickEvent>(evt => HandleHeldItemClick(evt, curInventoryData.HeldItem));
                heldItemSlotContainer.RegisterCallback<PointerLeaveEvent>(evt => handleInventoryItemHoverExit(evt, curInventoryData.HeldItem));
            }
            else
            {
                // No held item or sprite missing
                heldItemIcon.sprite = null;
                heldItemIcon.style.visibility = Visibility.Hidden;
            }
        }
    }
}