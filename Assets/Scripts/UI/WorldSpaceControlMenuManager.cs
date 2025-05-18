using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Enumerable methods like Any(), Where()

/// <summary>
/// Manages world-space trigger icons and pop-up control menus based on proximity
/// to the mouse cursor and interaction context provided by the PlayerManager.
/// Uses UI Toolkit for menu rendering.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldSpaceControlMenuManager : MonoBehaviour
{
    #region Configuration Fields

    [Header("Core References")]
    [SerializeField]
    [Tooltip("Camera for world-to-screen conversions. Defaults to Camera.main if unassigned.")]
    private Camera viewCamera;

    [SerializeField]
    [Tooltip("UXML template asset for a single menu item.")]
    private VisualTreeAsset menuUxmlTemplate;

    [SerializeField]
    [Tooltip("Default prefab for the trigger icon displayed in world space.")]
    private GameObject defaultIconPrefab;

    [Header("Detection & Activation")]
    [SerializeField]
    [Tooltip("Radius around the hover point to check for triggers.")]
    private float detectionRadius = 2.0f;

    [SerializeField]
    [Tooltip("Layer(s) containing PlayerControlTrigger objects.")]
    private LayerMask triggerLayerMask;

    [SerializeField]
    [Tooltip("Max screen distance (pixels) from mouse cursor to trigger icon to activate menus.")]
    private float menuActivationDistance = 50f;

    [SerializeField]
    [Tooltip("Screen distance (pixels) from the mouse cursor to trigger menu deactivation. Controls hysteresis.")]
    private float menuDeactivationDistance = 100f;

    [Header("Layout")]
    [SerializeField]
    [Tooltip("Screen distance (pixels) from icon center to place menus when multiple are visible.")]
    private float menuCircleRadius = 70f;

    [SerializeField]
    [Tooltip("Optional: Name of a VisualElement in the UI Doc to contain menus. If empty, uses the root.")]
    private string menuContainerName = "WorldMenuContainer";

    #endregion

    #region Private State Fields

    // --- Core Components ---
    private UIDocument _uiDocument;
    private VisualElement _menuContainer;
    private ProximityDetector _proximityDetector;
    private IPanel _cachedPanel; // Cached panel reference for coordinate conversions

    // --- Singleton References ---
    private PlayerManager _playerManagerInstance;
    private InputManager _inputManagerInstance;

    // --- Input & Context State ---
    private Vector2? _currentMousePanelPosition; // Last reported mouse panel position (Y=0 is Top)
    private Vector3 _currentMouseWorldPosition;
    private bool _hasWorldHit;
    private NpcContext _currentFocusedNpc;

    // --- Tracking State ---
    private readonly HashSet<GameObject> _nearbyTriggerObjects = new HashSet<GameObject>();
    private readonly HashSet<GameObject> _validNearbyParents = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, GameObject> _activeIcons = new Dictionary<GameObject, GameObject>();
    private readonly Dictionary<GameObject, PlayerControlTriggerVisualDefinition> _iconVisualDefinitions = new Dictionary<GameObject, PlayerControlTriggerVisualDefinition>();

    // --- Menu State ---
    // Store pairs of triggers and their interaction status directly
    private readonly Dictionary<GameObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)>> _lastFrameStatuses =
        new Dictionary<GameObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)>>();
    private readonly Dictionary<GameObject, List<ControlMenuDisplay>> _activeMenus = new Dictionary<GameObject, List<ControlMenuDisplay>>();
    private GameObject _currentMenuTargetParent = null; // The parent GO whose menus are currently displayed or targeted
    private bool _isMenuLocked = false; // True if the mouse is over a menu, preventing it from hiding

    #endregion

    #region MonoBehaviour Messages

    /// <summary>
    /// Initializes component references and performs essential null checks.
    /// </summary>
    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        // Default to the main camera if none is assigned
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        // Validate essential components
        if (viewCamera == null)
        {
            Debug.LogError("WSCMM: View Camera is missing and Camera.main could not be found!", this);
            enabled = false; // Disable script if camera is missing
            return;
        }
        if (_uiDocument?.rootVisualElement == null)
        {
            Debug.LogError("WSCMM: UIDocument or its rootVisualElement is null.", this);
            enabled = false; // Disable script if UI Document is invalid
            return;
        }
    }

    /// <summary>
    /// Sets up references, finds the menu container, instantiates the detector,
    /// gets singleton instances, and subscribes to input events.
    /// </summary>
    private void Start()
    {
        // Validate required assets
        if (menuUxmlTemplate == null)
        {
            Debug.LogError("WSCMM: Menu UXML Template asset is missing!", this);
            enabled = false;
            return;
        }
        if (defaultIconPrefab == null)
        {
            Debug.LogError("WSCMM: Default Icon Prefab is missing!", this);
            enabled = false;
            return;
        }

        // Setup Menu Container: Find the specified container or default to the root
        if (!string.IsNullOrEmpty(menuContainerName))
        {
            _menuContainer = _uiDocument.rootVisualElement.Q<VisualElement>(menuContainerName);
            if (_menuContainer == null)
            {
                Debug.LogWarning($"WSCMM: Container VisualElement named '{menuContainerName}' not found. Defaulting to the root element.", this);
            }
        }
        // Ensure _menuContainer is assigned, falling back to the root if necessary
        if (_menuContainer == null)
        {
            _menuContainer = _uiDocument.rootVisualElement;
        }

        // Instantiate the proximity detector
        _proximityDetector = new ProximityDetector(detectionRadius, triggerLayerMask);

        // Get Singleton References and validate
        _playerManagerInstance = PlayerManager.Instance;
        if (_playerManagerInstance == null)
        {
            Debug.LogError("WSCMM: PlayerManager.Instance is null! Ensure a PlayerManager exists in the scene.", this);
            enabled = false;
            return;
        }

        _inputManagerInstance = InputManager.Instance;
        if (_inputManagerInstance == null)
        {
            Debug.LogError("WSCMM: InputManager.Instance is null! Ensure an InputManager exists in the scene.", this);
            enabled = false;
            return;
        }

        // Get Initial State from PlayerManager
        _currentFocusedNpc = _playerManagerInstance.CurrentFocusedNpc;

        // Subscribe to InputManager events
        _inputManagerInstance.OnHoverChanged += HandleHoverChange;
    }

    /// <summary>
    /// Unsubscribes from events and cleans up all UI elements when the component is disabled or destroyed.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe from events to prevent memory leaks
        if (_inputManagerInstance != null)
        {
            _inputManagerInstance.OnHoverChanged -= HandleHoverChange;
        }

        // Clean up all created UI elements and reset state
        CleanupAllUI();
        _nearbyTriggerObjects.Clear();
        _validNearbyParents.Clear();
        _lastFrameStatuses.Clear(); // Clear the status cache
        _currentMenuTargetParent = null;
        _isMenuLocked = false;
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Handles updates from the InputManager's hover state changes.
    /// Manages proximity detection, object entry/exit, icon updates, and menu display logic.
    /// </summary>
    /// <param name="hoverState">The current hover state information from the InputManager.</param>
    private void HandleHoverChange(InputManager.HoverState hoverState)
    {
        // Update current mouse positions and hit status
        _currentMousePanelPosition = ConvertScreenToPanelPosition(hoverState.ScreenPosition);
        _currentMouseWorldPosition = hoverState.WorldPosition;
        _hasWorldHit = hoverState.HasHit;
        _currentFocusedNpc = _playerManagerInstance.CurrentFocusedNpc; // Refresh focused NPC context

        // UI calculations require a valid panel position. If we don't have one, exit early.
        if (!_currentMousePanelPosition.HasValue)
        {
            // Optional: Could potentially hide all icons/menus here if panel position is lost
            // CleanupAllUI(); // Uncomment if desired behavior is to hide everything
            return;
        }

        // Perform proximity check based on whether the mouse hit something in the world
        // ProximityChanges changes = _hasWorldHit
        //     ? _proximityDetector.UpdateProximityCheck(_currentMouseWorldPosition)
        //     : _proximityDetector.UpdateProximityCheck(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)); // Use distant point if no world hit
        ProximityChanges changes = _proximityDetector.UpdateProximityCheck(_currentMouseWorldPosition);

        // Process objects that have exited the proximity radius
        foreach (GameObject exitedObject in changes.ExitedObjects)
        {
            if (_nearbyTriggerObjects.Remove(exitedObject))
            {
                HandleObjectExit(exitedObject);
            }
        }
        // Process objects that have entered the proximity radius
        foreach (GameObject enteredObject in changes.EnteredObjects)
        {
            if (_nearbyTriggerObjects.Add(enteredObject))
            {
                HandleObjectEnter(enteredObject);
            }
        }

        // Update trigger statuses, icon visibility, and determine the closest icon parent
        var targetingResult = UpdateIconsTargetingAndStatus();

        // Update the menu display based on the closest icon and current statuses
        UpdateMenuDisplay(targetingResult.closestIconParent, targetingResult.frameStatuses);
    }

    /// <summary>
    /// Handles logic when a GameObject with a PlayerControlTrigger enters the proximity radius.
    /// </summary>
    /// <param name="enteredObject">The GameObject that entered the radius.</param>
    private void HandleObjectEnter(GameObject enteredObject)
    {
        // Add the object to the set of valid nearby parents if it has a trigger component
        if (enteredObject != null && enteredObject.GetComponent<PlayerControlTrigger>() != null)
        {
            _validNearbyParents.Add(enteredObject);
            // Note: Status calculation and icon display happen in UpdateIconsTargetingAndStatus
        }
    }

    /// <summary>
    /// Handles logic when a GameObject exits the proximity radius.
    /// Removes it from tracking, cleans up its icon and potentially its menu.
    /// </summary>
    /// <param name="exitedObject">The GameObject that exited the radius.</param>
    private void HandleObjectExit(GameObject exitedObject)
    {
        // Remove from valid parents and clear its cached status
        _validNearbyParents.Remove(exitedObject);
        _lastFrameStatuses.Remove(exitedObject);

        // Remove the associated trigger icon if one exists
        if (_activeIcons.ContainsKey(exitedObject))
        {
            RemoveTriggerIcon(exitedObject);
        }

        // If the exiting object was the current menu target and the menu isn't locked, hide its menus
        if (_currentMenuTargetParent == exitedObject && !_isMenuLocked)
        {
            HideMenusForParent(exitedObject);
            // _currentMenuTargetParent will be nullified within HideMenusForParent
        }
    }

    /// <summary>
    /// Iterates through valid nearby parent objects, calculates the status of their triggers,
    /// manages the visibility of trigger icons based on status, finds the closest icon to the mouse cursor,
    /// and caches the calculated trigger statuses for the current frame.
    /// </summary>
    /// <returns>A tuple containing the closest parent GameObject (if any within range) and a dictionary of statuses for all valid parents.</returns>
    private (GameObject closestIconParent, Dictionary<GameObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)>> frameStatuses) UpdateIconsTargetingAndStatus()
    {
        GameObject closestIconParent = null;
        float minDistanceSq = menuActivationDistance * menuActivationDistance; // Use squared distance for efficiency

        // Clear the status dictionary from the previous frame before repopulating
        _lastFrameStatuses.Clear();

        // Use a temporary list to mark parents for removal if they become invalid during iteration (e.g., destroyed)
        List<GameObject> parentsToRemove = null;

        // Iterate through the GameObjects currently considered valid parents
        foreach (GameObject parentObject in _validNearbyParents)
        {
            // Safety check: Handle cases where the GameObject might have been destroyed since last frame
            if (parentObject == null)
            {
                if (parentsToRemove == null) { parentsToRemove = new List<GameObject>(); }
                parentsToRemove.Add(parentObject); // Mark null object for removal from _validNearbyParents later
                continue;
            }

            // Get all PlayerControlTrigger components on the parent object
            PlayerControlTrigger[] triggers = parentObject.GetComponents<PlayerControlTrigger>();

            // Handle cases where triggers might have been removed or disabled unexpectedly
            if (triggers == null || triggers.Length == 0)
            {
                if (parentsToRemove == null) { parentsToRemove = new List<GameObject>(); }
                parentsToRemove.Add(parentObject); // Mark for removal

                // Ensure any associated UI (icon/menu) is cleaned up if it somehow still exists
                if (_activeIcons.ContainsKey(parentObject)) { RemoveTriggerIcon(parentObject); }
                if (_activeMenus.ContainsKey(parentObject)) { HideMenusForParent(parentObject); }
                continue;
            }

            // List to store the status pairs for the triggers on this specific parentObject
            List<(PlayerControlTrigger Trigger, InteractionStatus Status)> currentPairs = new List<(PlayerControlTrigger, InteractionStatus)>();
            bool anyTriggerVisible = false; // Flag to track if any trigger on this parent should make the icon visible

            // Evaluate each trigger on the parent object
            foreach (PlayerControlTrigger trigger in triggers)
            {
                // Process only triggers that are enabled, not null, and configured for world interaction display
                if (trigger != null && trigger.enabled && trigger.IsInteractionAvailableFrom(InteractionAvailableFrom.World))
                {
                    // Get the interaction status based on the currently focused NPC (if any)
                    InteractionStatus status = (_currentFocusedNpc != null)
                                              ? trigger.GetActionStatus(_currentFocusedNpc.gameObject)
                                              : new InteractionStatus(); // Default status if no NPC is focused

                    currentPairs.Add((trigger, status)); // Store the trigger and its calculated status

                    // If this trigger's status indicates it should be visible, mark the flag
                    if (status.IsVisible)
                    {
                        anyTriggerVisible = true;
                    }
                }
                // Optional: Could add logic here to store pairs for non-world/disabled triggers
                // with a specific "not visible" status if needed for other systems.
            }

            // Cache the calculated statuses for this parent object for this frame
            _lastFrameStatuses[parentObject] = currentPairs;

            // --- Manage Icon Visibility ---
            bool isIconCurrentlyActive = _activeIcons.ContainsKey(parentObject);
            // Show icon if any trigger is visible and the icon isn't already active
            if (anyTriggerVisible && !isIconCurrentlyActive)
            {
                AddTriggerIcon(parentObject);
            }
            // Hide icon if no triggers are visible and the icon is currently active
            else if (!anyTriggerVisible && isIconCurrentlyActive)
            {
                RemoveTriggerIcon(parentObject);
            }

            // --- Check Distance to Active Icon for Menu Activation ---
            // Only check distance if an icon should be visible, exists, and we have a mouse panel position
            if (anyTriggerVisible && _activeIcons.ContainsKey(parentObject) && _currentMousePanelPosition.HasValue)
            {
                Vector2? iconPanelPos = GetIconPanelPosition(parentObject);
                if (iconPanelPos.HasValue)
                {
                    // Calculate squared distance between mouse and icon panel positions
                    float distSq = (iconPanelPos.Value - _currentMousePanelPosition.Value).sqrMagnitude;
                    // If this icon is closer than the current minimum, update the closest parent
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestIconParent = parentObject;
                    }
                }
            }
        }

        // --- Cleanup Invalid Parents ---
        // Remove any parent objects that were marked for removal during the iteration
        if (parentsToRemove != null)
        {
            foreach (var parentToRemove in parentsToRemove)
            {
                _validNearbyParents.Remove(parentToRemove);
                _lastFrameStatuses.Remove(parentToRemove); // Also ensure the status cache is cleared for removed parents
            }
        }

        // Return the closest parent found (null if none are within activation range) and the dictionary of all statuses calculated this frame
        return (closestIconParent, _lastFrameStatuses);
    }
    
    /// Manages the display and positioning of control menus based on the closest icon parent
    /// (within activation range) and the cached trigger statuses. Implements separate activation
    /// and deactivation distances for hysteresis, and handles menu locking logic.
    /// </summary>
    /// <param name="closestIconParentWithinActivationRange">The parent GameObject whose icon is currently closest to the mouse within the activation distance (null if none).</param>
    /// <param name="frameStatuses">The dictionary containing trigger statuses calculated for this frame.</param>
    // Single Function
    private void UpdateMenuDisplay(GameObject closestIconParentWithinActivationRange, Dictionary<GameObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)>> frameStatuses)
    {
        GameObject newPotentialTarget = closestIconParentWithinActivationRange; // Potential target based on activation range

        // --- 1. Handle Locked State (Highest Priority) ---
        if (_isMenuLocked)
        {
            // If a menu is locked, ensure the target parent and its menus still exist
            if (_currentMenuTargetParent != null && _activeMenus.ContainsKey(_currentMenuTargetParent))
            {
                // Get the *latest* statuses for the locked menu's parent from the frame data
                List<(PlayerControlTrigger Trigger, InteractionStatus Status)> currentStatuses;
                if (frameStatuses.TryGetValue(_currentMenuTargetParent, out currentStatuses) && currentStatuses != null)
                {
                    // Reposition the existing menus based on the potentially updated icon position and statuses
                    PositionMenusForParent(_currentMenuTargetParent, currentStatuses);
                }
                else
                {
                    // If statuses are unavailable (e.g., parent destroyed between status calc and here),
                    // the menu remains visually, but won't reposition. Consider logging a warning.
                    // Debug.LogWarning($"WSCMM: Cannot reposition locked menu for {_currentMenuTargetParent.name}, status unavailable.", _currentMenuTargetParent);
                }
            }
            else
            {
                // If the locked state is somehow invalid (e.g., target destroyed), unlock and clear target
                _isMenuLocked = false;
                _currentMenuTargetParent = null;
                // Consider hiding any lingering menu elements here if necessary (though HideMenusForParent handles cleanup)
            }
            // When locked, we ignore distance checks and just update the locked menu
            return;
        }

        // --- 2. Handle Currently Active Menu (Deactivation Check) ---
        if (_currentMenuTargetParent != null)
        {
            // Get the panel position of the currently active menu's icon
            Vector2? currentIconPanelPos = GetIconPanelPosition(_currentMenuTargetParent);

            // Check if the icon position is valid and we have a mouse position
            if (currentIconPanelPos.HasValue && _currentMousePanelPosition.HasValue)
            {
                // Calculate distance squared between mouse and the *current* menu's icon
                float distSq = (currentIconPanelPos.Value - _currentMousePanelPosition.Value).sqrMagnitude;
                float deactivationDistSq = menuDeactivationDistance * menuDeactivationDistance;

                // 2a. Check if mouse is still within the DEACTIVATION radius
                if (distSq <= deactivationDistSq)
                {
                    // Still within range, keep the current menu active and just update its position.
                    List<(PlayerControlTrigger Trigger, InteractionStatus Status)> currentStatuses;
                    if (frameStatuses.TryGetValue(_currentMenuTargetParent, out currentStatuses) && currentStatuses != null)
                    {
                        PositionMenusForParent(_currentMenuTargetParent, currentStatuses);
                    }
                    else
                    {
                         // Debug.LogWarning($"WSCMM: Cannot reposition menu for {_currentMenuTargetParent.name}, status unavailable.", _currentMenuTargetParent);
                    }
                    // IMPORTANT: Return here to prevent hiding this menu or showing a different one
                    return;
                }
                else
                {
                    // 2b. Mouse has moved OUTSIDE the deactivation radius. Hide the current menu.
                    HideMenusForParent(_currentMenuTargetParent);
                    // _currentMenuTargetParent is now null, allowing a potential new menu to show below.
                }
            }
            else
            {
                // 2c. Cannot get icon position for the current menu (e.g., icon/parent destroyed). Hide it.
                Debug.LogWarning($"WSCMM: Hiding menu for '{_currentMenuTargetParent.name}' because its icon position is no longer available.", _currentMenuTargetParent);
                HideMenusForParent(_currentMenuTargetParent);
                // _currentMenuTargetParent is now null.
            }
        }

        // --- 3. Handle Potential New Menu Activation ---
        // This code is reached IF:
        //   a) No menu was active initially.
        //   b) The previously active menu was just hidden (in step 2b or 2c).
        // Now, check if there's a *new potential target* within the *activation* range.
        if (_currentMenuTargetParent == null && newPotentialTarget != null)
        {
            // Get the statuses for the NEW potential target from the frame data
            List<(PlayerControlTrigger Trigger, InteractionStatus Status)> targetStatuses;
            if (frameStatuses.TryGetValue(newPotentialTarget, out targetStatuses) && targetStatuses != null)
            {
                // Show the menus for this new target
                ShowMenusForParent(newPotentialTarget, targetStatuses);
                // Update the current target parent state
                _currentMenuTargetParent = newPotentialTarget;
            }
            else
            {
                 // Debug.LogWarning($"WSCMM: Cannot show menu for potential target '{newPotentialTarget.name}', status unavailable.", newPotentialTarget);
            }
        }

        // --- 4. No Action Needed ---
        // If _currentMenuTargetParent is null (either initially or after hiding)
        // AND newPotentialTarget is also null (no icon within activation range),
        // then no menus should be shown. Nothing further needed.
    }

    #endregion

    #region Icon Management

    /// <summary>
    /// Instantiates and displays a trigger icon associated with a parent GameObject.
    /// Uses visual definitions for positioning and optional prefab overrides.
    /// </summary>
    /// <param name="parentObject">The GameObject to associate the icon with.</param>
    private void AddTriggerIcon(GameObject parentObject)
    {
        // Prevent adding if object is null or an icon already exists
        if (parentObject == null || _activeIcons.ContainsKey(parentObject))
        {
            return;
        }

        // Get the visual definition component for icon details
        PlayerControlTriggerVisualDefinition visualDef = parentObject.GetComponent<PlayerControlTriggerVisualDefinition>();
        // Require a visual definition and a valid transform to position the icon
        if (visualDef == null || visualDef.IconPositionTransform == null)
        {
            Debug.LogError($"WSCMM: Cannot add icon for '{parentObject.name}' - missing PlayerControlTriggerVisualDefinition or its IconPositionTransform is null.", parentObject);
            return;
        }

        // Store the visual definition for later use (e.g., getting position)
        _iconVisualDefinitions[parentObject] = visualDef;

        // Determine which icon prefab to use (override or default)
        GameObject prefabToInstantiate = visualDef.OverrideIconPrefab != null ? visualDef.OverrideIconPrefab : defaultIconPrefab;
        if (prefabToInstantiate == null)
        {
            Debug.LogError($"WSCMM: No valid icon prefab found (neither override nor default) for '{parentObject.name}'.", parentObject);
            return; // Cannot instantiate if no prefab is available
        }

        // Instantiate the icon prefab
        GameObject iconInstance = Instantiate(prefabToInstantiate);
        iconInstance.name = $"{prefabToInstantiate.name}_For_{parentObject.name}"; // Make debugging easier

        // Handle optional pop-in animation
        TriggerIconPopInAnimator animator = iconInstance.GetComponent<TriggerIconPopInAnimator>();
        if (animator != null)
        {
            // Initialize and enable the animator
            animator.Initialize(visualDef.IconPositionTransform); // Pass the target transform
            animator.enabled = true; // Start the animation process
        }
        else
        {
            // If no animator, parent directly and reset position/rotation
            Debug.LogWarning($"Icon prefab '{prefabToInstantiate.name}' for {parentObject.name} lacks a TriggerIconPopInAnimator component. Parenting directly.", iconInstance);
            iconInstance.transform.SetParent(visualDef.IconPositionTransform, false); // worldPositionStays = false
            iconInstance.transform.localPosition = Vector3.zero;
            iconInstance.transform.localRotation = Quaternion.identity;
        }

        // Track the active icon instance
        _activeIcons[parentObject] = iconInstance;
    }

    /// <summary>
    /// Removes and destroys the trigger icon associated with a parent GameObject.
    /// Handles optional fade-out animation.
    /// </summary>
    /// <param name="parentObject">The GameObject whose icon should be removed.</param>
    private void RemoveTriggerIcon(GameObject parentObject)
    {
        // Ignore if parent is null
        if (parentObject == null)
        {
            return;
        }

        // Remove the cached visual definition
        _iconVisualDefinitions.Remove(parentObject);

        // Try to get the active icon instance
        if (_activeIcons.TryGetValue(parentObject, out GameObject iconInstance))
        {
            if (iconInstance != null) // Check if the instance hasn't been destroyed already
            {
                // Handle optional fade-out animation
                TriggerIconPopInAnimator animator = iconInstance.GetComponent<TriggerIconPopInAnimator>();
                if (animator != null && animator.enabled)
                {
                    // Start the fade-out animation; the animator should handle destroying the object afterwards
                    animator.StartFadeOutAnimation();
                }
                else
                {
                    // If no animator or it's disabled, destroy immediately
                    Destroy(iconInstance);
                }
            }
            // Remove the entry from the tracking dictionary regardless
            _activeIcons.Remove(parentObject);
        }
    }

    /// <summary>
    /// Gets the screen-space panel position (Y=0 at top) corresponding to the world-space
    /// icon anchor transform defined in the parent's visual definition.
    /// </summary>
    /// <param name="parentObject">The parent GameObject whose icon position is needed.</param>
    /// <returns>The Vector2 panel position, or null if unobtainable.</returns>
    private Vector2? GetIconPanelPosition(GameObject parentObject)
    {
        // Check if the parent exists and we have its visual definition cached
        if (parentObject != null && _iconVisualDefinitions.TryGetValue(parentObject, out PlayerControlTriggerVisualDefinition visualDef))
        {
            // Ensure the visual definition and its transform are still valid
            if (visualDef != null && visualDef.IconPositionTransform != null)
            {
                 // Convert the world position of the icon's anchor transform to a panel position
                 return ConvertWorldToPanelPosition(visualDef.IconPositionTransform.position);
            }
            else
            {
                 // Clean up if the visual definition or its transform became invalid unexpectedly
                 _iconVisualDefinitions.Remove(parentObject);
                 if (_activeIcons.ContainsKey(parentObject))
                 {
                    // Remove the icon as well, since its positioning info is lost
                    RemoveTriggerIcon(parentObject);
                 }
            }
        }
        // Return null if the parent, visual definition, or transform is invalid/missing
        return null;
    }
    #endregion

    #region Menu Management

    /// <summary>
    /// Creates, positions, and displays control menus for a given parent object based on
    /// the provided list of visible triggers and their statuses.
    /// </summary>
    /// <param name="parentObject">The GameObject to associate the menus with.</param>
    /// <param name="triggerStatusPairs">A list containing pairs of Triggers and their calculated Status for this parent.</param>
    private void ShowMenusForParent(GameObject parentObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)> triggerStatusPairs)
    {
        // Validate required components and data
        if (parentObject == null || menuUxmlTemplate == null || _menuContainer == null || triggerStatusPairs == null)
        {
            return;
        }
        // Safeguard: If menus somehow already exist for this parent, hide them before creating new ones
        if (_activeMenus.ContainsKey(parentObject))
        {
            Debug.LogWarning($"WSCMM: Attempting to show menus for '{parentObject.name}' which already has active menus. Hiding existing ones first.", parentObject);
            HideMenusForParent(parentObject);
        }

        // Filter the provided pairs to include only those where the status indicates visibility
        var visiblePairs = triggerStatusPairs.Where(pair => pair.Status.IsVisible).ToList();
        int visibleCount = visiblePairs.Count;

        // If no triggers are currently visible for this parent, there's nothing to show
        if (visibleCount == 0)
        {
            return;
        }

        // Get the anchor position for the menus (usually the icon's screen position)
        Vector2? anchorPos = GetIconPanelPosition(parentObject);
        // If we can't determine the anchor position, we can't place the menus
        if (!anchorPos.HasValue)
        {
             Debug.LogWarning($"WSCMM: Cannot show menus for '{parentObject.name}' because its icon panel position could not be determined.", parentObject);
             return;
        }

        // Calculate the screen positions for each menu based on the anchor and count
        List<Vector2> menuPositions = CalculateMenuPositions(anchorPos.Value, visibleCount);
        List<ControlMenuDisplay> newMenus = new List<ControlMenuDisplay>(); // Track the newly created menu displays

        // Instantiate and configure a menu item for each visible trigger
        for (int i = 0; i < visibleCount; i++)
        {
            PlayerControlTrigger trigger = visiblePairs[i].Trigger; // Get the specific trigger from the filtered pair
            InteractionStatus status = visiblePairs[i].Status;   // Get the status as well (might be needed by ControlMenuDisplay)
            Vector2 position = menuPositions[i];               // Get the calculated screen position

            // Instantiate the menu item from the UXML template
            VisualElement menuVE = menuUxmlTemplate.Instantiate();
            menuVE.name = $"Menu_{parentObject.name}_{trigger.Title}"; // Helpful for debugging UI

            // Define the action to perform when this menu item is clicked
            Action clickAction = () => HandleMenuExecution(trigger);

            // Create a wrapper object to manage the menu's VE and associated data/logic
            // Pass the status in case the display needs to adapt (e.g., grey out if !IsEnabled)
            ControlMenuDisplay menuDisplay = new ControlMenuDisplay(trigger, menuVE, clickAction, interactionStatus: status);

            // Register event callbacks for locking behavior
            menuVE.RegisterCallback<PointerEnterEvent>(HandleMenuPointerEnter);
            menuVE.RegisterCallback<PointerLeaveEvent>(HandleMenuPointerLeave);

            // Set the initial screen position and make the menu visible
            menuDisplay.SetPanelPosition(position);
            menuDisplay.Show(); // Assumes ControlMenuDisplay has logic to make the VE visible

            // Add the new menu VisualElement to the designated container in the UI Document
            _menuContainer.Add(menuVE);
            newMenus.Add(menuDisplay); // Add the manager object to our list
        }

        // Store the list of active menu displays associated with this parent object
        _activeMenus[parentObject] = newMenus;
    }

    /// <summary>
    /// Hides and cleans up all menus associated with a specific parent GameObject.
    /// </summary>
    /// <param name="parentObject">The GameObject whose menus should be hidden.</param>
    private void HideMenusForParent(GameObject parentObject)
    {
        if (parentObject == null) { return; }

        // Check if there are active menus associated with this parent
        if (_activeMenus.TryGetValue(parentObject, out List<ControlMenuDisplay> menusToHide))
        {
            // Iterate through each active menu display for this parent
            foreach (ControlMenuDisplay menuDisplay in menusToHide)
            {
                VisualElement ve = menuDisplay.GetRootElement();
                // Unregister event listeners to prevent memory leaks and unwanted callbacks
                ve.UnregisterCallback<PointerEnterEvent>(HandleMenuPointerEnter);
                ve.UnregisterCallback<PointerLeaveEvent>(HandleMenuPointerLeave);

                // Perform any internal cleanup needed by the ControlMenuDisplay instance
                menuDisplay.Cleanup();

                // Remove the VisualElement from the UI hierarchy
                ve.RemoveFromHierarchy();
            }
            // Remove the entry from the active menus dictionary
            _activeMenus.Remove(parentObject);
        }

        // If the object whose menus are being hidden was the current target, clear the target reference
        if (_currentMenuTargetParent == parentObject)
        {
            _currentMenuTargetParent = null;
            // Ensure menu lock is also released if the target is cleared
            _isMenuLocked = false;
        }
    }

    /// <summary>
    /// Repositions the existing menus associated with a parent object.
    /// This is typically called when the target hasn't changed but its icon position might have.
    /// </summary>
    /// <param name="parentObject">The GameObject whose menus need repositioning.</param>
    /// <param name="triggerStatusPairs">The list of current trigger/status pairs (used to confirm visibility and count potentially).</param>
    private void PositionMenusForParent(GameObject parentObject, List<(PlayerControlTrigger Trigger, InteractionStatus Status)> triggerStatusPairs)
    {
        // Validate parent, status data, and check if menus actually exist for this parent
        if (parentObject == null || triggerStatusPairs == null || !_activeMenus.TryGetValue(parentObject, out List<ControlMenuDisplay> menusToPosition) || menusToPosition.Count == 0)
        {
            return; // Nothing to position
        }

        // Get the current anchor position (icon position)
        Vector2? anchorPos = GetIconPanelPosition(parentObject);
        if (!anchorPos.HasValue)
        {
            // If anchor position is lost, we can't reposition. Consider hiding the menus instead.
            // HideMenusForParent(parentObject); // Option: Hide if position is lost
            Debug.LogWarning($"WSCMM: Cannot reposition menus for '{parentObject.name}', icon position unavailable.", parentObject);
            return;
        }

        // --- Determine Menu Count for Positioning ---
        // Option 1 (Current): Assume the number of *visible* menus hasn't changed drastically since ShowMenus.
        // Use the count of *existing* menu display objects for the layout calculation.
        // This is simpler and works well for locked menus where the set shouldn't change.
        int menuCountForLayout = menusToPosition.Count;

        // Option 2 (More Robust, Complex): Recalculate visible count from current statuses.
        // int visibleCountNow = triggerStatusPairs.Count(pair => pair.Status.IsVisible);
        // if (visibleCountNow == 0) { HideMenusForParent(parentObject); return; }
        // if (visibleCountNow != menusToPosition.Count) {
        //    // The set of visible menus changed. Recreate them instead of just repositioning.
        //    HideMenusForParent(parentObject);
        //    ShowMenusForParent(parentObject, triggerStatusPairs); // Pass current statuses
        //    return;
        // }
        // menuCountForLayout = visibleCountNow; // Use the recalculated count

        // Calculate new positions based on the current anchor and menu count
        List<Vector2> menuPositions = CalculateMenuPositions(anchorPos.Value, menuCountForLayout);

        // Apply the new positions to the existing menu display objects
        // Use Mathf.Min for safety, although counts should match if using Option 1 above.
        for (int i = 0; i < Mathf.Min(menusToPosition.Count, menuPositions.Count); i++)
        {
            // Update the screen position of each menu's VisualElement via its controller
            menusToPosition[i].SetPanelPosition(menuPositions[i]);
            // Potentially update status display within the menu if needed
            // menusToPosition[i].UpdateStatus(triggerStatusPairs.FirstOrDefault(p => p.Trigger == menusToPosition[i].AssociatedTrigger).Status);
        }
    }

    /// <summary>
    /// Calculates the screen positions for multiple menus arranged in a circle (or arc)
    /// around a central anchor point.
    /// </summary>
    /// <param name="anchorPanelPos">The central anchor position in panel coordinates (Y=0 at top).</param>
    /// <param name="menuCount">The number of menus to position.</param>
    /// <returns>A list of calculated Vector2 positions for each menu.</returns>
    private List<Vector2> CalculateMenuPositions(Vector2 anchorPanelPos, int menuCount)
    {
        List<Vector2> positions = new List<Vector2>();
        if (menuCount <= 0)
        {
            return positions; // No positions needed for zero menus
        }

        // Special case: Single menu slightly above the anchor
        if (menuCount == 1)
        {
            // Position it directly above the anchor point, adjusting Y negatively (upwards)
            positions.Add(anchorPanelPos + new Vector2(0, -menuCircleRadius * 0.8f)); // Use 80% of radius for closer placement
            return positions;
        }

        // Calculate positions for multiple menus in a circle/arc
        float angleStep = 360f / menuCount; // Angle between each menu item
        float startAngleDeg = 90f;         // Start positioning from the top (90 degrees)

        for (int i = 0; i < menuCount; i++)
        {
            // Calculate the angle for the current menu item (clockwise from the top)
            float currentAngleDeg = startAngleDeg - (i * angleStep);
            // Convert degrees to radians for trigonometric functions
            float currentAngleRad = currentAngleDeg * Mathf.Deg2Rad;

            // Calculate the offset from the anchor point using trigonometry
            // Cos(angle) for X, -Sin(angle) for Y (because Y is inverted in panel space)
            Vector2 offset = new Vector2(Mathf.Cos(currentAngleRad), -Mathf.Sin(currentAngleRad)) * menuCircleRadius;

            // Add the offset to the anchor position to get the final menu position
            positions.Add(anchorPanelPos + offset);
        }
        return positions;
    }

    #endregion

    #region Event Handlers & Callbacks

    /// <summary>
    /// Called when the mouse pointer enters the bounds of a menu VisualElement.
    /// Locks the menu visibility to prevent it from hiding due to proximity changes.
    /// </summary>
    private void HandleMenuPointerEnter(PointerEnterEvent evt)
    {
        // If there's a currently targeted parent (whose menus are showing), lock the menu state
        if (_currentMenuTargetParent != null)
        {
            // Debug.Log($"Locking menu position due to pointer enter");
            _isMenuLocked = true;
        }
    }

    /// <summary>
    /// Called when the mouse pointer leaves the bounds of a menu VisualElement.
    /// Unlocks the menu visibility *only* if the pointer moves outside the entire group of active menus.
    /// </summary>
    private void HandleMenuPointerLeave(PointerLeaveEvent evt)
    {
        // Only proceed if the menu is currently locked and there's a target parent
        if (!_isMenuLocked || _currentMenuTargetParent == null)
        {
            return;
        }

        _isMenuLocked = false;
    }

    /// <summary>
    /// Handles the execution of an action when a specific menu item is clicked.
    /// Delegates the interaction to the PlayerManager and then hides the menus.
    /// </summary>
    /// <param name="triggerToExecute">The PlayerControlTrigger associated with the clicked menu item.</param>
    private void HandleMenuExecution(PlayerControlTrigger triggerToExecute)
    {
        if (triggerToExecute == null || _playerManagerInstance == null)
        {
             Debug.LogError("WSCMM: Cannot execute menu action. Trigger or PlayerManager is null.", triggerToExecute?.gameObject);
             return;
        }

        // Use PlayerManager to handle the interaction logic associated with the trigger
        // Debug.Log($"Executing trigger: {triggerToExecute.Title} on {triggerToExecute.gameObject.name}");
        _playerManagerInstance.HandleTriggerInteraction(triggerToExecute);

        // Hide the menus associated with the target parent after execution
        if (_currentMenuTargetParent != null)
        {
            HideMenusForParent(_currentMenuTargetParent);
            // Note: HideMenusForParent also sets _currentMenuTargetParent to null and _isMenuLocked to false
        }
        else {
             // If target was somehow null, ensure lock is still released
             _isMenuLocked = false;
        }
    }
    #endregion

    #region Coordinate Helpers

    /// <summary>
    /// Gets the IPanel associated with the UIDocument, caching it for efficiency.
    /// </summary>
    /// <returns>The IPanel interface, or null if unavailable.</returns>
    private IPanel GetPanel()
    {
        // Return cached panel if available
        if (_cachedPanel == null && _uiDocument?.rootVisualElement != null)
        {
            // Cache the panel reference the first time it's needed
            _cachedPanel = _uiDocument.rootVisualElement.panel;
        }
        return _cachedPanel;
    }

    /// <summary>
    /// Converts a world-space position to a UI Toolkit panel position (origin top-left).
    /// Handles points behind the camera.
    /// </summary>
    /// <param name="worldPos">The world-space position to convert.</param>
    /// <returns>The corresponding panel position (Vector2), or null if conversion fails or point is behind camera.</returns>
    private Vector2? ConvertWorldToPanelPosition(Vector3 worldPos)
    {
        if (viewCamera == null) return null;
        IPanel panel = GetPanel();
        if (panel == null) return null;

        // Convert world position to screen coordinates (pixels, origin bottom-left)
        // screenPoint3D.z is distance from camera plane
        Vector3 screenPoint3D = viewCamera.WorldToScreenPoint(worldPos);

        // Check if the point is behind the camera's near clipping plane
        if (screenPoint3D.z < viewCamera.nearClipPlane)
        {
            return null; // Point is behind the camera, not visible on screen
        }

        // Convert screen coordinates (Vector2 part) to panel coordinates (origin bottom-left)
        // This accounts for panel scaling and offset within the screen.
        Vector2 panelPosBottomOrigin = RuntimePanelUtils.ScreenToPanel(panel, screenPoint3D);

        // Get the actual resolved height of the panel's root visual element
        float panelHeight = panel.visualTree.resolvedStyle.height;

        // Fallback to camera pixel height if panel height is invalid (e.g., during initial layout, or if panel covers whole screen)
        if (float.IsNaN(panelHeight) || float.IsInfinity(panelHeight) || panelHeight <= 0)
        {
            panelHeight = viewCamera.pixelHeight; // Use camera's render target height
            if (panelHeight <= 0) return null; // Cannot determine height
        }

        // Convert Y coordinate from bottom-left origin to top-left origin
        // Panel Y (top-left) = Panel Height - Panel Y (bottom-left)
        return new Vector2(panelPosBottomOrigin.x, panelHeight - panelPosBottomOrigin.y);
    }

    /// <summary>
    /// Converts a screen-space position (e.g., mouse position, origin bottom-left)
    /// to a UI Toolkit panel position (origin top-left).
    /// </summary>
    /// <param name="screenPos">The screen-space position (pixels, origin bottom-left).</param>
    /// <returns>The corresponding panel position (Vector2), or null if conversion fails.</returns>
    private Vector2? ConvertScreenToPanelPosition(Vector2 screenPos)
    {
        IPanel panel = GetPanel();
        if (panel == null) return null;

        // Convert screen position directly to panel position (origin bottom-left)
        Vector2 panelPosBottomOrigin = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

        // Get panel height to convert Y coordinate (origin top-left)
        float panelHeight = panel.visualTree.resolvedStyle.height;

        // Fallback to camera pixel height if panel height is invalid
        if (float.IsNaN(panelHeight) || float.IsInfinity(panelHeight) || panelHeight <= 0)
        {
             panelHeight = viewCamera.pixelHeight;
             if (panelHeight <= 0) return null; // Cannot determine height
        }

        // Convert Y coordinate: Panel Y (top-left) = Panel Height - Panel Y (bottom-left)
        return new Vector2(panelPosBottomOrigin.x, panelHeight - panelPosBottomOrigin.y);
    }

    #endregion

    #region Cleanup Helper
    /// <summary>
    /// Cleans up all active icons and menus managed by this script.
    /// Typically called during OnDisable or OnDestroy.
    /// </summary>
    private void CleanupAllUI()
    {
        // --- Clean up Icons ---
        // Create a copy of keys to avoid modification during iteration issues
        List<GameObject> iconKeys = new List<GameObject>(_activeIcons.Keys);
        foreach (GameObject parentObject in iconKeys)
        {
            // Use the existing RemoveTriggerIcon method which handles animation and destruction
            RemoveTriggerIcon(parentObject);
        }
        // Ensure dictionaries are cleared even if RemoveTriggerIcon failed somehow
        _activeIcons.Clear();
        _iconVisualDefinitions.Clear();

        // --- Clean up Menus ---
        // Create a copy of keys
        List<GameObject> menuKeys = new List<GameObject>(_activeMenus.Keys);
        foreach (GameObject parentObject in menuKeys)
        {
            // Use the existing HideMenusForParent method which handles unregistering, cleanup, and removal
            HideMenusForParent(parentObject);
        }
        // Ensure dictionary is cleared
        _activeMenus.Clear();

        // Reset related state just in case
        _currentMenuTargetParent = null;
        _isMenuLocked = false;
    }
    #endregion
}