using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Action

public enum HoldableVisualState
{
    InWorld,
    InHand,
    InInventory,
    Ghost
}

public class HoldableSaveableData : InteractableSaveableData
{
    // We add data about the transform here.
    // We do not need to store the holder or the visual state as those get set when the NPC that is holding the item is loaded.
    // Instead, we just put everything into the world
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

/// <summary>
/// Represents an instance of a holdable item in the scene.
/// Manages its state (held, in inventory, on ground) and interaction consequences.
/// Inherits from Interactable and enables/disables its PickUp/PutDown InteractionInstances based on state.
/// </summary>
public class Holdable : Interactable
{
    [Header("Holdable Configuration")]

    [Tooltip("The interaction definition used to initiate picking up this item.")]
    [SerializeField] private InteractionDefinitionSO pickUpDefinition;

    [Tooltip("The interaction definition used to initiate putting down this item.")]
    [SerializeField] private InteractionDefinitionSO putDownDefinition;

    [Tooltip("Assign the child Transform that represents the point and orientation to align with the holder's hand.")]
    [SerializeField] private Transform gripPoint;

    [Header("Inventory Configuration")]
    [Tooltip("The sprite to use when displaying in the inventory.")]
    [SerializeField] private Sprite inventorySprite;
    public Sprite InventorySprite => inventorySprite;
    
    [Header("Initial State")]
    [Tooltip("Optional: Assign an NPC GameObject here if this item should start held by them.")]
    [SerializeField] private GameObject initialHolder;
    
    public override SaveableData GetSaveData()
    {
        SaveableData baseData = base.GetSaveData();
        // The base class returns a InteractableSaveableData. We need to copy the values into a HoldableSaveableData.
        InteractableSaveableData interactableData = baseData as InteractableSaveableData;

        HoldableSaveableData holdableData = new HoldableSaveableData();
        holdableData.InteractionInstancesData = interactableData.InteractionInstancesData;
        
        if (holdableData == null)
        {
            throw new InvalidCastException($"Base save data for Holdable '{gameObject.name}' is not of type HoldableSaveableData. Ensure GetSaveData() is overridden correctly.");
        }
        
        holdableData.Position = transform.position;
        holdableData.Rotation = transform.rotation;
        holdableData.Scale = transform.localScale;
        
        return holdableData;
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        base.LoadSaveData(data, blankLoad);  // Sets the base properties from InteractableSaveableData

        if (!blankLoad)
        {
            if (data is not HoldableSaveableData holdableData)
            {
                throw new InvalidCastException($"Expected HoldableSaveableData for loading, but got {data.GetType().Name} for Holdable '{gameObject.name}'.");
            }
        
            // Now set the specific Holdable properties
            transform.position = holdableData.Position;
            transform.rotation = holdableData.Rotation;
            transform.localScale = holdableData.Scale;
            // And put into the world. The actual state will be set when NPCs are loaded.
            SetVisualState(HoldableVisualState.InWorld);
        }
        else
        {
            // If we are doing a blank load, we also need to set our state based on the initial conditions
            // --- Handle Initial Holder ---
            bool startedHeld = false;
            if (initialHolder != null)
            {
                NpcContext holderContext = initialHolder.GetComponent<NpcContext>();
                if (holderContext != null && holderContext.Inventory != null)
                {
                    InteractionContext initialPickupContext = new InteractionContext(initialHolder, this, pickUpDefinition);
                    HandlePickUp(initialPickupContext);
                    if(CurrentHolder == initialHolder)
                    {
                        startedHeld = true; // Successfully started held
                    }
                    else
                    {
                        Debug.LogError($"Holdable '{gameObject.name}' failed to initialize as held by '{initialHolder.name}'.", this);
                    }
                } else {
                    Debug.LogError($"Holdable '{gameObject.name}' has InitialHolder '{initialHolder.name}' assigned, but it lacks NpcContext or NpcInventory.", initialHolder);
                    initialHolder = null;
                }
            }

            // --- Set Initial Interaction Enabled States ---
            // Must be done *after* attempting initial hold
            if (pickUpDefinition != null)
            {
                // Can only pick up if NOT currently held (or in inventory, though HandlePickUp sets IsHeld=true)
                SetInteractionEnableInfo(pickUpDefinition, !startedHeld, true, "Item is already held or in inventory.");
            }
            if (putDownDefinition != null)
            {
                // Can only put down IF currently held
                SetInteractionEnableInfo(putDownDefinition, startedHeld, true, "Item is not currently held.");
            }

            // Ensure visual state is correct if not initially held
            if (!startedHeld)
            {
                SetVisualState(HoldableVisualState.InWorld);
            }
        }
    }

