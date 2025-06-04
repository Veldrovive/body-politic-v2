using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq; // Used for FirstOrDefault

/// <summary>
/// Context data passed with interaction events.
/// </summary>
public struct InteractionContext
{
    public GameObject Initiator { get; }
    public Interactable InteractableComponent { get; }
    public InteractionDefinitionSO InteractionDefinition { get; }

    public InteractionContext(GameObject initiator, Interactable interactableComponent, InteractionDefinitionSO interactionDefinition)
    {
        Initiator = initiator;
        InteractableComponent = interactableComponent;
        InteractionDefinition = interactionDefinition;
    }
}

/// <summary>
/// Represents a specific instance of an interaction type configured on an Interactable object.
/// Holds the definition and associated event hooks.
/// </summary>
[System.Serializable]
public class InteractionInstance
{
    [Tooltip("Designer notes for this specific interaction instance.")]
    [TextArea(3, 10)]
    public string DesignerNotes;
    [Tooltip("The definition asset describing the interaction type, requirements, and effects.")]
    public InteractionDefinitionSO InteractionDefinition;

    public bool IsEnabled = true;
    public bool DisabledImpliesHidden = false; // If true, the interaction is hidden when disabled
    public string DisabledReason = ""; // Reason for being disabled (for UI tooltips)

    // --- Events ---
    [Tooltip("Unity Event to raise when interaction begins successfully.")]
    public UnityEvent<InteractionContext> OnInteractionStart;
    [Tooltip("Unity Event to raise when interaction ends successfully (completes duration).")]
    public UnityEvent<InteractionContext> OnInteractionEnd;
    [Tooltip("Unity Event to raise when interaction is interrupted before completion.")]
    public UnityEvent<InteractionContext> OnInteractionInterrupted;
    [Tooltip("Unity Event to raise when the initiator has exited the interaction state.")]
    public UnityEvent<InteractionContext> OnInteractionStateExited;
    
    public InteractionInstance()
    {
        IsEnabled = true;
        DisabledImpliesHidden = false;
        DisabledReason = "";
        OnInteractionStart = new UnityEvent<InteractionContext>();
        OnInteractionEnd = new UnityEvent<InteractionContext>();
        OnInteractionInterrupted = new UnityEvent<InteractionContext>();
        OnInteractionStateExited = new UnityEvent<InteractionContext>();
    }
}

public class InteractableSaveableData : SaveableData
{
    public class InteracttionInstanceSaveableData
    {
        public InteractionDefinitionSO InteractionDefinition;
        public bool IsEnabled = true;
        public bool DisabledImpliesHidden = false; // If true, the interaction is hidden when disabled
        public string DisabledReason = ""; // Reason for being disabled (for UI tooltips)
    }
    
    public List<InteracttionInstanceSaveableData> InteractionInstancesData = new List<InteracttionInstanceSaveableData>();
}


/// <summary>
/// Base class for objects that NPCs can interact with.
/// Manages a list of possible interaction instances (defined by InteractionDefinitionSO).
/// Handles the lifecycle events for interactions performed on it.
/// </summary>
public class Interactable : SaveableMonoBehaviour
{
    [Tooltip("List of interactions available on this object.")]
    [SerializeField] protected List<InteractionInstance> interactionInstances = new List<InteractionInstance>();

    /// <summary>
    /// Public read-only access to the interaction instances configured on this Interactable.
    /// Useful for editor scripts or other systems needing to inspect available interactions.
    /// </summary>
    public IReadOnlyList<InteractionInstance> InteractionInstances => interactionInstances;

    public override SaveableData GetSaveData()
    {
        return new InteractableSaveableData()
        {
            InteractionInstancesData = interactionInstances.Select(inst =>
                new InteractableSaveableData.InteracttionInstanceSaveableData
                {
                    InteractionDefinition = inst.InteractionDefinition,
                    IsEnabled = inst.IsEnabled,
                    DisabledImpliesHidden = inst.DisabledImpliesHidden,
                    DisabledReason = inst.DisabledReason
                }).ToList()
        };
    }

