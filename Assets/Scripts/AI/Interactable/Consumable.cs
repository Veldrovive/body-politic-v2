using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Represents an instance of a consumable item in the scene.
/// Inherits from Holdable and adds a 'Consume' interaction.
/// Can potentially infect the consumer if the 'infected' flag is set.
/// </summary>
public class Consumable : Holdable
{
    [FormerlySerializedAs("consumeDefinition")]
    [Header("Consumable Configuration")]
    [Tooltip("The interaction definition used to initiate consuming this item.")]
    [SerializeField] private InteractionDefinitionSO consumeInteractionDefinition;

    [Tooltip("If true, consuming this item will notify the InfectionManager.")]
    [SerializeField] private bool infected = false;
    public bool Infected => infected;

    // --- Internal References ---
    private InteractionInstance _consumeInstance;

    /// <summary>
    /// Initializes state, ensures InteractionInstances exist (including Consume),
    /// caches components, and sets up event listeners automatically.
    /// </summary>
    protected override void Awake() // Use 'new' to hide the base Awake, as we're adding specific logic
    {
        // Call the base class Awake first to handle Holdable setup
        base.Awake();

        // --- Ensure Consume Interaction Instance Exists and Link Event ---
        _consumeInstance = FindInteractionInstance(consumeInteractionDefinition);
        if (_consumeInstance == null && consumeInteractionDefinition != null)
        {
            _consumeInstance = new InteractionInstance() { InteractionDefinition = consumeInteractionDefinition };
            AddInteractionInstance(_consumeInstance); // Add the instance to the Interactable's list
        }

        if (_consumeInstance != null)
        {
            // Hook into the completion event for the consume action
            _consumeInstance.OnInteractionEnd.RemoveListener(HandleConsume); // Prevent duplicate listeners
            _consumeInstance.OnInteractionEnd.AddListener(HandleConsume);
        }
        else if (consumeInteractionDefinition != null)
        {
             Debug.LogError($"Consumable '{gameObject.name}' failed to find or add InteractionInstance for ConsumeDefinition '{consumeInteractionDefinition.name}'.", this);
        }
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        base.LoadSaveData(data, blankLoad);

        if (blankLoad)
        {
            // --- Set Initial Consume Interaction Enabled State ---
            // Consume action is only possible if the item is currently being held.
            // The base Start() method determines the initial 'IsHeld' state.
            if (consumeInteractionDefinition != null)
            {
                SetInteractionEnableInfo(consumeInteractionDefinition, IsHeld, true, "Item must be held to be consumed.");
            }
        }
    }

    /// <summary>
    /// Handles the logic when the 'Pick Up' interaction successfully completes.
    /// Updates interaction enables, including enabling 'Consume' if it exists.
    /// </summary>
    public override void HandlePickUp(InteractionContext context)
    {
        // Let the base class handle the core pickup logic (state changes, visual updates, base interaction enables)
        base.HandlePickUp(context);

        // After being picked up (IsHeld is now true), enable the Consume interaction if it exists.
        if (consumeInteractionDefinition != null) // Double check IsHeld in case base logic failed
        {
            SetInteractionEnableInfo(consumeInteractionDefinition, true, true, "Item must be held to be consumed.");
        }
    }

    /// <summary>
    /// Handles the logic when the 'Put Down' interaction successfully completes.
    /// Updates interaction enables, including disabling 'Consume'.
    /// </summary>
    public override bool PutDown(GameObject initiator, Vector3? placePosition = null, Quaternion? placeRotation = null)
    {
        // Let the base class handle the core put down logic
        bool wasPutDown = base.PutDown(initiator, placePosition, placeRotation);

        if (wasPutDown)
        {
            // Disable Consume interaction *before* base logic potentially clears CurrentHolder/IsHeld state
            if (consumeInteractionDefinition != null)
            {
                SetInteractionEnableInfo(consumeInteractionDefinition, false, true, "Item must be held to be consumed.");
            }
        }
        
        return wasPutDown;
    }

