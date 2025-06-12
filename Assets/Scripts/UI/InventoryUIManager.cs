using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System; // Required for Action
using System.Linq; // Required for LINQ operations like Sum

[DefaultExecutionOrder(-50)]
public class InventoryUIManager : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;
    
    [SerializeField] [Tooltip("The document component that contains the InventoryUI element.")]
    private UIDocument inventoryUIDocument;

    private IPanel _cachedPanel;
    private VisualElement _rootVisualElement; // Cache the root VE
    
    [SerializeField] [Tooltip("UXML template asset for a single inventory slot")]
    private VisualTreeAsset slotUITemplate;

    // There is no implementation needed for the menuUxmlTemplate as
    // ControlMenuDisplay(PlayerControlTrigger controlTrigger, VisualElement menuElement, Action executeCallback)
    // handles getting all the information and populating it.
    // This already exists in the code base and can simply be used
    [SerializeField]
    [Tooltip("UXML template asset for a single menu item.")]
    private VisualTreeAsset menuUxmlTemplate;
    
    [SerializeField]
    [Tooltip("Sprite to show on an inventory item when hovering if it can be transferred to the hand.")]
    private Sprite transferToHandSprite;

    [SerializeField]
    [Tooltip("Sprite to show on the held item when hovering if it can be transferred to the inventory.")]
    private Sprite transferToInventorySprite;

    private NpcInventory focusedNpcInventory;

    private int inventorySize;

    private List<Holdable> inventorySlots;
    private Dictionary<Holdable, List<PlayerControlTrigger>> inventorySlotControlTriggers;
    private Dictionary<Holdable, Sprite> inventorySlotSprites;

    private Holdable heldItem;
    private List<PlayerControlTrigger> heldItemTriggers;
    private Sprite heldItemSprite;

    // Store references to the slot VisualElements for hover detection
    private Dictionary<VisualElement, Holdable> _slotElementToHoldableMap = new();
    private VisualElement _heldItemSlotElement;

    private Rect inventoryUIBBox = Rect.zero; // Bounding box in Panel coordinates (origin top-left)
    private bool controlMenuOpen = false;
    private Rect? currentControlMenuBBox = null; // Bounding box in Panel coordinates (origin top-left)
    private List<ControlMenuDisplay> activeControlMenus = new();

    private Holdable _currentlyHoveredHoldable = null; // Track which item (if any) is currently hovered

    /// <summary>
    /// The Holdable game object will have children that have PlayerControlTrigger components. This is where we get them.
    /// PlayerControlTriggers are passed directly to the ControlTriggerMenuDisplay for rendering.
    /// GetActionStatus(GameObject initiator).IsVisible tells us whether the menu should be visible.
    /// playerManager.HandleTriggerInteraction(PlayerControlTrigger); should be linked to the execute button
    /// </summary>

    /// <summary>
    /// Holdable has some potentially useful fields.
    /// If we decide to give more information, Holdable.GetItemDefinition().ItemName/ItemDescription may be useful
    /// Holdable.InventorySprite is how we get the sprite.
    /// </summary>

    #region Unity Lifecycle

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Ensure PlayerManager is assigned
        if (playerManager == null)
        {
            playerManager = PlayerManager.Instance;
        }
        if (playerManager == null)
        {
            Debug.LogError("Inventory UI Manager could not find PlayerManager.", this);
            enabled = false; // Disable script if PlayerManager is missing
            return;
        }

        // Ensure UIDocument and templates are assigned
        if (inventoryUIDocument == null || slotUITemplate == null || menuUxmlTemplate == null)
        {
            Debug.LogError("Inventory UI Manager is missing required UI Document or Template Assets.", this);
            enabled = false; // Disable script if essential assets are missing
            return;
        }

        // Cache the root VisualElement
        _rootVisualElement = inventoryUIDocument.rootVisualElement;
        if (_rootVisualElement == null)
        {
            Debug.LogError("Inventory UI Document has no root VisualElement.", this);
            enabled = false;
            return;
        }
        // Initially hide the inventory UI
        _rootVisualElement.style.display = DisplayStyle.None;
        
        // Initialize lists/dictionaries
        inventorySlots = new List<Holdable>();
        inventorySlotControlTriggers = new Dictionary<Holdable, List<PlayerControlTrigger>>();
        inventorySlotSprites = new Dictionary<Holdable, Sprite>();
        heldItemTriggers = new List<PlayerControlTrigger>();
        activeControlMenus = new List<ControlMenuDisplay>();
        _slotElementToHoldableMap = new Dictionary<VisualElement, Holdable>();

        // Subscribe to necessary events
        playerManager.OnFocusChanged += HandleFocusedNpcChange;
        if (playerManager.CurrentFocusedNpc != null)
        {
            HandleFocusedNpcChange(null, playerManager.CurrentFocusedNpc);
        }
        InputManager.Instance.OnHoverChanged += HandleMouseMove;
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (playerManager != null)
        {
            playerManager.OnFocusChanged -= HandleFocusedNpcChange;
        }
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnHoverChanged -= HandleMouseMove;
        }
        // Clean up any open menus if the object is destroyed
        RemoveControlMenus();
    }

    #endregion

    #region Event Handlers

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

        // Update the focused inventory and subscribe to its events
        focusedNpcInventory = npcContext?.Inventory; // Use null-conditional operator

        if (focusedNpcInventory != null)
        {
            focusedNpcInventory.OnInventoryChanged += HandleInventoryUpdated;
            // Immediately update the UI with the new inventory data
            HandleInventoryUpdated(focusedNpcInventory.GetInventoryData());
            // Show the inventory UI
             _rootVisualElement.style.display = DisplayStyle.Flex;
        }
        else
        {
            // If no NPC is focused or the focused NPC has no inventory, clear and hide the UI
            ClearInventoryData();
            BuildUIFromCurrentData(); // Build an empty UI
            _rootVisualElement.style.display = DisplayStyle.None; // Hide the inventory UI
        }
    }

    /// <summary>
    /// Retrieves all PlayerControlTrigger components attached to the direct children of a Holdable's GameObject.
    /// </summary>
    /// <param name="holdable">The Holdable object whose children's triggers we want to find.</param>
    /// <returns>A list of PlayerControlTrigger components found on the children.</returns>
    private List<PlayerControlTrigger> GetHoldableControlTriggers(Holdable holdable)
    {
        // Initialize a list to store the triggers we find.
        List<PlayerControlTrigger> triggers = new List<PlayerControlTrigger>();

        // Check if the provided holdable is null to prevent errors.
        if (holdable == null)
        {
            // This is expected for empty slots, so no warning needed.
            return triggers; // Return the empty list if holdable is null.
        }

        // Get the GameObject associated with the Holdable.
        GameObject holdableGo = holdable.gameObject;

        // Iterate through each direct child transform of the holdable's GameObject.
        // We iterate through transforms because it's a common way to access children in Unity.
        foreach (Transform childTransform in holdableGo.transform)
        {
            // Try to get the PlayerControlTrigger component from the child's GameObject.
            // We look specifically on children because the design specifies triggers are located there,
            // not on the parent Holdable object itself.
            // if (childTransform.TryGetComponent<PlayerControlTrigger>(out PlayerControlTrigger trigger))
            // {
            //     // If a trigger component is found, add it to our list.
            //     triggers.Add(trigger);
            // }
            
            PlayerControlTrigger[] childTriggers = childTransform.gameObject.GetComponents<PlayerControlTrigger>();
            triggers.AddRange(childTriggers);
        }

        // Return the populated list of triggers.
        return triggers;
    }

    /// <summary>
    /// Clears the internal inventory data structures.
    /// </summary>
    private void ClearInventoryData()
    {
        inventorySize = 0;
        inventorySlots.Clear();
        inventorySlotControlTriggers.Clear();
        inventorySlotSprites.Clear();
        heldItem = null;
        heldItemTriggers.Clear();
        heldItemSprite = null;
        _currentlyHoveredHoldable = null;
        // Debug.Log($"Nulling _currentlyHoveredHoldable on clear inventory data.");
        RemoveControlMenus(); // Ensure menus are closed when data is cleared
    }


    /// <summary>
    /// Called when the focused NPC's inventory changes. Updates internal data and rebuilds the UI.
    /// </summary>
    /// <param name="inventoryData">The updated inventory data.</param>
    void HandleInventoryUpdated(InventoryData inventoryData)
    {
        // Debug.Log($"Inventory Update: Capacity {inventoryData.InventorySlots.Count} / {inventoryData.InventorySize}. Held Item: {inventoryData.HeldItem?.name ?? "None"}");

        // Update inventory size
        inventorySize = inventoryData.InventorySize;

        // Update inventory slots data
        inventorySlots = inventoryData.InventorySlots ?? new List<Holdable>(); // Ensure list is not null
        inventorySlotControlTriggers.Clear();
        inventorySlotSprites.Clear();
        foreach (Holdable slotHoldable in inventorySlots)
        {
            // A null entry in inventorySlots signifies an empty slot
            if (slotHoldable != null)
            {
                inventorySlotControlTriggers.Add(slotHoldable, GetHoldableControlTriggers(slotHoldable));
                inventorySlotSprites.Add(slotHoldable, slotHoldable.InventorySprite);
            }
            // We don't add entries for null (empty) slots in the dictionaries
        }

        // Update held item data
        heldItem = inventoryData.HeldItem;
        heldItemTriggers = GetHoldableControlTriggers(heldItem); // Handles null heldItem internally
        heldItemSprite = heldItem?.InventorySprite; // Use null-conditional operator

        // Rebuild the UI based on the new data
        BuildUIFromCurrentData();
    }

    private void HandleItemPointerExit(PointerLeaveEvent evt, Holdable holdable)
    {
        if (holdable != null)
        {
            VisualElement slotElement = evt.target as VisualElement;
            Image itemIcon = slotElement.Q<Image>("ItemIcon");
            itemIcon.sprite = holdable.InventorySprite;
        }
    }
    
    /// <summary>
    /// Handles the pointer entering a visual element representing an inventory slot or held item.
    /// Initiates the display of control menus if the item is different from the currently hovered one.
    ///
    /// Also changes the sprite to reflect the fact that the player can click to transfer between hand and inventory.
    /// </summary>
    /// <param name="evt">The pointer enter event details.</param>
    /// <param name="holdable">The Holdable item associated with the element being hovered.</param>
    private void HandleItemPointerEnter(PointerEnterEvent evt, Holdable holdable)
    {
        // First, let's replace the icon if there is a holdable
        if (holdable != null)
        {
            VisualElement slotElement = evt.target as VisualElement;
            Image itemIcon = slotElement.Q<Image>("ItemIcon");
            if (slotElement == _heldItemSlotElement)
            {
                // Then this is the held item slot... obviously. This comment follows good comment form
                itemIcon.sprite = transferToInventorySprite;
            }
            else
            {
                itemIcon.sprite = transferToHandSprite;
            }
        }
        
        // If we are hovering over a different item, or entering an item slot when no menus are open
        if (holdable != _currentlyHoveredHoldable)
        {
            // Remove any existing menus first
            RemoveControlMenus();

            _currentlyHoveredHoldable = holdable; // Update the currently hovered item
            // Debug.Log($"Setting _currentlyHoveredHoldable due to pointer enter item box.");

            // Only display menus if the slot is not empty
            if (holdable != null)
            {
                VisualElement targetElement = evt.currentTarget as VisualElement;
                if (targetElement != null)
                {
                    // Calculate the position for the menus: top-center of the hovered element
                    // We need to wait for geometry to be calculated if it hasn't been already.
                    if (targetElement.resolvedStyle.width > 0 && targetElement.resolvedStyle.height > 0)
                    {
                        Vector2 itemCenterTopPanelPosition = targetElement.worldBound.center;
                        itemCenterTopPanelPosition.y = targetElement.worldBound.yMin; // Use the top edge Y coordinate
                        HandleItemHover(holdable, itemCenterTopPanelPosition);
                    }
                    else
                    {
                        // If geometry is not ready, register a callback to try again once it is
                        targetElement.RegisterCallback<GeometryChangedEvent>(e =>
                        {
                            // Check if we are still hovering the same item when the geometry is ready
                            if (_currentlyHoveredHoldable == holdable)
                            {
                                Vector2 pos = e.newRect.center;
                                pos.y = e.newRect.yMin;
                                HandleItemHover(holdable, pos);
                            }
                        });
                    }
                }
            }
        }
    }


    /// <summary>
    /// Initiates the display of control menus for a specific Holdable item at a given position.
    /// </summary>
    /// <param name="holdable">The Holdable item to show menus for.</param>
    /// <param name="itemCenterTopPanelPosition">The panel position corresponding to the top center of the item's visual element.</param>
    private void HandleItemHover(Holdable holdable, Vector2 itemCenterTopPanelPosition)
    {
        // Debug.Log($"Handling item hover over {holdable.name}");
        if (holdable == null)
        {
            // This can happen if HandleItemPointerEnter calls this before the null check,
            // or if the item disappears between pointer enter and geometry calculation.
            // Debug.LogWarning("HandleItemHover called with a null holdable.");
            _currentlyHoveredHoldable = null; // Ensure hover state is cleared
            // Debug.Log($"Nulling _currentlyHoveredHoldable due to item hover over null item.");
            RemoveControlMenus(); // Clean up any potentially orphaned menus
            return;
        }

        // Retrieve the triggers associated with the holdable item
        List<PlayerControlTrigger> holdableTriggers;
        if (holdable == heldItem)
        {
            holdableTriggers = heldItemTriggers;
        }
        else if (!inventorySlotControlTriggers.TryGetValue(holdable, out holdableTriggers))
        {
            // If the holdable isn't the held item and not found in slot triggers, something is wrong.
            Debug.LogWarning($"Could not find triggers for holdable '{holdable.name}' in inventory slots.", holdable.gameObject);
            holdableTriggers = new List<PlayerControlTrigger>(); // Use an empty list to avoid errors
        }

        // Proceed to display the control menus for these triggers
        DisplayControlMenus(holdableTriggers, itemCenterTopPanelPosition);
    }

    /// <summary>
    /// Tries to transfer the item into the inventory
    /// </summary>
    /// <param name="evt"></param>
    /// <param name="holdable"></param>
    private void HandleHeldItemClick(ClickEvent evt, Holdable holdable)
    {
        if (holdable == null)
        {
            return;
        }
        
        focusedNpcInventory.TryStoreHeldItem();
    }

    /// <summary>
    /// Tries to transfer the item from the inventory into the held slot.
    /// </summary>
    /// <param name="evt"></param>
    /// <param name="holdable"></param>
    private void HandleInventoryItemClick(ClickEvent evt, Holdable holdable)
    {
        if (holdable == null)
        {
            return;
        }

        focusedNpcInventory.TryRetrieveItem(holdable, storeHeldFirst: true);
    }
    
    /// <summary>
    /// Handles global mouse movement to determine if the cursor has left the relevant UI areas (inventory + open menus).
    /// </summary>
    /// <param name="hoverState">Current hover state including screen position.</param>
    private void HandleMouseMove(InputManager.HoverState hoverState)
    {
        // Only process if control menus are currently open or if we are hovering over an item
        if (!controlMenuOpen && !_currentlyHoveredHoldable) return;

        // Convert the mouse screen position to the UI panel's coordinate system
        Vector2? panelMousePositionNullable = ConvertScreenToPanelPosition(hoverState.ScreenPosition);
        if (!panelMousePositionNullable.HasValue) return; // Exit if conversion fails

        Vector2 panelMousePosition = panelMousePositionNullable.Value;

        // Check if the mouse is within the bounding box of the main inventory UI
        bool isInInventoryBBox = inventoryUIBBox.Contains(panelMousePosition);

        // Check if the mouse is within the bounding box of the currently open control menus
        bool isInControlMenuBBox = currentControlMenuBBox.HasValue && currentControlMenuBBox.Value.Contains(panelMousePosition);

        // If the mouse pointer is outside both the inventory area and the control menu area, close the menus.
        if (!isInInventoryBBox && !isInControlMenuBBox)
        {
            // Debug.Log($"Mouse left UI bounds, removing control menus.\nMouse location {panelMousePosition}.\nInventory BBox {inventoryUIBBox}.\nMenu BBox {currentControlMenuBBox}.");
            if (_currentlyHoveredHoldable != null)
            {
                _currentlyHoveredHoldable = null; // Clear hover state as we left the area
                // Debug.Log($"Nulling _currentlyHoveredHoldable because control menu is open and we left the area.");    
            }

            if (controlMenuOpen)
            {
                RemoveControlMenus();
            }
        }
    }

    /// <summary>
    /// Handles the execution of an action when a specific menu item is clicked.
    /// Delegates the interaction to the PlayerManager and then hides the menus.
    /// </summary>
    /// <param name="triggerToExecute">The PlayerControlTrigger associated with the clicked menu item.</param>
    private void HandleControlMenuExecution(PlayerControlTrigger triggerToExecute)
    {
        if (triggerToExecute == null || playerManager == null)
        {
            Debug.LogError("Cannot execute menu action. Trigger or PlayerManager is null.", triggerToExecute?.gameObject);
            // Still attempt to remove menus even if execution fails
            RemoveControlMenus();
            _currentlyHoveredHoldable = null; // Clear hover state after action
            // Debug.Log($"Nulling _currentlyHoveredHoldable after failured to execute menu action.");
            return;
        }

        // Use PlayerManager to handle the interaction logic associated with the trigger
        // Debug.Log($"Executing trigger: {triggerToExecute.Title} on {triggerToExecute.gameObject.name}");
        playerManager.HandleTriggerInteraction(triggerToExecute);

        // Close the control menus after executing the action
        RemoveControlMenus(); 
        _currentlyHoveredHoldable = null; // Clear hover state after action
        // Debug.Log($"Nulling _currentlyHoveredHoldable after interaction triggered.");
    }

    #endregion

    #region UI Management

    /// <summary>
    /// Reconstructs the inventory UI based on the current data (inventorySize, inventorySlots, heldItem).
    /// Clears previous elements and creates new ones.
    /// </summary>
    private void BuildUIFromCurrentData()
    {
        if (_rootVisualElement == null || slotUITemplate == null)
        {
            Debug.LogError("Cannot build UI, root element or slot template is missing.");
            return;
        }

        // --- Clear Existing UI Elements and State ---
        RemoveControlMenus(); // Ensure menus are closed before rebuilding
        _slotElementToHoldableMap.Clear(); // Clear the mapping
        _heldItemSlotElement = null;
        _currentlyHoveredHoldable = null; // Reset hover state
        // Debug.Log($"Nulling _currentlyHoveredHoldable on UI build from data.");

        // Find the containers within the UXML structure
        VisualElement slotsContainer = _rootVisualElement.Q<VisualElement>("SlotsContainer");
        VisualElement heldItemSlotContainer = _rootVisualElement.Q<VisualElement>("HeldItemSlot"); // Assuming this is the container/element for the held item

        if (slotsContainer == null)
        {
            Debug.LogError("Could not find 'SlotsContainer' in the Inventory UI Document.", this);
            return;
        }
        if (heldItemSlotContainer == null)
        {
            Debug.LogError("Could not find 'HeldItemSlot' in the Inventory UI Document.", this);
            // Depending on design, this might be optional, but log warning for now.
            Debug.LogWarning("Could not find 'HeldItemSlot' element in the Inventory UI Document.", this);
        }

        // Clear previous slots from the container
        slotsContainer.Clear();

        // --- Populate Inventory Slots ---
        for (int i = 0; i < inventorySize; i++)
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
            Holdable currentHoldable = (i < inventorySlots.Count) ? inventorySlots[i] : null;

            // Configure the slot based on whether it contains an item
            if (currentHoldable != null && inventorySlotSprites.TryGetValue(currentHoldable, out Sprite sprite))
            {
                itemIcon.sprite = sprite;
                itemIcon.style.visibility = Visibility.Visible; // Make icon visible

                // Map this visual element to its Holdable
                _slotElementToHoldableMap[slotInstance] = currentHoldable;

                // Register hover event listener for this specific slot
                // Use a local variable capture to ensure the correct 'currentHoldable' is used in the lambda
                Holdable capturedHoldable = currentHoldable;
                slotInstance.RegisterCallback<PointerEnterEvent>(evt => HandleItemPointerEnter(evt, capturedHoldable));
                slotInstance.RegisterCallback<ClickEvent>(evt => HandleInventoryItemClick(evt, capturedHoldable));
                slotInstance.RegisterCallback<PointerLeaveEvent>(evt => HandleItemPointerExit(evt, capturedHoldable));
                // Note: PointerLeaveEvent is implicitly handled by HandleMouseMove checking bounds
            }
            else
            {
                // No item in this slot, or sprite missing
                itemIcon.sprite = null;
                itemIcon.style.visibility = Visibility.Hidden; // Hide icon for empty slots
                // Do not add to _slotElementToHoldableMap for empty slots
                // Do not register hover events for empty slots
            }

            // Add the configured slot instance to the container
            slotsContainer.Add(slotInstance);
        }

        // --- Populate Held Item Slot ---
        if (heldItemSlotContainer != null)
        {
             // Find the Image element within the held item slot
            Image heldItemIcon = heldItemSlotContainer.Q<Image>("ItemIcon"); // Assuming same structure as regular slots
            if (heldItemIcon != null)
            {
                 if (heldItem != null && heldItemSprite != null)
                 {
                     heldItemIcon.sprite = heldItemSprite;
                     heldItemIcon.style.visibility = Visibility.Visible;

                     _heldItemSlotElement = heldItemSlotContainer; // Store reference

                     // Register hover event listener for the held item slot
                     heldItemSlotContainer.RegisterCallback<PointerEnterEvent>(evt => HandleItemPointerEnter(evt, heldItem));
                     heldItemSlotContainer.RegisterCallback<ClickEvent>(evt => HandleHeldItemClick(evt, heldItem));
                     heldItemSlotContainer.RegisterCallback<PointerLeaveEvent>(evt => HandleItemPointerExit(evt, heldItem));
                 }
                 else
                 {
                     // No held item or sprite missing
                     heldItemIcon.sprite = null;
                     heldItemIcon.style.visibility = Visibility.Hidden;
                 }
            }
             else
            {
                 Debug.LogWarning($"Held item slot container '{heldItemSlotContainer.name}' does not contain an Image element named 'ItemIcon'.", this);
            }
        }


        // --- Update Inventory Bounding Box ---
        // We need the geometry to be calculated *after* elements are added.
        // Register a one-time callback to capture the bounds once layout is complete.
        VisualElement inventoryContainer = _rootVisualElement.Q<VisualElement>("InventoryContainer");
        if (inventoryContainer != null)
        {
            inventoryContainer.RegisterCallback<GeometryChangedEvent>(UpdateInventoryBoundingBox);
        }
        else
        {
            Debug.LogWarning("Could not find 'InventoryContainer' to calculate bounds.", this);
            // Fallback: use the root element's bounds, might be less accurate
             _rootVisualElement.RegisterCallback<GeometryChangedEvent>(UpdateInventoryBoundingBox);
        }
    }

    /// <summary>
    /// Callback function executed when the inventory container's geometry changes.
    /// Updates the stored bounding box used for hover checks.
    /// </summary>
    /// <param name="evt">The geometry changed event details.</param>
    private void UpdateInventoryBoundingBox(GeometryChangedEvent evt)
    {
        // Update the bounding box in Panel coordinates (worldBound is already in panel space)
        inventoryUIBBox = evt.newRect;
        
        // Debug.Log($"New inventory bounding box {inventoryUIBBox}");

        // It's good practice to unregister the callback if it's only needed once per build/layout change.
        // However, if the inventory container might resize dynamically later, keep it registered.
        // For simplicity here, let's assume it's static after initial build.
        (evt.currentTarget as VisualElement)?.UnregisterCallback<GeometryChangedEvent>(UpdateInventoryBoundingBox);
        // Debug.Log($"Inventory BBox Updated: {inventoryUIBBox}");
    }


    /// <summary>
    /// Instantiates and positions control menus for the given triggers above the specified panel position.
    /// Handles asynchronous geometry calculation to ensure correct placement.
    /// </summary>
    /// <param name="triggers">List of PlayerControlTriggers to create menus for.</param>
    /// <param name="itemCenterTopPanelPosition">The position (panel coordinates) above which to center the menus.</param>
    private void DisplayControlMenus(List<PlayerControlTrigger> triggers, Vector2 itemCenterTopPanelPosition)
    {
        // Prevent opening menus if already open or if essential assets are missing
        if (controlMenuOpen || menuUxmlTemplate == null || _rootVisualElement == null || playerManager == null || playerManager.CurrentFocusedNpc == null)
        {
            // Debug.LogWarning($"Tried to open control menus when already open ({controlMenuOpen}) or assets missing.");
            return;
        }

        GameObject initiator = playerManager.CurrentFocusedNpc.gameObject;

        foreach (PlayerControlTrigger trigger in triggers)
        {
            InteractionStatus testStatus = trigger.GetActionStatus(initiator);
        }

        var triggersWithStatus = triggers
            .Where(t => t != null) // Ensure trigger itself isn't null
            .Select(t => new { Trigger = t, Status = t.GetActionStatus(initiator) })
            .ToList();
        var visibleTriggersWithStatus = triggersWithStatus
            .Where(t => t.Status.IsVisible)
            .ToList();

        // If no triggers are visible, don't display any menus
        if (visibleTriggersWithStatus.Count == 0)
        {
            // Debug.Log("No visible triggers for the hovered item.");
            return;
        }

        // --- Menu Creation Phase ---
        activeControlMenus.Clear(); // Clear any lingering references (shouldn't happen, but safety first)
        List<VisualElement> menuElements = new List<VisualElement>(); // Store the root VEs of the menus
        foreach (var item in visibleTriggersWithStatus)
        {
            PlayerControlTrigger trigger = item.Trigger;
            InteractionStatus status = item.Status;
            // 1. Instantiate the menu template
            VisualElement menuVE = menuUxmlTemplate.Instantiate();
            menuVE.name = $"Menu_{trigger.Title.Replace(" ", "")}"; // Ensure valid VE name

            // 2. Create the action to perform when this menu item is clicked
            Action clickAction = () => HandleControlMenuExecution(trigger);

            // 3. Create the ControlMenuDisplay controller
            // This assumes ControlMenuDisplay handles populating the menuVE based on the trigger
            // Debug.Log($"Dislaying menu {trigger.Title} with status {status}");
            ControlMenuDisplay menuDisplay = new ControlMenuDisplay(trigger, menuVE, clickAction, interactionStatus: status);

            // 4. Add the menu's visual element to the main UI root (initially hidden or positioned off-screen)
            // Position will be set properly after geometry calculation. Hide it for now.
            // menuVE.style.visibility = Visibility.Hidden;
            _rootVisualElement.Add(menuVE);

            // 5. Store the controller and the element
            activeControlMenus.Add(menuDisplay);
            menuElements.Add(menuVE);
            
            menuDisplay.Show();
        }

        // --- Asynchronous Positioning Phase ---
        int geometryReadyCount = 0; // Counter for menus whose geometry is calculated
        float totalMenuWidth = 0f;  // Accumulate width to calculate centering

        // Callback to execute once ALL menu geometries are known
        Action positionMenusAction = () =>
        {
            // Debug.Log($"Positioning menus at {itemCenterTopPanelPosition}");
            // Calculate the starting X position to center the entire block of menus
            float startX = itemCenterTopPanelPosition.x - (totalMenuWidth / 2f);
            float currentX = startX;
            float bottomY = itemCenterTopPanelPosition.y; // Menus align their bottom edge to this Y

            float maxHeight = 0;

            for (int i = 0; i < activeControlMenus.Count; i++)
            {
                ControlMenuDisplay menuDisplay = activeControlMenus[i];
                VisualElement menuTemplate = menuElements[i]; // Get the corresponding VE
                VisualElement menuVE = menuTemplate.Children().First();
                
                float width = menuVE.resolvedStyle.width;
                float height = menuVE.resolvedStyle.height;
                if (width == 0)
                {
                    Debug.LogError($"Width of menu was 0 during position");
                }

                if (height > maxHeight)
                {
                    maxHeight = height;
                }

                // Calculate the target position for the *bottom-center* of this menu
                Vector2 targetBottomCenter = new Vector2(currentX + (width / 2f), bottomY - height / 2f);
                // Debug.Log($"Setting menu position to {targetBottomCenter}");

                // Use the menuDisplay's method to set its position (assuming it handles the anchor)
                menuDisplay.SetPanelPosition(targetBottomCenter);

                // Make the menu visible now that it's positioned
                menuDisplay.Show(); // Assumes this sets visibility on the menuVE

                // Update the current X position for the next menu
                currentX += width; // Add spacing if desired: currentX += menuBounds.width + spacing;
            }
            
            // Compute the bounding box
            // We always make the height enough to reach the inventory UI bounding box. Otherwise you wouldn't
            // actually be able to move to this bounding box
            float bboxHeight = MathF.Max(inventoryUIBBox.y - (bottomY - maxHeight), maxHeight);
            Rect combinedBBox = new(startX, bottomY - maxHeight, totalMenuWidth, bboxHeight);
            // Debug.Log($"Combined menu bounding box {combinedBBox}");

            // Store the final bounding box and set the flag
            currentControlMenuBBox = combinedBBox;
            controlMenuOpen = true;
            // Debug.Log($"Control Menus Opened. BBox: {currentControlMenuBBox}");
        };

        // Register geometry callbacks for each menu element
        foreach (ControlMenuDisplay menuDisplay in activeControlMenus)
        {
            // Use local variable capture for the specific VE in the callback
            VisualElement capturedVE = menuDisplay.GetRootElement();
            // Not sure exactly what is going on, but we need to get the size of the child of capturedVE
            // capturedVE is of type TemplateContainer named #Menu_PutDownHoldable which contains a VisualElement
            // named #MenuContainer. This VisualElement is what we actually need
            VisualElement menuElement = capturedVE.Children().First();
            menuElement.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                // Check if width is valid (geometry is calculated)
                if (evt.newRect.width > 0)
                {
                    // Unregister when we get a valid value
                    menuElement.UnregisterCallback<GeometryChangedEvent>(arguments => { });
                    
                    // Debug.Log($"Got menu of size {evt.newRect}");
                    
                    geometryReadyCount++;
                    totalMenuWidth += evt.newRect.width; // Add spacing if desired: totalMenuWidth += evt.newRect.width + spacing;

                    // If all menus have reported their geometry, execute the positioning logic
                    if (geometryReadyCount == menuElements.Count)
                    {
                        positionMenusAction();
                    }
                }
                else
                {
                     // Handle cases where geometry might not resolve correctly
                     Debug.LogWarning($"Menu element '{capturedVE.name}' reported zero width on GeometryChangedEvent.");
                     // Potentially increment count anyway or use an estimated width?
                     // For now, we'll just log and it might prevent menus from showing if one fails.
                }
            });
        }
         // Safety check: If after a short delay, not all geometries are ready, try positioning anyway or log error.
         // This handles rare cases where GeometryChangedEvent might not fire.
         // Example using a coroutine or Invoke could be added here if needed.
    }


    /// <summary>
    /// Cleans up and removes all currently displayed control menus from the UI.
    /// </summary>
    private void RemoveControlMenus()
    {
        // Only proceed if menus are actually open
        if (!controlMenuOpen)
        {
            // Debug.Log("Attempted to remove control menus, but none were open.");
            return;
        }

        // Iterate through the active menu controllers
        foreach (ControlMenuDisplay menuDisplay in activeControlMenus)
        {
            if (menuDisplay != null)
            {
                // Call the cleanup method on the controller (might unsubscribe events, etc.)
                menuDisplay.Cleanup();

                // Get the root visual element managed by the controller
                VisualElement menuVE = menuDisplay.GetRootElement();
                if (menuVE != null)
                {
                    // Remove the visual element from the UI hierarchy
                    menuVE.RemoveFromHierarchy();
                }
            }
        }

        // Clear the list of active menus and reset state variables
        activeControlMenus.Clear();
        controlMenuOpen = false;
        currentControlMenuBBox = null; // Reset the bounding box
        // Debug.Log("Control Menus Removed.");
    }

    #endregion

    #region Coordinate Helpers

    /// <summary>
    /// Gets the IPanel associated with the UIDocument, caching it for efficiency.
    /// </summary>
    /// <returns>The IPanel interface, or null if unavailable.</returns>
    private IPanel GetPanel()
    {
        // Return cached panel if available and still valid
        if (_cachedPanel == null || _rootVisualElement?.panel != _cachedPanel) // Check if panel association changed
        {
             if (inventoryUIDocument?.rootVisualElement != null)
             {
                // Cache the panel reference
                _cachedPanel = inventoryUIDocument.rootVisualElement.panel;
             }
             else
             {
                 // If rootVisualElement is null somehow, return null
                 _cachedPanel = null;
             }
        }
        return _cachedPanel;
    }


    /// <summary>
    /// Converts a screen-space position (e.g., mouse position, origin bottom-left)
    /// to a UI Toolkit panel position (origin top-left).
    /// </summary>
    /// <param name="screenPos">The screen-space position (pixels, origin bottom-left).</param>
    /// <returns>The corresponding panel position (Vector2, origin top-left), or null if conversion fails.</returns>
    private Vector2? ConvertScreenToPanelPosition(Vector2 screenPos)
    {
        IPanel panel = GetPanel();
        // Ensure panel and its visualTree are valid, as panel height is needed.
        if (panel == null || panel.visualTree == null) return null;

        // 1. Convert screen position to panel position (origin bottom-left)
        //    RuntimePanelUtils.ScreenToPanel handles DPI scaling and panel transformations.
        Vector2 panelPosBottomOrigin = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

        // Check if the conversion resulted in NaN values, which can happen in edge cases
        // (e.g., panel not fully initialized, off-screen coordinates depending on panel setup).
        if (float.IsNaN(panelPosBottomOrigin.x) || float.IsNaN(panelPosBottomOrigin.y))
        {
            // Debug.LogWarning($"ScreenToPanel conversion resulted in NaN for screen position {screenPos}");
            return null;
        }

        // 2. Get the resolved height of the panel's root visual element.
        //    This is crucial for flipping the Y-coordinate correctly.
        float panelHeight = panel.visualTree.resolvedStyle.height;

        // Check if panel height is valid (it might be NaN or zero if layout hasn't completed).
        if (float.IsNaN(panelHeight) || panelHeight <= 0) {
            // Debug.LogWarning($"Panel height is not valid ({panelHeight}) for Y-coordinate flip.");
            // If height isn't ready, we cannot accurately convert to top-left origin.
            return null;
        }

        // 3. Flip the Y-coordinate to convert from bottom-left origin to top-left origin.
        //    Panel Y (top-left) = Panel Height - Panel Y (bottom-left)
        return new Vector2(panelPosBottomOrigin.x, panelHeight - panelPosBottomOrigin.y);
    }


    #endregion
}
