using System;
using UnityEngine;

[Serializable]
public class InteractionStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(InteractionState);

    /// <summary>The Interactable component to target.</summary>
    public GameObjectReference TargetInteractableGO;
    /// <summary>The InteractionDefinitionSO defining the interaction to perform.</summary>
    public InteractionDefinitionSO InteractionDefinition;

    public InteractionStateConfiguration()
    {
        TargetInteractableGO = new GameObjectReference();
        InteractionDefinition = null;
    }

    /// <summary>
    /// Constructor for creating configuration data.
    /// </summary>
    /// <param name="target">The target interactable.</param>
    /// <param name="definition">The interaction definition.</param>
    public InteractionStateConfiguration(GameObject target, InteractionDefinitionSO definition)
    {
        TargetInteractableGO = new GameObjectReference(target);
        InteractionDefinition = definition;
    }

    public InteractionStateConfiguration(GameObjectVariableSO target, InteractionDefinitionSO definition)
    {
        TargetInteractableGO = new GameObjectReference(target);
        InteractionDefinition = definition;
    }
}

/// <summary>
/// Possible reasons for the InteractionState failing. Used by the AbstractCommunicativeCharState failure handling system.
/// </summary>
public enum InteractionStateOutcome
{
    CompletedInteraction,
    Error,
    /// <summary>The initiator failed the proximity check defined by the InteractionDefinitionSO.</summary>
    ProximityCheckFailed,
    /// <summary>The initiator failed the role check defined by the InteractionDefinitionSO.</summary>
    RoleCheckFailed,
}

public class InteractionState : GenericAbstractState<InteractionStateOutcome, InteractionStateConfiguration>
{
    #region Unity Inspector Variables
    
    [Header("Interaction Setup (Can be overridden by Configure)")]
    [Tooltip("Default Interactable component to interact with. Can be overridden dynamically via Configure.")]
    [SerializeField] private GameObjectReference targetInteractableGO;
    [Tooltip("Default Interaction Definition SO to execute. Can be overridden dynamically via Configure.")]
    [SerializeField] private InteractionDefinitionSO interactionToPerform;
    
    #endregion
    
    
    #region Internal State
    
    private Interactable targetInteractable; // The Interactable component to interact with.
    private float interactionTimer;
    private bool interactionInProgress = false;
    
    #endregion


    #region Configuration

    public override void ConfigureState(InteractionStateConfiguration configuration)
    {
        // Set the target Interactable and Interaction Definition from the configuration
        targetInteractableGO = configuration.TargetInteractableGO;
        interactionToPerform = configuration.InteractionDefinition;
    }

    #endregion


    #region Lifecycle

