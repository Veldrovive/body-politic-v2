using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum PlayerManagerSelectionMode
{
    TargetMove,
    PlaceItem
}

public class PlayerManagerSaveableData : SaveableData
{
    public GameObject CurrentFocusedNpcId;
}

/// <summary>
/// Manages the player's current focus (NPC), handles input for focus switching
/// and interactions, relays trigger events, and sends commands to the focused NPC's CharacterModeController.
/// </summary>
[DefaultExecutionOrder(-85)]
public class PlayerManager : SaveableGOConsumer
{
    // --- Dependencies ---
    [Header("Dependencies")]
    [SerializeField] private InputManager inputManager; // [cite: 988]
    [SerializeField] private GameObject movementMarkerPrefab;
    [SerializeField] private float movementMarkerSnapSpeed = 100f;

    // --- Focus Management ---
    [Header("Focus Management")]
    [Tooltip("The NPC to focus on initially. Must be in the Controllable NPCs list.")]
    [SerializeField] private NpcContext defaultFocusedNpc; // [cite: 990]
    [Tooltip("List of NPCs the player can potentially control or focus on.")]
    [SerializeField] private List<NpcContext> controllableNpcs = new List<NpcContext>(); // [cite: 991]

    [SerializeField] private float itemPlaceDistance = 3.0f;
    