    // --- State Properties ---
    /// <summary>Gets whether this item is currently attached to a holder's hand.</summary>
    public bool IsHeld => GetIsHeld(); // { get; private set; }
    private bool GetIsHeld()
    {
        if (CurrentHolder == null) return false;
        
        if (!CurrentHolder.TryGetComponent<NpcContext>(out NpcContext npcContext)) return false;
        
        if (npcContext.Inventory == null) return false;

        return npcContext.Inventory.HasItemInHand(this);
    }

    /// <summary>Gets whether this item is currently stored in a holder's inventory slots (and thus visually inactive).</summary>
    public bool IsInInventory => GetIsInInventory(); // { get; private set; }
    private bool GetIsInInventory()
    {
        if (CurrentHolder == null) return false;
        
        if (!CurrentHolder.TryGetComponent<NpcContext>(out NpcContext npcContext)) return false;
        
        if (npcContext.Inventory == null) return false;

        return npcContext.Inventory.HasItemInInventory(this);
    }
    /// <summary>Gets the GameObject currently holding this item (either in hand or inventory).</summary>
    public GameObject CurrentHolder { get; private set; }

    // --- Events ---
    /// <summary>Fired when this object is successfully picked up. Passes the GameObject of the holder.</summary>
    public event Action<GameObject> OnPickedUp;
    /// <summary>Fired when this object is successfully dropped/put down.</summary>
    public event Action OnDropped;

    // --- Internal References ---
    private HoldableVisualState _currentVisualState;
    public HoldableVisualState CurrentVisualState => _currentVisualState;
    
    private Transform _currentGripPoint;
    public Transform CurrentGripPoint => _currentGripPoint;
    
    private InteractionInstance _pickUpInstance;
    private InteractionInstance _putDownInstance;
    private List<Collider> _colliders = new();
    private Rigidbody _rigidbody;


    /// <summary>
    /// Initializes state, ensures InteractionInstances exist, caches components,
    /// and sets up event listeners automatically.
    /// </summary>
    protected virtual void Awake()
    {
        // IsHeld = false;
        // IsInInventory = false;
        CurrentHolder = null;

        // Cache components
        _colliders = GetComponentsInChildren<Collider>().ToList();
        _rigidbody = GetComponent<Rigidbody>();

        if (_colliders.Count == 0) { Debug.LogWarning($"Holdable '{gameObject.name}' is missing a Collider component.", this); }
        if (gripPoint == null) { Debug.LogError($"Holdable '{gameObject.name}' is missing its Grip Point assignment. Item attachment will likely be incorrect.", this); }

        // --- Ensure Interaction Instances Exist and Link Events ---
        // PickUp Instance
        _pickUpInstance = FindInteractionInstance(pickUpDefinition);
        if (_pickUpInstance == null && pickUpDefinition != null)
        {
            _pickUpInstance = new InteractionInstance { InteractionDefinition = pickUpDefinition };
            // Assume InteractionInstance initializes IsEnabled = true by default if that field exists
            AddInteractionInstance(_pickUpInstance);
        }
        if (_pickUpInstance != null)
        {
            _pickUpInstance.OnInteractionEnd.RemoveListener(HandlePickUp);
            _pickUpInstance.OnInteractionEnd.AddListener(HandlePickUp);
        }
        else if (pickUpDefinition != null)
        {
             Debug.LogError($"Holdable '{gameObject.name}' failed to find or add InteractionInstance for PickUpDefinition '{pickUpDefinition.name}'.", this);
        }

        // PutDown Instance
        _putDownInstance = FindInteractionInstance(putDownDefinition);
        if (_putDownInstance == null && putDownDefinition != null)
        {
             _putDownInstance = new InteractionInstance { InteractionDefinition = putDownDefinition };
             // Assume InteractionInstance initializes IsEnabled = true by default
             AddInteractionInstance(_putDownInstance);
        }
        if (_putDownInstance != null)
        {
            _putDownInstance.OnInteractionEnd.RemoveListener(HandlePutDown);
            _putDownInstance.OnInteractionEnd.AddListener(HandlePutDown);
        }
        else if (putDownDefinition != null)
        {
              Debug.LogError($"Holdable '{gameObject.name}' failed to find or add InteractionInstance for PutDownDefinition '{putDownDefinition.name}'.", this);
        }
    }

