using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;



public class InteractionMenuUIManager : AbstractFloatingUIManager<InteractionMenuFloatingUIConfig>
{
    [SerializeField] private InteractionMenuVisualDefinitionSO interactionMenuVisualDefinition;
    
    [SerializeField]
    [Tooltip("Default prefab for the trigger icon displayed in world space.")]
    private GameObject defaultIconPrefab;
    
    [SerializeField] private float maxWidth = 40f;
    [SerializeField] private float minWidth = 30f;

    [SerializeField] private Sprite isSuspiciousIcon;

    [SerializeField] private float verticalOffset = 0f;

    [SerializeField] private bool keepOnScreen = true;
    
    private LayerMask IconTriggerLayerMask;
    private static string IconTriggerLayerName = "PlayerControlTriggerIconTrigger";
    private LayerMask MenuTriggerLayerMask;
    private static string MenuTriggerLayerName = "PlayerControlTriggerMenuTrigger";
    
    private EventCallback<PointerEnterEvent> pointerEnterCallback;
    private EventCallback<PointerLeaveEvent> pointerLeaveCallback;
    
    private FloaterData currentFloaterData;
    
    private HashSet<GameObject> activeIconTriggerGOs = new HashSet<GameObject>();
    private Dictionary<GameObject, GameObject> iconInstances = new Dictionary<GameObject, GameObject>();
    private GameObject activeMenuTriggerGO = null;  // If not null, then there is a menu open right now. Might be redundant with currentFloaterData. 
    private bool isHoveringMenu = false;  // Used to ensure that we do not close the menu when the mouse is over it.
    
    protected override bool OnSetupFloater(VisualElement floaterRoot, InteractionMenuFloatingUIConfig floaterConfig)
    {
        // Store the lambdas in the class-level fields so that we can unregister them later.
        pointerEnterCallback = evt => isHoveringMenu = true;
        pointerLeaveCallback = evt => isHoveringMenu = false;
        floaterRoot.RegisterCallback(pointerEnterCallback);
        floaterRoot.RegisterCallback(pointerLeaveCallback);
        
        InteractionMenuInstanceManager instanceManager = new InteractionMenuInstanceManager(
            floaterRoot,
            floaterConfig.visualDefinition,
            floaterConfig.TargetInteractable,
            floaterConfig.TargetInteractableDefinition,
            floaterConfig.Triggers
        );
        instanceManager.UpdateActions(CloseMenu);
        floaterConfig.InstanceManager = instanceManager;

        return true;
    }