    public override void LoadSaveData(SaveableData data)
    {
        if (data is not InteractableSaveableData interactableData)
        {
            Debug.LogError($"Invalid save data type for {gameObject.name}. Expected InteractableSaveableData.");
            return;
        }
        
        // We demand that the interactionInstances already has the definitions loaded in it.
        // We cannot dynamically load definitions here as they contain function references that cannot be serialized.
        // This does mean that interactions cannot be created dynamically, they must only be enabled and disabled.
        foreach (var interactionInstance in interactableData.InteractionInstancesData)
        {
            SetInteractionEnableInfo(
                interactionInstance.InteractionDefinition,
                interactionInstance.IsEnabled,
                interactionInstance.DisabledImpliesHidden,
                interactionInstance.DisabledReason
            );
        }
    }

    /// <summary>
    /// Finds the InteractionInstance configuration associated with a given InteractionDefinitionSO.
    /// </summary>
    /// <param name="definition">The definition to search for.</param>
    /// <returns>The matching InteractionInstance, or null if not found.</returns>
    public InteractionInstance FindInteractionInstance(InteractionDefinitionSO definition)
    {
        // Helper to locate the specific configuration (events) for a given interaction type.
        return interactionInstances.FirstOrDefault(inst => inst != null && inst.InteractionDefinition == definition);
    }
    
    /// <summary>
    /// Sets the enabled state, visibility, and disabled reason dynamically for a specific interaction instance.
    /// </summary>
    /// <param name="definition">The interaction definition to modify.</param>
    /// <returns>True if the instance was found and updated, false otherwise.</returns>
    public bool SetInteractionEnableInfo(InteractionDefinitionSO definition, bool isEnabled, bool disabledImpliesHidden, string disabledReason)
    {
        // Find the instance based on the definition
        InteractionInstance instance = FindInteractionInstance(definition);
        if (instance != null)
        {
            instance.IsEnabled = isEnabled;
            instance.DisabledImpliesHidden = disabledImpliesHidden;
            instance.DisabledReason = disabledReason;
            return true;
        }
        return false; // Instance not found
    }

    /// <summary>
    /// Adds a new InteractionInstance to the list programmatically.
    /// Ensures the instance and its definition are not null, and prevents duplicates based on the definition.
    /// Initializes UnityEvents if they are null.
    /// </summary>
    /// <param name="instanceToAdd">The InteractionInstance to add.</param>
    protected virtual void AddInteractionInstance(InteractionInstance instanceToAdd)
    {
        if (instanceToAdd == null || instanceToAdd.InteractionDefinition == null)
        {
             Debug.LogError($"Attempted to add a null InteractionInstance or one with a null Definition to {gameObject.name}.", this);
             return;
        }

        // Prevent adding duplicates based on the InteractionDefinitionSO
        if (FindInteractionInstance(instanceToAdd.InteractionDefinition) != null)
        {
             // Debug.LogWarning($"InteractionInstance for '{instanceToAdd.InteractionDefinition.name}' already exists on {gameObject.name}. Not adding duplicate.", this);
             return; // Don't add duplicates
        }

        // Ensure UnityEvent fields are initialized to prevent null reference exceptions
        // This safeguards against issues if instances are created purely in code.
        if (instanceToAdd.OnInteractionStart == null) instanceToAdd.OnInteractionStart = new UnityEvent<InteractionContext>();
        if (instanceToAdd.OnInteractionEnd == null) instanceToAdd.OnInteractionEnd = new UnityEvent<InteractionContext>();
        if (instanceToAdd.OnInteractionInterrupted == null) instanceToAdd.OnInteractionInterrupted = new UnityEvent<InteractionContext>();

        interactionInstances.Add(instanceToAdd);
    }


    // --- Interaction Lifecycle Methods ---