    /// <summary>
    /// Handles the logic when the 'Pick Up' interaction successfully completes.
    /// Attempts to acquire the item via the initiator's Inventory component and updates interaction enables.
    /// </summary>
    public virtual void HandlePickUp(InteractionContext context)
    {
        if (context.Initiator == null) return;

        NpcContext npcContext = context.Initiator.GetComponent<NpcContext>();
        if (npcContext == null || npcContext.Inventory == null) return;

        NpcInventory inventory = npcContext.Inventory;
        (bool success, bool wasHeld, Transform handAttachPoint) acquireResult = inventory.TryAcquireItem(this, context.Initiator);

        if (acquireResult.success)
        {
            // Update internal state FIRST
            // IsHeld = acquireResult.wasHeld;
            // IsInInventory = !acquireResult.wasHeld;
            CurrentHolder = context.Initiator;

            // Apply visual/physics changes
            if (IsHeld)
            {
                SetVisualState(HoldableVisualState.InHand, acquireResult.handAttachPoint);
            }
            else
            {
                SetVisualState(HoldableVisualState.InInventory);
            }

            // Update interaction availability: Cannot pick up again, can now put down (if held)
            if(pickUpDefinition != null)
            {
                SetInteractionEnableInfo(pickUpDefinition, false, true, "Item is already held or in inventory.");
            }
            if(putDownDefinition != null)
            {
                SetInteractionEnableInfo(putDownDefinition, true, true, "Item is not currently held.");
            }

            // Fire the pickup event AFTER state is fully updated
            try { OnPickedUp?.Invoke(CurrentHolder); }
            catch (Exception e) { Debug.LogError($"Error in OnPickedUp event handler for {gameObject.name}: {e.Message}\n{e.StackTrace}", this); }
        }
    }

    public void HandlePutDown(InteractionContext context)
    {
        PutDown(context.Initiator);
    }
    