    /// <summary>
    /// Handles the logic when the 'Consume' interaction successfully completes.
    /// Applies infection if necessary and destroys the consumable item.
    /// </summary>
    public virtual void HandleConsume(InteractionContext context)
    {
        // Basic validation: Ensure the interaction is happening with the correct initiator
        if (context.Initiator == null || CurrentHolder != context.Initiator)
        {
            Debug.LogWarning($"Consumable '{gameObject.name}' Consume interaction completed, but the initiator '{context.Initiator?.name ?? "null"}' is not the CurrentHolder '{CurrentHolder?.name ?? "null"}'. Aborting consume effects.", this);
            return;
        }
        NpcContext npcContext = context.Initiator.GetComponent<NpcContext>();

        // --- Apply Infection ---
        if (infected)
        {
            // Check if the InfectionManager instance exists before trying to use it
            if (InfectionManager.Instance != null)
            {
                // Notify the manager about the potential infection event
                InfectionManager.Instance.NotifyInfection(npcContext);
            }
            else
            {
                Debug.LogError($"Consumable '{gameObject.name}' is infected, but InfectionManager.Instance is null. Cannot notify.", this);
            }
        }
        
        // If the item definition is of the derived class ConsumableDefinitionSO, then it also has a .ConsumedRole
        // that we will add to the dynamic roles of the initiator
        
        if (InteractableDefinition is ConsumableDefinitionSO)
        {
            ConsumableDefinitionSO consumeDefinition = InteractableDefinition as ConsumableDefinitionSO;

            if (consumeDefinition != null && consumeDefinition.ConsumedRole != null)
            {
                npcContext.Identity.AddDynamicRole(consumeDefinition.ConsumedRole);   
            }
        }

        // --- Remove Item ---
        // Typically, consuming an item destroys it.
        // Note: This happens *after* applying effects. Ensure the initiator's inventory is cleared *before* destroying.
        if (npcContext != null && npcContext.Inventory != null)
        {
            // Ensure the item is removed from the inventory *before* destroying the GameObject
            Holdable releasedItem = npcContext.Inventory.ReleaseHeldItem();
            if (releasedItem != this)
            {
                Debug.LogError($"'{context.Initiator.name}' consumed '{gameObject.name}', but Inventory.ReleaseHeldItem() returned '{releasedItem?.name ?? "null"}' during consumption. State mismatch likely.", this);
                // Attempt to proceed with destruction anyway, but log the error.
            }
        }
        else
        {
             Debug.LogWarning($"'{context.Initiator.name}' consumed '{gameObject.name}', but could not find NpcContext/NpcInventory to clear the held item slot.", this);
        }

        // Destroy the consumable GameObject
        Destroy(gameObject);
    }

    public void SetInfected(bool infected)
    {
        // Allows external systems to set the infected state of this consumable.
        this.infected = infected;
    }

#if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Adds validation checks specific to Consumable setup.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        
        // Basic check to ensure the Consume Definition is assigned if this script is used.
        if (consumeInteractionDefinition == null)
        {
            Debug.LogWarning($"Consumable component on '{gameObject.name}' is missing its 'Consume Definition'. The consume interaction will not be available.", this);
        }
    }
#endif

    // --- No need to override SetVisualState unless consuming has a specific visual state ---
    // The base Holdable's SetVisualState handles Held/Not Held/Inventory states.
    // Consumption leads to destruction, so no persistent visual state change is needed here.

    // --- Getters for definitions are inherited from Holdable ---
    // Add a getter for the consume definition if needed by external systems.

    /// <summary>
    /// Gets the InteractionDefinitionSO used for consuming this item.
    /// </summary>
    /// <returns>The consume definition, or null if not assigned.</returns>
    public InteractionDefinitionSO GetConsumeDefinition()
    {
        // Provides controlled access to the consume definition.
        return consumeInteractionDefinition;
    }
}