    // --- Keybinds ---
    [Header("Keybinds")]
    [Tooltip("Key to press to force the focused NPC back into Routine mode.")]
    [SerializeField] private KeyCode returnToRoutineKey = KeyCode.R;
    [Tooltip("Key to press to cycle focus to the next controllable NPC.")]
    [SerializeField] private KeyCode cycleFocusKey = KeyCode.N;
    [Tooltip("Key to press to activate item placing mode.")]
    [SerializeField] private KeyCode itemPlacingKey = KeyCode.B;
    [Tooltip("Key for quicksave")]
    [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
    [Tooltip("Key for quickload")]
    [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;
    [Tooltip("Key to hold to overwrite the player controller queue instead of appending.")]
    [SerializeField] private KeyCode overwriteQueueModifier = KeyCode.LeftShift;
    
    [Header("Configuration")]
    [SerializeField] MovementSpeed defaultMovementSpeed = MovementSpeed.NpcSpeed;


    // --- Events ---
    /// <summary>
    /// Fired when the focused NPC changes.
    /// Arg1: Previous NPC (can be null). Arg2: New NPC (can be null if focus is lost).
    /// </summary>
    public event Action<NpcContext, NpcContext> OnFocusChanged; // [cite: 995]

    public event Action<List<NpcContext>> OnControlledNpcsChanged;

    // --- Singleton ---
    public static PlayerManager Instance { get; private set; } // [cite: 1003]

    // --- Private State ---
    private PlayerManagerSelectionMode currentSelectionMode = PlayerManagerSelectionMode.TargetMove;
    private NpcContext currentFocusedNpc;
    public NpcContext CurrentFocusedNpc => currentFocusedNpc;
    
    private GameObject movementMarkerInstance;
    /// <summary>
    /// Stores the result of the MovementManager CanSatisfyRequest call.
    /// If there is no path, this is set to null. If there is a path, this is set to the true destination.
    /// Clicking on the ground will try to navigate to this position, not the mouse position.
    /// </summary>
    private Vector3? mouseHoverMovementDestination = null;

    private Holdable itemPlaceHoldable = null;
    private Vector3? itemPlacePosition = null;

    public override SaveableData GetSaveData()
    {
        return new PlayerManagerSaveableData
        {
            CurrentFocusedNpcId = currentFocusedNpc.gameObject
        };
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            if (data is PlayerManagerSaveableData playerData)
            {
                if (playerData.CurrentFocusedNpcId != null)
                {
                    // Try to find the NPC in the controllable list
                    NpcContext npc = controllableNpcs.FirstOrDefault(n => n.gameObject == playerData.CurrentFocusedNpcId);
                    if (npc != null)
                    {
                        SetFocus(npc);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find NPC with ID {playerData.CurrentFocusedNpcId.name} in controllable NPCs. Focus not set.", this);
                        InitializeFocus();
                    }
                }
                else
                {
                    Debug.LogWarning("PlayerManagerSaveableData has no CurrentFocusedNpcId. Focus not set.", this);
                }
            }
            else
            {
                Debug.LogError("Invalid save data type for PlayerManager. Expected PlayerManagerSaveableData.", this);
            }
        }
        else
        {
            InitializeFocus();
        }
        
        if (inputManager != null)
        {
            inputManager.OnClicked += HandleClicked;
            inputManager.OnHoverChanged += HandleHover;
            inputManager.OnKeyPressed += HandleKeyPressed;
            inputManager.OnHeldModifierKeysChanged += HandleHeldModifierKeysChanged; // Keep subscription even if unused for now
        }
        else
        {
            Debug.LogWarning("PlayerManager: InputManager not found in Start. Input handling will be disabled.", this);
        }

        if (currentFocusedNpc != null)
        {
            OnFocusChanged?.Invoke(null, currentFocusedNpc);
        }
    }

    /// <summary> Initializes the Singleton, dependencies, focus, and validates references. </summary>
    void Awake() // [cite: 1006]
    {
        if (Instance != null)
        {
            Debug.LogError("Duplicate PlayerManager instance detected. Destroying this one.", gameObject);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
            if (inputManager == null)
                Debug.LogError("PlayerManager could not find an instance of InputManager. Interactions will not work.", this);
        }
        
        // Create the movement marker instance
        if (movementMarkerPrefab != null)
        {
            movementMarkerInstance = Instantiate(movementMarkerPrefab);
            movementMarkerInstance.SetActive(false); // Start inactive
        }
        else
        {
            Debug.LogError("Movement marker prefab is not assigned. Movement marker will not be created.", this);
        }
    }

    private void Update()
    {
        if (currentSelectionMode == PlayerManagerSelectionMode.TargetMove)
        {
            // Visualize where the player is trying to move
            if (movementMarkerInstance != null)
            {
                if (mouseHoverMovementDestination.HasValue)
                {
                    if (!movementMarkerInstance.activeSelf)
                    {
                        movementMarkerInstance.SetActive(true);
                        movementMarkerInstance.transform.position = mouseHoverMovementDestination.Value;
                        // Cursor.visible = false;
                    }
                    Vector3 currentPosition = movementMarkerInstance.transform.position;
                    Vector3 newPosition = Vector3.Lerp(currentPosition, mouseHoverMovementDestination.Value, movementMarkerSnapSpeed * Time.deltaTime);
                    movementMarkerInstance.transform.position = newPosition;
                }
                else
                {
                    if (movementMarkerInstance.activeSelf)
                    {   
                        movementMarkerInstance.SetActive(false);
                        // Cursor.visible = true;
                    }
                }
            }
        }
        else
        {
            movementMarkerInstance.SetActive(false);
        }
    }

    /// <summary> Unsubscribes from all events when destroyed. </summary>
    void OnDestroy() // [cite: 1022]
    {
        if (inputManager != null)
        {
            inputManager.OnClicked -= HandleClicked;
            inputManager.OnHoverChanged -= HandleHover;
            inputManager.OnKeyPressed -= HandleKeyPressed;
            inputManager.OnHeldModifierKeysChanged -= HandleHeldModifierKeysChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    //--------------------------------------------------------------------------
    // Initialization Logic
    //--------------------------------------------------------------------------
    /// <summary> Determines the initial NPC focus based on settings. </summary>
    private void InitializeFocus() // [cite: 1048]
    {
        if (currentFocusedNpc != null)
        {
            // We're actually already done. It was initialized externally.
            return;
        }
        
        if (defaultFocusedNpc != null)
        {
            if (controllableNpcs.Contains(defaultFocusedNpc))
            {
                currentFocusedNpc = defaultFocusedNpc;
            }
            else
            {
                // Debug.LogError($"Default focused NPC '{defaultFocusedNpc.gameObject.name}' is not present in the controllable NPCs list.", this);
                if (controllableNpcs.Count > 0)
                {
                    currentFocusedNpc = controllableNpcs[0];
                    // Debug.LogWarning($"Falling back to the first controllable NPC: {currentFocusedNpc.gameObject.name}", this);
                }
                else
                {
                     // Debug.LogError("No controllable NPCs assigned. PlayerManager cannot establish an initial focus.", this);
                }
            }
        }
        else if (controllableNpcs.Count > 0)
        {
             // Debug.LogWarning("No default focused NPC assigned. Using the first controllable NPC.", this);
             currentFocusedNpc = controllableNpcs[0];
        }
        else
        {
             // Debug.LogError("No default focused NPC OR controllable NPCs assigned. PlayerManager cannot function without an initial focus.", this);
        }
    }

    //--------------------------------------------------------------------------
    // Input Handling
    //--------------------------------------------------------------------------

    /// <summary> Handles click events for focus changes or interactions. </summary>
    void HandleClicked(InputManager.ClickState clickState) // [cite: 1061]
    {
        if (clickState.OverUI) return;  // This will be handled by the UI managers

        if (clickState.Button == InputManager.MouseButton.Left)
        {
            if (currentSelectionMode == PlayerManagerSelectionMode.TargetMove)
            {
                SetCurrentNpcMoveTarget(); // Sets the current NPC's move target to the mouseHoverMovementDestination
            }
            else if (currentSelectionMode == PlayerManagerSelectionMode.PlaceItem)
            {
                DropHeldItemAndSetPosition();  // Places the currently held item at the itemPlacePosition
            }
        }
    }

    void HandleHover(InputManager.HoverState hoverState)
    {
        if (hoverState.HasHit)
        {
            // Then we are hovering over something that is not UI

            if (currentSelectionMode == PlayerManagerSelectionMode.TargetMove)
            {
                // TODO: Consider checking whether the hit GameObject has a NavMeshSurface component
                HandleMovementPositionTargetHover(hoverState.WorldPosition);
            }
            else if (currentSelectionMode == PlayerManagerSelectionMode.PlaceItem)
            {
                SetHeldItemPlacePosition(hoverState.WorldPosition);
            }
        }
        else
        {
            // Debug.Log("No Hit");
            mouseHoverMovementDestination = null;
        }
    }
    
    /// <summary> Handles discrete key press events for focus cycling and returning to routine. </summary>
    void HandleKeyPressed(InputManager.KeyState keyState) // [cite: 1065]
    {
        if (keyState.Key == quickSaveKey)
        {
            SaveableDataManager.Instance.CreateSave();
        }

        if (keyState.Key == quickLoadKey)
        {
            SceneLoadManager.Instance.QuickLoad();
        }
        else if (keyState.Key == cycleFocusKey) // Use configured key [cite: 1066]
        {
            CycleCurrentFocusedNpc();
        }
        else if (keyState.Key == returnToRoutineKey) // Use configured key
        {
            HandleReturnToRoutineCommand();
        }
        else if (keyState.Key == itemPlacingKey)
        {
            StartItemPlaceMode();
        }
        // Add other keybinds here if needed
    }

    /// <summary> Handles changes in held keys (currently unused). </summary>
    void HandleHeldModifierKeysChanged(InputManager.HeldKeyState heldKeyState) { /* Placeholder */ } // [cite: 1067]


    //--------------------------------------------------------------------------
    // Focus Management
    //--------------------------------------------------------------------------
    /// <summary> Sets the currently focused NPC and fires the OnFocusChanged event. </summary>
    public void SetFocus(NpcContext npcToFocus) // [cite: 1068]
    {
        if (npcToFocus == null)
        {
            // We no longer have any focusable NPCs
            currentFocusedNpc = null;
            EndItemPlaceMode();
            Debug.LogWarning($"TODO: Implement level failure");
        }
        else
        {
            if (npcToFocus == currentFocusedNpc) return;
            if (!controllableNpcs.Contains(npcToFocus))
            {
                Debug.LogWarning($"Attempted to focus on NPC '{npcToFocus.gameObject.name}' which is not in the controllable list.", this);
                return;
            }

            NpcContext previousFocusedNpc = currentFocusedNpc;
            currentFocusedNpc = npcToFocus;
            // Debug.Log($"Player focus changed to: {currentFocusedNpc.gameObject.name}", this);
        
            EndItemPlaceMode();

            // Update visibility of nearby triggers based on the new NPC's context
            // UpdateActiveTriggersVisibility();

            OnFocusChanged?.Invoke(previousFocusedNpc, currentFocusedNpc);
        }
    }


    /// <summary> Gets the currently focused NPC. </summary>
    public NpcContext GetFocusedNpc() // [cite: 1075]
    {
        return currentFocusedNpc; // [cite: 1076]
    }


    /// <summary> Cycles focus to the next controllable NPC. </summary>
    public void CycleCurrentFocusedNpc() // [cite: 1077]
    {
        // Logic remains the same
        if (controllableNpcs.Count <= 1) return;
        int currentIndex = (currentFocusedNpc != null) ? controllableNpcs.IndexOf(currentFocusedNpc) : -1;
        int nextIndex = (currentIndex + 1) % controllableNpcs.Count;
        SetFocus(controllableNpcs[nextIndex]);
    }

    public void AddControllableNpc(NpcContext npc) // [cite: 1080]
    {
        if (npc == null || controllableNpcs.Contains(npc)) return;
        controllableNpcs.Add(npc);

        OnControlledNpcsChanged?.Invoke(controllableNpcs);
    }

    public void RemoveControllableNpc(NpcContext npc) // [cite: 1082]
    {
        if (npc == null || !controllableNpcs.Contains(npc)) return;
        // If the NPC is currently focused, cycle focus to the next one
        if (currentFocusedNpc == npc)
        {
            CycleCurrentFocusedNpc();
        }

        if (currentFocusedNpc == npc)
        {
            // The player only had one NPC in the controllable list, so we clear the focus
            SetFocus(null);
        }
        controllableNpcs.Remove(npc);
        Debug.Log($"Removed NPC '{npc.gameObject.name}' from controllable list.", this);

        OnControlledNpcsChanged?.Invoke(controllableNpcs);
    }

    public void SetControllableNpcs(List<NpcContext> npcs, bool focusFirst = false)
    {
        controllableNpcs = npcs;
        bool needsNewFocus = currentFocusedNpc == null || !controllableNpcs.Contains(currentFocusedNpc);
        // Debug.Log($"Set controllable NPCs list to {controllableNpcs.Count} items.", this);

        OnControlledNpcsChanged?.Invoke(controllableNpcs);

        if ((focusFirst && controllableNpcs.Count > 0) || needsNewFocus)
        {
            SetFocus(controllableNpcs[0]);
        }
    }
    
    /// <summary>
    /// Handles interaction logic when a PlayerControlTrigger is clicked.
    /// Tells the focused NPC's CharacterModeController to switch to Player mode and execute the trigger's step.
    /// </summary>
    public void HandleTriggerInteraction(PlayerControlTrigger clickedTrigger)
    {
        if (currentFocusedNpc == null)
        {
            Debug.LogWarning("Cannot handle trigger interaction: No NPC focused.", this);
            return;
        }
        if (clickedTrigger == null) return;
    
        // Check if the specific action is actually possible right now
        InteractionStatus status = clickedTrigger.GetActionStatus(currentFocusedNpc.gameObject);
        if (!status.CanInteract(true))
        {
             Debug.LogWarning($"Cannot interact with trigger '{clickedTrigger.gameObject.name}': Action not allowed for {currentFocusedNpc.gameObject.name}. Reasons: [{string.Join(", ", status.FailureReasons)}]", clickedTrigger);
             // Optionally provide UI feedback here
             return;
        }

        AbstractGraphFactory factory = clickedTrigger.GetGraphDefinition(currentFocusedNpc);
        if (factory == null)
        {
            Debug.LogError($"Trigger '{clickedTrigger.gameObject.name}' failed to generate a valid graph definition.", clickedTrigger);
            return;
        }
        
        StateGraphController controller = currentFocusedNpc.StateGraphController;
        
        bool overwrite = inputManager != null && inputManager.IsModifierKeyHeld(overwriteQueueModifier);
        bool interrupt = controller.IsInRoutine || overwrite;
        bool clearDeque = overwrite;
        if (interrupt)
        {
            bool didInterrupt = controller.TryInterrupt(
                factory,
                saveThisGraphIfItGetsInterrupted: false,
                clearDequeOnSuccess: clearDeque
            );
            if (didInterrupt)
            {
                // If we interrupted, we swap the controller to use player control
                controller.IdleOnExit = true;  // Tells it not to resume routine on state exit
            }
        }
        else
        {
            controller.EnqueueStateGraph(
                factory,
                interruptCurrent: false,
                saveThisGraphIfItGetsInterrupted: false,
                clearDeque: clearDeque
            );
        }
    }

    /// <summary>
    /// When the mouse hovers over a point in the world, this method can be used to handle any hover logic.
    /// </summary>
    /// <param name="worldHoverPosition"></param>
    public void HandleMovementPositionTargetHover(Vector3 worldHoverPosition)
    {
        // Check whether we can navigate to the hovered position.
        // mouseHoverMovementDestination both provides a visual indication of where you are navigating to and
        // allows us to be sure that the destination is reachable before allowing the player to try to navigate there.
        if (currentFocusedNpc != null)
        {
            NpcMovementRequest request = new NpcMovementRequest(worldHoverPosition);
            var (reachable, destination) = currentFocusedNpc.MovementManager.CanSatisfyRequest(request);
            if (reachable)
            {
                // We can reach this position, so set it as the destination
                mouseHoverMovementDestination = destination;
            }
            else
            {
                // We cannot reach this position, so clear the destination
                mouseHoverMovementDestination = null;
            }
        }
    }
    
    /// <summary>
    /// Handles interaction logic when a point in the world is clicked.
    /// Generates a MoveTo step and tells the focused NPC's CharacterModeController to execute it.
    /// </summary>
    public void SetCurrentNpcMoveTarget() // [cite: 1085]
    {
        if (currentFocusedNpc == null)
        {
            Debug.LogWarning("Cannot handle world interaction: No NPC focused.", this);
            return;
        }

        if (mouseHoverMovementDestination.HasValue)
        {
            // Generate the MoveTo graph
            MoveGraphConfiguration config = new MoveGraphConfiguration()
            {
                GraphId = "PlayerMoveToCommandGraph",
                moveToStateConfig = new MoveToStateConfiguration(mouseHoverMovementDestination.Value)
                {
                    DesiredSpeed = defaultMovementSpeed,
                    RequireExactPosition = true
                }
            };
            MoveGraphFactory factory = new MoveGraphFactory(config);
            
            StateGraphController controller = currentFocusedNpc.StateGraphController;
            
            // If the player is holding the key to overwrite the queue, we want to interrupt and clear the current queue
            bool overwrite = inputManager != null && inputManager.IsModifierKeyHeld(overwriteQueueModifier);
            // If the graph is currently running a MoveTo command from the player, we want to overwrite it instead of
            // adding to the queue
            // Also if we are in the routine graph we want to interrupt it
            bool interrupt = controller.IsInRoutine || controller.CurrentStateGraph?.id == config.GraphId || overwrite;
            if (interrupt)
            {
                bool didInterrupt = controller.TryInterrupt(
                    factory,
                    saveThisGraphIfItGetsInterrupted: false,
                    clearDequeOnSuccess: overwrite
                );
                if (didInterrupt)
                {
                    // If we interrupted, we swap the controller to use player control
                    controller.IdleOnExit = true;  // Tells it not to resume routine on state exit
                }
            }
            else
            {
                controller.EnqueueStateGraph(
                    factory,
                    interruptCurrent: false,
                    saveThisGraphIfItGetsInterrupted: false,
                    clearDeque: overwrite
                );
            }
        }
    }

    public void SetHeldItemPlacePosition(Vector3 worldHoverPosition)
    {
        if (itemPlaceHoldable == null)
        {
            Debug.LogError("ItemPlaceHoldable is null. Cannot set item place position.", this);
            return;
        }
        
        // If we are outside of the item place distance, we null the item place position
        if ((currentFocusedNpc.transform.position - worldHoverPosition).sqrMagnitude > itemPlaceDistance * itemPlaceDistance)
        {
            // We are outside of the radius
            itemPlacePosition = null;
            if(!itemPlaceHoldable.SetGhostPosition(Vector3.zero, itemPlaceHoldable.GetDefaultRotation()))
            {
                // This indicates that the item is not in a state where it can be placed. We should exit place mode.
                Debug.LogError("Failed to set item to ghost position. Cannot place item.", this);
                EndItemPlaceMode();
            }
        }
        else
        {
            // We can set the item place position
            itemPlacePosition = worldHoverPosition;
            // itemPlaceHoldable.gameObject.transform.position = itemPlacePosition.Value;
            if(!itemPlaceHoldable.SetGhostPosition(itemPlacePosition.Value, itemPlaceHoldable.GetDefaultRotation()))
            {
                // This indicates that the item is not in a state where it can be placed. We should exit place mode.
                Debug.LogError("Failed to set item to ghost position. Cannot place item.", this);
                EndItemPlaceMode();
            }
        }
    }
    
    public void DropHeldItemAndSetPosition()
    {
        // EndItemPlaceMode will null the itemPlaceHoldable and itemPlacePosition so we need to store them
        Holdable curHoldable = itemPlaceHoldable;
        Vector3? itemPlacePos = itemPlacePosition;
        EndItemPlaceMode();  // We do this to ensure that the item is in a state where we can drop it without causing issues

        if (curHoldable == null)
        {
            Debug.LogError("ItemPlaceHoldable is null. Cannot drop item.", this);
            return;
        }
        else if (!curHoldable.IsHeld)
        {
            // Somehow the item exited the held state? This is an error.
            Debug.LogError("ItemPlaceHoldable is not held. Cannot drop item.", this);
            return;
        }

        if (!itemPlacePos.HasValue)
        {
            // We don't have a valid position to drop the item at
            Debug.LogError("ItemPlacePosition is null. Cannot drop item.", this);
            return;
        }
        
        // Now we can drop the item
        curHoldable.PutDown(
            currentFocusedNpc.gameObject,
            itemPlacePos.Value,
            curHoldable.GetDefaultRotation()
        );
    }

    public void StartItemPlaceMode()
    {
        if (currentFocusedNpc == null)
        {
            // Nothing to do
            Debug.LogWarning("Cannot start item place mode: No NPC focused.", this);
            return;
        }

        Holdable heldItem = currentFocusedNpc.Inventory.GetInventoryData().HeldItem;
        if (heldItem == null)
        {
            // Again, nothing to do
            Debug.LogWarning("Cannot start item place mode: No item held.", this);
            return;
        }

        if (!heldItem.SetVisualState(HoldableVisualState.Ghost))
        {
            Debug.LogError("Failed to set item to ghost state. Cannot start item place mode.", this);
            return;
        }
        
        if (!heldItem.SetGhostPosition(Vector3.zero, heldItem.GetDefaultRotation()))
        {
            // Position set immediately failed swap back to in hand and don't enter place mode
            heldItem.SetVisualState(HoldableVisualState.InHand, heldItem.CurrentGripPoint);
            Debug.LogError("Failed to set item to ghost position. Cannot start item place mode.", this);
            return;
        }
        
        itemPlaceHoldable = heldItem;
        itemPlacePosition = null; // Reset the position
        currentSelectionMode = PlayerManagerSelectionMode.PlaceItem;
    }
    
    public void EndItemPlaceMode()
    {
        if (currentSelectionMode != PlayerManagerSelectionMode.PlaceItem)
        {
            // That was easy. We aren't in item place mode to begin with.
            return;
        }
        
        if (itemPlaceHoldable == null)
        {
            // We lost the reference to the item. This is an error.
            Debug.LogError("ItemPlaceHoldable is null. Cannot end item place mode.", this);
            return;
        }

        if (itemPlaceHoldable.CurrentVisualState == HoldableVisualState.Ghost)
        {
            // Then we need to manually exit the ghost state in the holdable
            itemPlaceHoldable.SetVisualState(HoldableVisualState.InHand, itemPlaceHoldable.CurrentGripPoint);
        }

        currentSelectionMode = PlayerManagerSelectionMode.TargetMove;
        itemPlaceHoldable = null;
        itemPlacePosition = null;
    }
    
    /// <summary>
    /// Handles a command (via key press) for the focused NPC to return to its default routine.
    /// Tells the CharacterModeController to switch back to Routine mode.
    /// </summary>
    private void HandleReturnToRoutineCommand() // [cite: 1090]
    {
        // Inform the StateGraphController that it should revert to routine control when the state queue is empty
        currentFocusedNpc.StateGraphController.IdleOnExit = false;
    }
}