    protected override void OnUpdateFloater(VisualElement floaterRoot, InteractionMenuFloatingUIConfig floaterConfig)
    {
        // We only update the interaction definition buttons as the title and description are expected to be static.
        floaterConfig.InstanceManager.UpdateActions(CloseMenu);
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

    private LayerMask GetLayerMaskFromName(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"Layer '{layerName}' does not exist. Please check the layer name.");
            return 0;
        }
        return 1 << layer;  // Convert layer to a LayerMask.
    }

    protected override void Awake()
    {
        base.Awake();
        
        IconTriggerLayerMask = GetLayerMaskFromName(IconTriggerLayerName);
        MenuTriggerLayerMask = GetLayerMaskFromName(MenuTriggerLayerName);
    }

    private void CreateIcon(GameObject triggerParent)
    {
        // Get the visual definition component for icon details
        PlayerControlTriggerVisualDefinition visualDef = triggerParent.GetComponent<PlayerControlTriggerVisualDefinition>();
        // Require a visual definition and a valid transform to position the icon
        if (visualDef == null || visualDef.IconPositionTransform == null)
        {
            Debug.LogError($"WSCMM: Cannot add icon for '{triggerParent.transform.parent.name}' - missing PlayerControlTriggerVisualDefinition or its IconPositionTransform is null.", triggerParent);
            return;
        }

        // Determine which icon prefab to use (override or default)
        GameObject prefabToInstantiate = visualDef.OverrideIconPrefab != null ? visualDef.OverrideIconPrefab : defaultIconPrefab;
        if (prefabToInstantiate == null)
        {
            Debug.LogError($"WSCMM: No valid icon prefab found (neither override nor default) for '{triggerParent.transform.parent.name}'.", triggerParent);
            return; // Cannot instantiate if no prefab is available
        }

        // Instantiate the icon prefab
        GameObject iconInstance = Instantiate(prefabToInstantiate);
        iconInstance.name = $"{prefabToInstantiate.name}_For_{triggerParent.transform.parent.name}"; // Make debugging easier

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
            Debug.LogWarning($"Icon prefab '{prefabToInstantiate.name}' for {triggerParent.transform.parent.name} lacks a TriggerIconPopInAnimator component. Parenting directly.", iconInstance);
            iconInstance.transform.SetParent(visualDef.IconPositionTransform, false); // worldPositionStays = false
            iconInstance.transform.localPosition = Vector3.zero;
            iconInstance.transform.localRotation = Quaternion.identity;
        }
        
        iconInstances.Add(triggerParent, iconInstance);
    }

    private void RemoveIcon(GameObject triggerParent)
    {
        // Try to get the active icon instance
        if (iconInstances.TryGetValue(triggerParent, out GameObject iconInstance))
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
            // Remove the icon from the dictionary
            iconInstances.Remove(triggerParent);
        }
    }

    private void Update()
    {
        // Step 1: Handle Icons
        var newActiveIconTriggers = GetActiveIconTriggers();

        // Remove icons for triggers that are no longer active
        activeIconTriggerGOs.RemoveWhere(triggerGO =>
        {
            if (!newActiveIconTriggers.Contains(triggerGO))
            {
                RemoveIcon(triggerGO);
                return true; // Remove from the hash set
            }
            return false;
        });

        // Add icons for newly activated triggers
        foreach (var triggerGO in newActiveIconTriggers)
        {
            if (activeIconTriggerGOs.Add(triggerGO)) // .Add returns true if the item was new
            {
                CreateIcon(triggerGO);
            }
        }
    
        // Step 2: Check if we should open, close, or swap the menu.
    
        // Get the closest active menu trigger. FirstOrDefault is safe for empty lists.
        // PlayerControlTrigger closestTrigger = GetActiveMenuTriggers().FirstOrDefault();
        GameObject closestTriggerGO = GetActiveMenuTriggers().FirstOrDefault();
        
        // Case 1: We are still hovering over the same trigger that has its menu open.
        // Do absolutely nothing to prevent flickering.
        if ((activeMenuTriggerGO != null && closestTriggerGO == activeMenuTriggerGO) || isHoveringMenu)
        {
            return; 
        }

        // Case 2: We are hovering a trigger (either a new one, or the first one).
        if (closestTriggerGO != null)
        {
            // Open a menu for this trigger. OpenNewMenu already handles closing any old menu.
            OpenNewMenu(closestTriggerGO);
        }
        // Case 3: We are not hovering any trigger.
        else
        {
            // If a menu is open and we are not hovering over the UI panel itself, close it.
            bool isMenuOpen = currentFloaterData is { IsDestroyed: false };
            if (isMenuOpen)
            {
                CloseMenu();
            }
        }
    }

    private bool OpenNewMenu(GameObject newActiveMenuTriggerGO)
    {
        // If a menu is open, we need to close it first.
        if (currentFloaterData != null && !currentFloaterData.IsDestroyed)
        {
            CloseMenu();
        }
        
        Interactable interactable = newActiveMenuTriggerGO.transform.parent.GetComponent<Interactable>();
        if (interactable == null)
        {
            Debug.LogError($"WSCMM: Cannot open menu for '{newActiveMenuTriggerGO.name}' - missing Interactable component.", newActiveMenuTriggerGO);
            return false; // Cannot open a menu without an interactable
        }
        
        var config = InteractionMenuInstanceManager.GenerateFloatingUIConfig(
            interactable,
            interactionMenuVisualDefinition,

            positionType: FloatingUIPositionType.Transform,
            targetObject: newActiveMenuTriggerGO.transform,
            maxWidth: maxWidth,
            minWidth: minWidth,
            verticalOffset: verticalOffset,
            keepOnScreen: keepOnScreen
        );
        
        var floaterData = CreateFloater(config);
        if (floaterData == null)
        {
            // Failed to create floater, log an error
            Debug.LogError("Failed to create Speech Bubble floater.", this);
            return false;
        }
        
        currentFloaterData = floaterData;
        activeMenuTriggerGO = newActiveMenuTriggerGO;

        return true;
    }

    private void CloseMenu()
    {
        RemoveFloater(currentFloaterData.Id);
        
        currentFloaterData = null;
        activeMenuTriggerGO = null;
        isHoveringMenu = false; // Reset hover state when closing the menu
    }

    #region Raycast Helpers

    /// <summary>
    /// Gets a list of all player control triggers for which a raycast through the mouse passed through the
    /// icon trigger collider.
    /// This is effectively just getting the parent of all colliders that the raycast hit on the IconTriggerLayerMask
    /// </summary>
    /// <returns></returns>
    private List<GameObject> GetActiveIconTriggers()
    {
        // Create a ray from the camera through the mouse position.
        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);

        // Perform the raycast, getting all colliders hit on the specified layer.
        // Physics.RaycastAll does not guarantee order.
        return Physics.RaycastAll(ray, Mathf.Infinity, IconTriggerLayerMask)
            // For each hit, get the transform of the parent object.
            .Select(hit => hit.collider.transform.parent.gameObject)
            // Filter out any hits where the collider has no parent.
            .Where(parent => parent != null)
            .Where(triggerGO => triggerGO.transform.parent != PlayerManager.Instance.CurrentFocusedNpc?.gameObject.transform)
            // Ensure the list contains only unique triggers, in case multiple child colliders
            // of the same trigger were hit.
            .Distinct()
            // Convert the result to a List and return it.
            .ToList();
    }

    /// <summary>
    /// Gets a list of all player control triggers for which a raycast through the mouse passed through the menu
    /// trigger collider.
    /// Returns the list ordered by increasing distance so that the closest trigger is first.
    /// </summary>
    /// <returns></returns>
    private List<GameObject> GetActiveMenuTriggers()
    {
        // Create a ray from the camera through the mouse position.
        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
        
        // Perform the raycast, getting all colliders hit on the specified layer.
        return Physics.RaycastAll(ray, Mathf.Infinity, MenuTriggerLayerMask)
            // IMPORTANT: Order the hits by their distance from the ray's origin (the camera).
            .OrderBy(hit => hit.distance)
            // For each hit (now sorted by distance), get the transform of the parent object.
            .Select(hit => hit.collider.transform.parent.gameObject)
            // Filter out any hits where the collider has no parent.
            .Where(parent => parent != null)
            .Where(triggerGO => triggerGO.transform.parent != PlayerManager.Instance.CurrentFocusedNpc.gameObject.transform)
            // Because we sorted by distance *before* processing, Distinct() will keep the *first*
            // instance it finds of each trigger, which corresponds to the closest one.
            .Distinct()
            // Convert the result to a List and return it.
            .ToList();
    }
    
    #endregion
}