    protected InteractionStatus GetInteractionStatus(InteractionDefinitionSO chosenDefinition, GameObject initiator)
    {
        // --- Basic Input Validation ---
        if (chosenDefinition == null || initiator == null)
        {
             Debug.LogError($"{gameObject.name}: TryInitiateInteraction called with null definition or initiator.", this);
             // Return a default failure status
             var failStatus = new InteractionStatus();
             failStatus.AddFailureReason(new HumanReadableFailureReason(
                InteractionFailureReason.InternalError,
                0,
                "Interaction initiation failed due to null definition or initiator."
            ));
             return failStatus;
        }

        // --- Find Instance ---
        // Note: We find the instance here primarily to access its events (OnInteractionStart, etc.).
        // The enabled check is now handled within GetStatus via GetInteractionEnableInfo.
        InteractionInstance targetInstance = FindInteractionInstance(chosenDefinition);
        if (targetInstance == null) {
            // This InteractionDefinitionSO is not configured on this Interactable's list.
            // It shouldn't be possible to attempt it if it's not listed.
            Debug.LogWarning($"{gameObject.name}: Interaction instance not found for definition {chosenDefinition.name}. Cannot initiate.", this);
            // Return a failure status indicating it's essentially disabled/missing here.
            var failStatus = new InteractionStatus();
            failStatus.AddFailureReason(new HumanReadableFailureReason(
                InteractionFailureReason.InteractionDisabled,
                0,
                $"Interaction instance for '{chosenDefinition.name}' is not configured on this Interactable."
            )); // Treat missing instance as disabled
            failStatus.IsVisible = false; // If instance isn't here, it shouldn't be visible
            return failStatus;
        }

        // --- Check Requirements using InteractionDefinition's GetStatus ---
        // GetStatus now incorporates role, proximity, and the instance's enabled state check.
        InteractionStatus statusResult = chosenDefinition.GetStatus(initiator, this);

        // --- Handle Requirement Check Failure ---
        if (!statusResult.CanInteract())
        {
             #if UNITY_EDITOR || DEVELOPMENT_BUILD
             // Log detailed failure reasons only in relevant builds
             // string reasons = string.Join(", ", statusResult.FailureReasons);
             string reasons = statusResult.GetHumanReadableFailureReasonString();
             Debug.Log($"{gameObject.name}: Initiator {initiator.name} cannot initiate interaction '{chosenDefinition.name}'. Reasons: [{reasons}]. Visible: {statusResult.IsVisible}", this);
             #endif
        }

        return statusResult;
    }
    
