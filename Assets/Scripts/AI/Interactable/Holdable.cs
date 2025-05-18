using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Action

/// <summary>
/// Represents an instance of a holdable item in the scene.
/// Manages its state (held, in inventory, on ground) and interaction consequences.
/// Inherits from Interactable and enables/disables its PickUp/PutDown InteractionInstances based on state.
/// </summary>
public class Holdable : Interactable
{
    [Header("Holdable Configuration")]
    [Tooltip("Reference to the ScriptableObject defining shared properties for this item type.")]
    [SerializeField] protected HoldableDefinitionSO itemDefinition;

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

    // --- State Properties ---
    /// <summary>Gets whether this item is currently attached to a holder's hand.</summary>
    public bool IsHeld { get; private set; }
    /// <summary>Gets whether this item is currently stored in a holder's inventory slots (and thus visually inactive).</summary>
    public bool IsInInventory { get; private set; }
    /// <summary>Gets the GameObject currently holding this item (either in hand or inventory).</summary>
    public GameObject CurrentHolder { get; private set; }

    // --- Action Availability Getters --- REMOVED
    // public bool CanBePickedUp => !IsHeld && !IsInInventory; // REMOVED
    // public bool CanBePutDown => IsHeld; // REMOVED

    // --- Events ---
    /// <summary>Fired when this object is successfully picked up. Passes the GameObject of the holder.</summary>
    public event Action<GameObject> OnPickedUp;
    /// <summary>Fired when this object is successfully dropped/put down.</summary>
    public event Action OnDropped;

    // --- Internal References ---
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
        IsHeld = false;
        IsInInventory = false;
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
    /// Sets initial enabled state for PickUp/PutDown interactions and handles starting held.
    /// </summary>
    protected virtual void Start()
    {
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
            SetVisualState(false, false, null);
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
            IsHeld = acquireResult.wasHeld;
            IsInInventory = !acquireResult.wasHeld;
            CurrentHolder = context.Initiator;

            // Apply visual/physics changes
            SetVisualState(IsHeld, IsInInventory, acquireResult.handAttachPoint);

            // Update interaction availability: Cannot pick up again, can now put down (if held)
            if(pickUpDefinition != null)
            {
                SetInteractionEnableInfo(pickUpDefinition, false, true, "Item is already held or in inventory.");
            }
            if(putDownDefinition != null)
            {
                SetInteractionEnableInfo(putDownDefinition, IsHeld, true, "Item is not currently held.");
            }

            // Fire the pickup event AFTER state is fully updated
            try { OnPickedUp?.Invoke(CurrentHolder); }
            catch (Exception e) { Debug.LogError($"Error in OnPickedUp event handler for {gameObject.name}: {e.Message}\n{e.StackTrace}", this); }
        }
    }

    /// <summary>
    /// Handles the logic when the 'Put Down' interaction successfully completes.
    /// Releases the item from the initiator's Inventory component and updates interaction enables.
    /// </summary>
    public virtual void HandlePutDown(InteractionContext context)
    {
        if (context.Initiator == null || CurrentHolder != context.Initiator) return;

        NpcContext npcContext = context.Initiator.GetComponent<NpcContext>();
        if (npcContext == null || npcContext.Inventory == null) return;

        NpcInventory inventory = npcContext.Inventory;
        Holdable releasedItem = inventory.ReleaseHeldItem();

        if (releasedItem == this)
        {
            // Clear internal state FIRST
            IsHeld = false;
            IsInInventory = false;
            CurrentHolder = null;

            // Apply visual/physics changes
            SetVisualState(false, false, null);

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
            Debug.LogError($"'{context.Initiator.name}' tried to put down '{gameObject.name}', but Inventory.ReleaseHeldItem() returned '{releasedItem?.name ?? "null"}'. State mismatch!", this);
            // Attempt recovery: force state and interaction enables
            IsHeld = false;
            IsInInventory = false;
            CurrentHolder = null;
            SetVisualState(false, false, null);
            if(pickUpDefinition != null)
            {
                SetInteractionEnableInfo(pickUpDefinition, true, true, "Item is already held or in inventory.");
            }
            if(putDownDefinition != null)
            {
                SetInteractionEnableInfo(putDownDefinition, false, true, "Item is not currently held.");
            }
        }
    }


    /// <summary>
    /// Manages the visual state (attachment, visibility) and physics/collider state based on whether the item is held, in inventory, or on the ground.
    /// Uses the 'gripPoint' child transform for alignment when held.
    /// </summary>
    public virtual void SetVisualState(bool isHeld, bool isInInventory, Transform attachParent)
    {
        if (isHeld && attachParent != null)
        {
            AlignGripPointWithParent(attachParent);
            transform.SetParent(attachParent, true);
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
        } else {
            if(transform.parent != null) transform.SetParent(null);
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
        }
        gameObject.SetActive(!isInInventory);
        if (_colliders.Count > 0)
        {
            foreach (Collider childCollider in _colliders)
            {
                childCollider.enabled = !isHeld && !isInInventory;
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


    /// <summary>
    /// Gets the definition ScriptableObject associated with this holdable item.
    /// </summary>
    public HoldableDefinitionSO GetItemDefinition()
    {
        return itemDefinition;
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