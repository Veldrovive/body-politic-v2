using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages the player's current focus (NPC), handles input for focus switching
/// and interactions, relays trigger events, and sends commands to the focused NPC's CharacterModeController.
/// </summary>
[DefaultExecutionOrder(-85)]
public class PlayerManager : MonoBehaviour
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

    // --- Keybinds ---
    [Header("Keybinds")]
    [Tooltip("Key to press to force the focused NPC back into Routine mode.")]
    [SerializeField] private KeyCode returnToRoutineKey = KeyCode.R;
    [Tooltip("Key to press to cycle focus to the next controllable NPC.")]
    [SerializeField] private KeyCode cycleFocusKey = KeyCode.N;
     [Tooltip("Key to hold to overwrite the player controller queue instead of appending.")]
    [SerializeField] private KeyCode overwriteQueueModifier = KeyCode.LeftShift;


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
    private NpcContext currentFocusedNpc;
    public NpcContext CurrentFocusedNpc => currentFocusedNpc;
    
    private GameObject movementMarkerInstance;
    /// <summary>
    /// Stores the result of the MovementManager CanSatisfyRequest call.
    /// If there is no path, this is set to null. If there is a path, this is set to the true destination.
    /// Clicking on the ground will try to navigate to this position, not the mouse position.
    /// </summary>
    private Vector3? mouseHoverMovementDestination = null;

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

    /// <summary> Subscribes to events, invokes initial focus event, performs first proximity update. </summary>
    void Start() // [cite: 1014]
    {
        InitializeFocus();
        
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

    private void Update()
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
                    Cursor.visible = false;
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
                    Cursor.visible = true;
                }
            }
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
        
        if (clickState.OverUI) return;
        
        // Try to get NPC component
        NpcContext clickedNpc = clickState.ClickedObject?.GetComponent<NpcContext>();
        // Try to get Trigger component
        // TODO: XXX Uncomment when interactions are re-implemented
        // PlayerControlTrigger clickedTrigger = clickState.ClickedObject?.GetComponent<PlayerControlTrigger>();
        
        if (clickedNpc != null && controllableNpcs.Contains(clickedNpc))
        {
            // Clicked on a controllable NPC -> Set focus
            SetFocus(clickedNpc); // [cite: 1063]
        }
        // TODO: XXX Uncomment when interactions are re-implemented
        // else if (clickedTrigger != null)
        // {
        //     // Clicked on a trigger -> Handle trigger interaction
        //     HandleTriggerInteraction(clickedTrigger);
        // }
        else
        {
            // Clicked on the world -> Handle world interaction
            HandleWorldInteraction(clickState.WorldPosition); // [cite: 1064]
        }
    }

    void HandleHover(InputManager.HoverState hoverState)
    {
        if (hoverState.HasHit)
        {
            // Then we are hovering over something that is not UI
            // TODO: Consider checking whether the hit GameObject has a NavMeshSurface component
            HandleWorldHover(hoverState.WorldPosition);
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
        if (keyState.Key == cycleFocusKey) // Use configured key [cite: 1066]
        {
            CycleCurrentFocusedNpc();
        }
        else if (keyState.Key == returnToRoutineKey) // Use configured key
        {
            HandleReturnToRoutineCommand();
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
         // Logic remains the same
        if (npcToFocus == currentFocusedNpc || npcToFocus == null) return;
        if (!controllableNpcs.Contains(npcToFocus))
        {
             Debug.LogWarning($"Attempted to focus on NPC '{npcToFocus.gameObject.name}' which is not in the controllable list.", this);
             return;
        }

        NpcContext previousFocusedNpc = currentFocusedNpc;
        currentFocusedNpc = npcToFocus;
        // Debug.Log($"Player focus changed to: {currentFocusedNpc.gameObject.name}", this);

        // Update visibility of nearby triggers based on the new NPC's context
        // UpdateActiveTriggersVisibility();

        OnFocusChanged?.Invoke(previousFocusedNpc, currentFocusedNpc);
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
        // Debug.Log($"Added NPC '{npc.gameObject.name}' to controllable list.", this);

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

    //--------------------------------------------------------------------------
    // Interaction Logic (Updated to use CharacterModeController)
    //--------------------------------------------------------------------------

    // TODO: XXX Uncomment when interactions are re-implemented
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
        if (!status.CanInteract)
        {
             Debug.LogWarning($"Cannot interact with trigger '{clickedTrigger.gameObject.name}': Action not allowed for {currentFocusedNpc.gameObject.name}. Reasons: [{string.Join(", ", status.FailureReasons)}]", clickedTrigger);
             // Optionally provide UI feedback here
             return;
        }
    
    
        // // Debug.Log($"Handling Trigger Interaction on {clickedTrigger.gameObject.name} by {currentFocusedNpc.gameObject.name}");
        //
        // // Get the StepDefinition from the trigger
        // StepDefinition triggeredStep = clickedTrigger.GetStepDefinition(currentFocusedNpc); // [cite: 711]
        // if (triggeredStep == null || triggeredStep.Count == 0)
        // {
        //      Debug.LogError($"Trigger '{clickedTrigger.gameObject.name}' failed to generate a valid StepDefinition.", clickedTrigger);
        //      return;
        // }
        //
        //
        // // Get the ModeController from the focused NPC's context
        // CharacterModeController modeController = currentFocusedNpc.ModeController;
        // if (modeController == null)
        // {
        //     Debug.LogError($"Focused NPC '{currentFocusedNpc.gameObject.name}' is missing CharacterModeController.", currentFocusedNpc);
        //     return;
        // }
        //
        // // Determine if overwrite modifier is held
        // bool overwrite = inputManager != null && inputManager.IsModifierKeyHeld(overwriteQueueModifier);
        //
        //
        // // Attempt to switch to player control (or enqueue if already player controlled)
        // modeController.AttemptSwitchToPlayerControl(triggeredStep, overwrite);

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
        controller.EnqueueStateGraph(
            factory,
            interruptCurrent: interrupt,
            saveThisGraphIfItGetsInterrupted: false,
            clearDeque: clearDeque
        );
    }

    /// <summary>
    /// When the mouse hovers over a point in the world, this method can be used to handle any hover logic.
    /// </summary>
    /// <param name="worldHoverPosition"></param>
    public void HandleWorldHover(Vector3 worldHoverPosition)
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
    public void HandleWorldInteraction(Vector3 worldPosition) // [cite: 1085]
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
                    DesiredSpeed = MovementSpeed.Run,
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
            controller.EnqueueStateGraph(
                factory,
                interruptCurrent: interrupt,
                saveThisGraphIfItGetsInterrupted: false,
                clearDeque: overwrite
            );
        }

        // // Get the ModeController
        // CharacterModeController modeController = currentFocusedNpc.ModeController;
        // if (modeController == null)
        // {
        //     Debug.LogError($"Focused NPC '{currentFocusedNpc.gameObject.name}' is missing CharacterModeController.", currentFocusedNpc);
        //     return;
        // }
        //
        // // Debug.Log($"Handling World Interaction at {worldPosition} for {currentFocusedNpc.gameObject.name}");
        //
        // // --- Generate MoveTo Step ---
        // // Create a temporary GameObject to hold the target transform for the MoveTo state.
        // // This avoids polluting the scene hierarchy long-term.
        // // We should destroy this object after the MoveToState is likely finished.
        // // Managing this lifecycle is tricky. A better approach might be for MoveToState
        // // to accept a Vector3 directly, or use a pooled object system.
        // // For now, create and rely on MoveToState potentially handling/ignoring the dummy transform later.
        // if (mouseHoverMovementDestination.HasValue)
        // {
        //     // GameObject dummyGO = new GameObject($"MoveTarget_{currentFocusedNpc.name}_{Time.frameCount}");
        //     // dummyGO.transform.position = mouseHoverMovementDestination.Value;
        //
        //     // Debug.Log($"Creating MoveTo step for {currentFocusedNpc.gameObject.name} at {mouseHoverMovementDestination.Value}");
        //     StepDefinition moveToStep = new StepDefinition(new AbstractCharStateConfiguration[]
        //     {
        //         // new MoveToStateConfiguration_v1(dummyGO.transform, desiredSpeed: MovementSpeed.Run)
        //         new MoveToStateConfiguration(mouseHoverMovementDestination.Value)
        //         {
        //             DesiredSpeed = MovementSpeed.Run,
        //             RequireExactPosition = true
        //         }
        //     });
        //     
        //     // Determine if overwrite modifier is held
        //     bool overwrite = inputManager != null && inputManager.IsModifierKeyHeld(overwriteQueueModifier);
        //
        //     // Attempt to switch/enqueue
        //     modeController.AttemptSwitchToPlayerControl(moveToStep, overwrite);
        // }
        // // --- End Generate MoveTo Step ---
        
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