    /// <summary>
    /// Called by an initiator (e.g., InteractionState) to attempt starting an interaction.
    /// Finds the interaction instance, checks requirements via InteractionDefinitionSO.GetStatus,
    /// and fires OnInteractionStart events if successful based on the returned status.
    /// </summary>
    /// <param name="chosenDefinition">The specific interaction definition being attempted.</param>
    /// <param name="initiator">The GameObject attempting the interaction.</param>
    /// <returns>An InteractionStatus object detailing the outcome. Check status.CanInteract to see if initiation succeeded.</returns>
    public virtual InteractionStatus TryInitiateInteraction(
        InteractionDefinitionSO chosenDefinition,
        GameObject initiator, int priority = 0)
    {
        InteractionStatus statusResult = GetInteractionStatus(chosenDefinition, initiator);
        if (!statusResult.CanInteract())
        {
            // Then we should not move forward with the interaction
            return statusResult;
        }

        // --- Requirements Met (statusResult.CanInteract is true), Proceed with Initiation ---
        // Debug.Log($"Interaction '{chosenDefinition.DisplayName}' initiated by {initiator.name} on {gameObject.name}. Suspicious: {statusResult.IsSuspicious}", this);

        // Create context for events
        InteractionContext context = new InteractionContext(initiator, this, chosenDefinition);

        // Fire Events
        InteractionInstance targetInstance = FindInteractionInstance(chosenDefinition);
        try
        {
            targetInstance.OnInteractionStart?.Invoke(context);
        }
        catch (System.Exception e) {
            Debug.LogError($"Exception during OnInteractionStart event for {chosenDefinition.name} on {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
        }


        // Trigger Target Animation (if specified on the definition)
        if (!string.IsNullOrEmpty(chosenDefinition.TargetAnimationTrigger))
        {
            Animator targetAnimator = GetComponent<Animator>(); // Animator on this interactable object
            if (targetAnimator != null)
            {
                try { targetAnimator.Play(chosenDefinition.TargetAnimationTrigger); }
                catch (System.Exception e) { Debug.LogError($"Exception playing animation '{chosenDefinition.TargetAnimationTrigger}' on {gameObject.name}: {e.Message}", this); }
            }
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            else { Debug.LogWarning($"{gameObject.name}: Interaction '{chosenDefinition.name}' specifies TargetAnimationTrigger '{chosenDefinition.TargetAnimationTrigger}', but no Animator component found on this object.", this); }
            #endif
        }

        // Return the success status (already populated by GetStatus)
        return statusResult;
    }

    /// <summary>
    /// Called by the initiator (e.g., InteractionState) when the interaction completes successfully (timer finishes).
    /// Fires the OnInteractionEnd events.
    /// </summary>
    /// <param name="completedDefinition">The definition of the interaction that completed.</param>
    /// <param name="initiator">The GameObject that completed the interaction.</param>
    public virtual void NotifyInteractionComplete(InteractionDefinitionSO completedDefinition, GameObject initiator)
    {
        if (completedDefinition == null || initiator == null) return; // Basic validation
        
        InteractionInstance targetInstance = FindInteractionInstance(completedDefinition);
        if (targetInstance == null)
        {
            Debug.LogError($"{gameObject.name}: Cannot notify completion for unknown interaction definition {completedDefinition.name}.", this);
            return;
        }

        // Debug.Log($"Interaction '{completedDefinition.DisplayName}' completed by {initiator.name} on {gameObject.name}.", this);

        // Create context and fire events
        InteractionContext context = new InteractionContext(initiator, this, completedDefinition);
        try {
            targetInstance.OnInteractionEnd?.Invoke(context);
        } catch (System.Exception e) {
             Debug.LogError($"Exception during OnInteractionEnd event for {completedDefinition.name} on {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
        }

    }

    /// <summary>
    /// Called by the initiator when the interaction state exits after completion. It is safer to trigger an interrupt
    /// on the controller after this event than it is after NotifyInteractionComplete as it will not immediately cause
    /// and infinite loop in the state graph controller.
    /// </summary>
    /// <param name="completedDefinition"></param>
    /// <param name="initiator"></param>
    public virtual void NotifyInteractionStateExited(InteractionDefinitionSO completedDefinition, GameObject initiator)
    {
        if (completedDefinition == null || initiator == null) return; // Basic validation
        
        InteractionInstance targetInstance = FindInteractionInstance(completedDefinition);
        if (targetInstance == null)
        {
            Debug.LogError($"{gameObject.name}: Cannot notify state exit for unknown interaction definition {completedDefinition.name}.", this);
            return;
        }

        // Create context and fire events
        InteractionContext context = new InteractionContext(initiator, this, completedDefinition);
        try {
            targetInstance.OnInteractionStateExited?.Invoke(context);
        } catch (System.Exception e) {
            Debug.LogError($"Exception during OnInteractionStateExited event for {completedDefinition.name} on {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
        }
    }

    /// <summary>
    /// Called by the initiator (e.g., InteractionState or Controller) when the interaction is interrupted before completion.
    /// Fires the OnInteractionInterrupted events.
    /// </summary>
    /// <param name="interruptedDefinition">The definition of the interaction that was interrupted.</param>
    /// <param name="initiator">The GameObject that was performing the interaction.</param>
    public virtual void NotifyInteractionInterrupted(InteractionDefinitionSO interruptedDefinition, GameObject initiator)
    {
         if (interruptedDefinition == null || initiator == null) return; // Basic validation

         InteractionInstance targetInstance = FindInteractionInstance(interruptedDefinition);
         if (targetInstance == null) return; // Ignore if instance not found

         // Debug.Log($"Interaction '{interruptedDefinition.DisplayName}' interrupted for {initiator.name} on {gameObject.name}.", this);

        // Create context and fire events
         InteractionContext context = new InteractionContext(initiator, this, interruptedDefinition);
        try {
            targetInstance.OnInteractionInterrupted?.Invoke(context);
        } catch (System.Exception e) {
             Debug.LogError($"Exception during OnInteractionInterrupted event for {interruptedDefinition.name} on {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
        }
    }
}