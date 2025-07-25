using System;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

class PlayerManagerV2 : MonoBehaviour
{
    public static PlayerManagerV2 Instance;

    [SerializeField] private Camera mainCamera;
    [Tooltip("Layers to consider for world interactions.")]
    [SerializeField] private LayerMask interactionLayers = ~0; // Default to everything
    [Tooltip("Maximum distance for raycasting.")]
    [SerializeField] private float maxRaycastDistance = 100f;
    
    [Tooltip("The prefab to place to show a movement target.")]
    [SerializeField] private GameObject movementTargetPrefab;
    
    
    private NpcContext _controlledNpc = null;
    public NpcContext ControlledNpc => _controlledNpc;
    
    [ShowNativeProperty]
    public bool CanControlNpc => _controlledNpc != null;
    
    private bool _overrideModifierHeld = false;
    private bool _isOverUI = false;

    private GameObject _movementMarkerInstance = null;

    private PlayerManagerSelectionMode _selectionMode = PlayerManagerSelectionMode.TargetMove;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There is more than one instance of PlayerManagerV2!");
            return;
        }

        Instance = this;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        GameManager.InputActions.PlayerControl.OverrideModifier.started += HandleOverrideModifierChanged;
        GameManager.InputActions.PlayerControl.OverrideModifier.canceled += HandleOverrideModifierChanged;

        GameManager.InputActions.PlayerControl.Primary.performed += HandlePrimaryActionPerformed;

        GameManager.InputActions.PlayerControl.PrecisePlace.performed += HandlePrecisePlaceActionPerformed;

        if (movementTargetPrefab != null)
        {
            _movementMarkerInstance = Instantiate(movementTargetPrefab);
        }
        else
        {
            Debug.LogWarning("PlayerManagerV2: movementTargetPrefab is not set. Movement target will not be displayed.");
        }
    }

    private void OnDisable()
    {
        GameManager.InputActions.PlayerControl.OverrideModifier.started -= HandleOverrideModifierChanged;
        GameManager.InputActions.PlayerControl.OverrideModifier.canceled -= HandleOverrideModifierChanged;

        GameManager.InputActions.PlayerControl.Primary.performed -= HandlePrimaryActionPerformed;

        GameManager.InputActions.PlayerControl.PrecisePlace.performed -= HandlePrecisePlaceActionPerformed;
        
        if (_movementMarkerInstance != null)
        {
            Destroy(_movementMarkerInstance);
            _movementMarkerInstance = null;
        }
    }

    private void Update()
    {
        _isOverUI = EventSystem.current.IsPointerOverGameObject();  // Why is this backwards? It is correct, but why?

        if (_controlledNpc != null && _controlledNpc.MovementManager.HasMovementRequest)
        {
            // Then we will update the UI to show the movement target
            Vector3? targetPosition = _controlledNpc.MovementManager.CurrentTargetPosition;
            if (_movementMarkerInstance == null)
            {
                Debug.LogWarning("PlayerManagerV2: movementTargetPrefab is not set. Movement target will not be displayed.");
            }
            else if (!targetPosition.HasValue)
            {
                Debug.LogWarning("PlayerManagerV2: CurrentMovementRequest does not have a target position. Movement marker will not be updated.");
            }
            else
            {
                // Update the movement marker position
                _movementMarkerInstance.transform.position = targetPosition.Value;
                _movementMarkerInstance.SetActive(true);
            }
        }
        else
        {
            // Hide the movement marker if there is no movement request
            if (_movementMarkerInstance != null)
            {
                _movementMarkerInstance.SetActive(false);
            }
        }
    }

    private void HandlePrecisePlaceActionPerformed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void HandlePrimaryActionPerformed(InputAction.CallbackContext context)
    {
        if (_isOverUI)
        {
            // Then we currently do nothing
            Debug.Log("PlayerManagerV2: Primary action performed over UI.");
        }
        else
        {
            Debug.Log("PlayerManagerV2: Primary action performed over world.");
            // Then this is a world interaction
            Vector2 mousePosition = Input.mousePosition;
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance, interactionLayers))
            {
                Vector3 worldPosition = hitInfo.point;
                HandleWorldInteraction(worldPosition);
            }
            {
                // There was no world interaction
            }
        }
    }

    private void HandleOverrideModifierChanged(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _overrideModifierHeld = true;
        }
        else if (context.canceled)
        {
            _overrideModifierHeld = false;
        }
        else
        {
            Debug.LogWarning($"PlayerManager: Unexpected context state for OverrideModifier: {context}");
        }
    }

    public void SetControlledNpc(NpcContext npc)
    {
        Debug.Log($"PlayerManager: SetControlledNpc to {npc}");
        _controlledNpc = npc;
        OnControlledNpcChange?.Invoke(npc);
    }
    public event Action<NpcContext> OnControlledNpcChange;


    /// <summary>
    /// Handles interaction logic when a PlayerControlTrigger is clicked.
    /// Tells the focused NPC's CharacterModeController to switch to Player mode and execute the trigger's step.
    /// </summary>
    public void HandleTriggerInteraction(PlayerControlTrigger clickedTrigger)
    {
        if (_controlledNpc == null)
        {
            Debug.LogWarning("Cannot handle trigger interaction: No NPC focused.", this);
            return;
        }
        if (clickedTrigger == null) return;
    
        // Check if the specific action is actually possible right now
        InteractionStatus status = clickedTrigger.GetActionStatus(_controlledNpc.gameObject);
        if (!status.CanInteract(true))
        {
            Debug.LogWarning($"Cannot interact with trigger '{clickedTrigger.gameObject.name}': Action not allowed for {_controlledNpc.gameObject.name}. Reasons: [{string.Join(", ", status.FailureReasons)}]", clickedTrigger);
            // Optionally provide UI feedback here
            return;
        }
        
        InterruptBehaviorDefinition interruptDefinition = clickedTrigger.GetBehaviorInterruptDefinition(_controlledNpc.gameObject);
        if (interruptDefinition == null)
        {
            Debug.LogError($"Trigger '{clickedTrigger.gameObject.name}' failed to generate a valid interrupt definition.", clickedTrigger);
            return;
        }
        
        BehaviorController controller = _controlledNpc.BehaviorController;
        if (_overrideModifierHeld)
        {
            controller.TryInterrupt(interruptDefinition, clearQueue: true);
        }
        else
        {
            controller.EnqueueInterrupt(interruptDefinition);
        }
    }

    public void HandleWorldInteraction(Vector3 worldPosition)
    {
        Debug.Log($"PlayerManager: World interaction {worldPosition} with override modifier held: {_overrideModifierHeld}");

        if (_selectionMode == PlayerManagerSelectionMode.TargetMove)
        {
            HandleMoveInteraction(worldPosition);
        }
        else if (_selectionMode == PlayerManagerSelectionMode.PlaceItem)
        {
            HandlePlaceInteraction(worldPosition);
        }
        else
        {
            Debug.LogError($"PlayerManager: Unhandled selection mode: {_selectionMode}");
        }
    }

    private void HandleMoveInteraction(Vector3 worldPosition)
    {
        Debug.Log($"PlayerManager: MoveInteraction {worldPosition}");
        NpcMovementRequest request = new NpcMovementRequest(worldPosition);
        var (reachable, destination) = _controlledNpc.MovementManager.CanSatisfyRequest(request);

        if (!reachable)
        {
            Debug.LogWarning($"PlayerManager: Cannot move to {worldPosition} - destination is unreachable.");
            return;
        }
        
        if (GlobalData.Instance?.defaultAggInterruptBehaviorFactory == null)
        {
            Debug.LogError("PlayerManager: InterruptBehaviorFactory is not set. Cannot interrupt behavior.", this);
            return;
        }
        AggInterruptBehaviorFactory factory = GlobalData.Instance.defaultAggInterruptBehaviorFactory;
            
        InterruptBehaviorDefinition moveInterrupt = factory.MoveToBehaviorFactory.GetInterruptDefinition(
            new MoveToBehaviorParameters()
            {
                targetPosition = worldPosition,
                AgentId = "PlayerMoveToCommand",
                desiredSpeed = MovementSpeed.NpcSpeed,
                exactPosition = true,
                finalAlignment = false,
                Priority = 0f,
                SaveContext = false,
            }
        );

        if (moveInterrupt == null)
        {
            Debug.LogError("Failed to create MoveTo interrupt behavior. Cannot set move target.", this);
            return;
        }
        
        if (_overrideModifierHeld)
        {
            _controlledNpc.BehaviorController.TryInterrupt(moveInterrupt, clearQueue: true);
        }
        else
        {
            _controlledNpc.BehaviorController.EnqueueInterrupt(moveInterrupt);
        }
    }

    private void HandlePlaceInteraction(Vector3 worldPosition)
    {
        throw new NotImplementedException("PlaceItem mode is not implemented yet.");
    }
}