    /// <summary>
    /// Handles the logic when the 'Put Down' interaction successfully completes.
    /// Releases the item from the initiator's Inventory component and updates interaction enables.
    /// </summary>
    public virtual bool PutDown(GameObject initiator, Vector3? placePosition = null, Quaternion? placeRotation = null)
    {
        if (initiator == null || CurrentHolder != initiator) return false;

        NpcContext npcContext = initiator.GetComponent<NpcContext>();
        if (npcContext == null || npcContext.Inventory == null) return false;

        NpcInventory inventory = npcContext.Inventory;
        Holdable releasedItem = inventory.ReleaseHeldItem();

        if (releasedItem == this)
        {
            // Clear internal state FIRST
            // IsHeld = false;
            // IsInInventory = false;
            CurrentHolder = null;

            // Apply visual/physics changes
            SetVisualState(HoldableVisualState.InWorld);

            // Update interaction availability: Can pick up again, cannot put down
            if(pickUpDefinition != null)
            {
                SetInteractionEnableInfo(pickUpDefinition, true, true, "Item is already held or in inventory.");
            }
            if(putDownDefinition != null)
            {
                SetInteractionEnableInfo(putDownDefinition, false, true, "Item is not currently held.");
            }

            // Fire the dropped event AFTER state is fully updated
            try
            {
                OnDropped?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in OnDropped event handler for {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
            }
        }
        else
        {
            Debug.LogError($"'{initiator.name}' tried to put down '{gameObject.name}', but Inventory.ReleaseHeldItem() returned '{releasedItem?.name ?? "null"}'. State mismatch!", this);
            // Attempt recovery: force state and interaction enables
            // IsHeld = false;
            // IsInInventory = false;
            CurrentHolder = null;
            SetVisualState(HoldableVisualState.InWorld);
            if(pickUpDefinition != null)
            {
                SetInteractionEnableInfo(pickUpDefinition, true, true, "Item is already held or in inventory.");
            }
            if(putDownDefinition != null)
            {
                SetInteractionEnableInfo(putDownDefinition, false, true, "Item is not currently held.");
            }
        }
        
        if (placePosition != null && placeRotation != null)
        {
            transform.position = placePosition.Value;
            transform.rotation = placeRotation.Value;
        }
        // else: SetVisualState(HoldableVisualState.InWorld) drops from the current position

        return true;
    }

    public bool SetGhostPosition(Vector3 position, Quaternion rotation)
    {
        if (_currentVisualState != HoldableVisualState.Ghost)
        {
            return false;
        }
        transform.position = position;
        transform.rotation = rotation;
        return true;
    }
    
    /// <summary>
    /// Manages the visual state (attachment, visibility) and physics/collider state based on whether the item is held, in inventory, or on the ground.
    /// Uses the 'gripPoint' child transform for alignment when held.
    /// </summary>
    public virtual bool SetVisualState(HoldableVisualState newVisualState, Transform attachParent = null)
    {
        if (newVisualState == HoldableVisualState.InHand)
        {
            if (attachParent == null)
            {
                Debug.LogError($"Cannot set '{gameObject.name}' to InHand: Attach Parent is null!", this);
                return false;
            }
            AlignGripPointWithParent(attachParent);
            transform.SetParent(attachParent, true);
            _currentGripPoint = attachParent;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            gameObject.SetActive(true);
            SetColliderState(false);
        }
        else if (newVisualState == HoldableVisualState.InInventory)
        {
            transform.SetParent(null);
            transform.position = Vector3.zero;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
            gameObject.SetActive(false);
            SetColliderState(false);
        }
        else if (newVisualState == HoldableVisualState.InWorld)
        {
            transform.SetParent(null);
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
            gameObject.SetActive(true);
            SetColliderState(true);
        }
        else if (newVisualState == HoldableVisualState.Ghost)
        {
            transform.SetParent(null);
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            gameObject.SetActive(true);
            SetColliderState(false);
        }
        else
        {
            Debug.LogError($"Invalid visual state '{newVisualState}' for '{gameObject.name}'.", this);
            return false;
        }
        _currentVisualState = newVisualState;
        return true;
    }
    
    public void SetColliderState(bool isEnabled)
    {
        if (_colliders.Count > 0)
        {
            foreach (Collider childCollider in _colliders)
            {
                childCollider.enabled = isEnabled;
            }
        }
    }
    
    /// <summary>
    /// Calculates and applies the position and rotation to the root transform
    /// such that the 'gripPoint' child aligns with the given 'attachParent'.
    /// </summary>
    /// <param name="attachParent">The parent transform to align with.</param>
    private void AlignGripPointWithParent(Transform attachParent = null)
    {
        if (gripPoint == null)
        {
            Debug.LogError($"Cannot align '{gameObject.name}': Grip Point transform is not assigned!", this);
            return;
        }
        
        if (attachParent == null)
        {
            Debug.LogError($"Cannot align '{gameObject.name}': Attach Parent transform is null!", this);
            return;
        }
        
        Quaternion targetItemWorldRotation = attachParent.rotation * Quaternion.Inverse(gripPoint.localRotation);
        
        Vector3 localOffset = gripPoint.localPosition;
        Vector3 scaledLocalOffset = Vector3.Scale(localOffset, transform.localScale);
        Vector3 worldOffset = targetItemWorldRotation * scaledLocalOffset;
        Vector3 targetItemWorldPosition = attachParent.position - worldOffset;
        
        transform.rotation = targetItemWorldRotation;
        transform.position = targetItemWorldPosition;
    }

    public Quaternion GetDefaultRotation()
    {
        return Quaternion.identity;
    }

    /// <summary>
    /// Gets the definition ScriptableObject associated with this holdable item.
    /// </summary>
    public HoldableDefinitionSO GetItemDefinition()
    {
        return InteractableDefinition as HoldableDefinitionSO;
    }

    /// <summary>
    /// Gets the InteractionDefinitionSO used for picking up this item.
    /// </summary>
    /// <returns>The pick up definition, or null if not assigned.</returns>
    public InteractionDefinitionSO GetPickUpDefinition()
    {
        // Provides controlled access to the pickup definition for external systems like editors.
        return pickUpDefinition;
    }

    /// <summary>
    /// Gets the InteractionDefinitionSO used for putting down this item.
    /// </summary>
    /// <returns>The put down definition, or null if not assigned.</returns>
    public InteractionDefinitionSO GetPutDownDefinition()
    {
        // Provides controlled access to the putdown definition for external systems like editors.
        return putDownDefinition;
    }
}