    private void OnEnable()
    {
        targetInteractable = targetInteractableGO.Value.GetComponent<Interactable>();
        if (targetInteractable == null)
        {
            Debug.LogError($"InteractionState: Target Interactable is null or missing component on {targetInteractableGO.Value.name}.", this);
            TriggerExit(InteractionStateOutcome.Error);
            return;
        }
        
        if (interactionToPerform == null)
        {
            Debug.LogError($"InteractionState: Interaction Definition is null on {targetInteractableGO.Value.name}.", this);
            TriggerExit(InteractionStateOutcome.Error);
            return;
        }
        
        // NpcContext is guaranteed by RequireComponent and assigned by the base class (AbstractCharStateBase.Awake).
        // We only need to check sub-properties like Identity if required by the interaction logic.
        if (npcContext.Identity == null)
        {
            Debug.LogError($"InteractionState: NpcContext is missing Identity on {npcContext.gameObject.name}.", this);
            TriggerExit(InteractionStateOutcome.Error);
            return;
        }
        
        // --- Attempt to Initiate Interaction via Interactable ---
        // Interactable.TryInitiateInteraction handles its own internal checks (proximity, role, etc.)
        InteractionStatus initiateResult = targetInteractable.TryInitiateInteraction(
            interactionToPerform,
            this.gameObject // Initiator GameObject
        );
        
        if (!initiateResult.CanInteract())
        {
            // Map the Interactable's failure reason to this State's error enum.
            if (initiateResult.HasFailureReason(InteractionFailureReason.InteractionDisabled))
            {
                TriggerExit(InteractionStateOutcome.Error);
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.RoleFailed))
            {
                TriggerExit(InteractionStateOutcome.RoleCheckFailed);
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.ProximityFailed))
            {
                TriggerExit(InteractionStateOutcome.ProximityCheckFailed);
            }
            // Add checks for other specific reasons like InteractionDisabled, TargetBusy etc. if needed
            // else if (initiateResult.FailureReasons.Contains(InteractionFailureReason.TargetBusy)) { ... }
            else // Treat other failures as generic initiation failures.
            {
                TriggerExit(InteractionStateOutcome.Error);
            }
            return; // FailState handles exiting
        }
        
        // --- Initiation Successful ---
        // Debug.Log($"State {StateName} on {gameObject.name}: Successfully initiated interaction '{currentInteractionToPerform.DisplayName}'. Suspicious: {initiateResult.IsSuspicious}", this);

        interactionInProgress = true; // Mark that the interaction timer should start.
        interactionTimer = interactionToPerform.InteractionDuration; // Get duration from the definition.
        
        // --- Trigger Initiator Animation ---
        // Ensure Animator exists and trigger name is valid before attempting to set trigger.
        if (npcContext.AnimationManager != null && !string.IsNullOrEmpty(interactionToPerform.InitiatorAnimationTrigger))
        {
            try
            {
                // Set the animation trigger specified in the InteractionDefinitionSO.
                npcContext.AnimationManager.SetTrigger(interactionToPerform.InitiatorAnimationTrigger);
            }
            catch (Exception e) // Catch potential errors if the trigger name is invalid or Animator setup issues occur.
            {
                // Log an error if setting the trigger fails, but don't necessarily fail the whole state unless animation is critical.
                Debug.LogError($"Exception setting initiator animation trigger '{interactionToPerform.InitiatorAnimationTrigger}' on Animator of {npcContext.gameObject.name}: {e.Message}", this);
                // Optionally: FailState(InteractionStateError.InitiationFailed, $"Failed to set animation trigger: {e.Message}"); return;
            }
        }
        
        // Check if the interaction that just started is suspicious based on InteractionDefinitionSO settings.
        if (initiateResult.IsSuspicious)
        {
            npcContext.SuspicionTracker?.AddSuspicionSource(StateId, interactionToPerform.WitnessSuspicionLevel, interactionTimer);
        }
        
        // --- Check for Immediate Completion ---
        // If the interaction has zero duration, complete it immediately after starting.
        if (interactionTimer <= 0)
        {
            interactionInProgress = false; // Mark as not in progress before calling CompleteState.
            // Notify target first
            targetInteractable?.NotifyInteractionComplete(interactionToPerform, this.gameObject);
            // Signal state success using the base method.
            TriggerExit(InteractionStateOutcome.CompletedInteraction);

            npcContext.SuspicionTracker?.RemoveSuspicionSource(StateId);
        }
    }
    
    /// <summary>
    /// Called every frame by Unity. Updates the interaction timer if the interaction is active.
    /// </summary>
    void Update()
    {
        // Only process timer if the interaction started successfully and hasn't finished or failed.
        if (!interactionInProgress) return; // If interaction isn't running, do nothing.

        interactionTimer -= Time.deltaTime; // Decrement timer.

        if (interactionTimer <= 0)
        {
            // Timer finished, complete the interaction successfully.
            interactionInProgress = false; // Ensure flag is set before completing.

            // Notify the Interactable component *before* signaling state completion.
            targetInteractable?.NotifyInteractionComplete(interactionToPerform, this.gameObject);
            
            npcContext.SuspicionTracker?.RemoveSuspicionSource(StateId);
            
            TriggerExit(InteractionStateOutcome.CompletedInteraction);
        }
    }

    #endregion

    public override bool InterruptState()
    {
        // If the interaction timer was still running when we were deactivated,
        // it implies an interruption occurred that wasn't handled by FailState/CompleteState already.
        if (interactionInProgress)
        {
            // Log for debugging purposes that an interruption occurred during deactivation.
            // Debug.LogWarning($"State {StateName} on {gameObject.name} deactivated while interaction was in progress. Notifying target of interruption.", this);
            interactionInProgress = false; // Mark as no longer in progress.

            // Notify the Interactable component that the interaction was cut short.
            // This allows the target to clean up its own state if necessary.
            targetInteractable?.NotifyInteractionInterrupted(interactionToPerform, this.gameObject);
        }

        npcContext.SuspicionTracker?.RemoveSuspicionSource(StateId);
        return true;
    }
}