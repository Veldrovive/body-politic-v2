using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[BlackboardEnum]
public enum InteractActionError
{
    Error,
    ProximityCheckFailed,
    RoleCheckFailed
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Interact", story: "[Self] uses [InteractionDefinition] on [Interactable]", category: "Action", id: "68316e1a5e856b0c1b8fb13a9b74e820")]
public partial class InteractAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<InteractionDefinitionSO> InteractionDefinition;
    [SerializeReference] public BlackboardVariable<GameObject> Interactable;
    
    [SerializeReference] public BlackboardVariable<InteractActionError> error;

    private NpcContext npcContext;
    private Interactable targetInteractable; // The Interactable component to interact with.
    private float interactionTimer;
    private bool interactionInProgress = false;

    protected override Status OnLoad()
    {
        Debug.Log("Starting InteractAction state on " + Self.Value.name);
        if (!Self.Value.TryGetComponent(out npcContext))
        {
            Debug.LogError("InteractAction: Self does not have a NpcContext component.");
            error.Value = InteractActionError.Error;
            return Status.Failure;
        }
        if (InteractionDefinition.Value == null)
        {
            Debug.LogError("InteractAction: InteractionDefinition is not set.");
            error.Value = InteractActionError.Error;
            return Status.Failure;
        }
        if (Interactable.Value == null || !Interactable.Value.TryGetComponent(out targetInteractable))
        {
            Debug.LogError("InteractAction: Interactable is not set or does not have an Interactable component.");
            error.Value = InteractActionError.Error;
            return Status.Failure;
        }
        
        // --- Attempt to Initiate Interaction via Interactable ---
        // Interactable.TryInitiateInteraction handles its own internal checks (proximity, role, etc.)
        InteractionStatus initiateResult = targetInteractable.TryInitiateInteraction(
            InteractionDefinition,
            Self, // Initiator GameObject
            priority: 5
        );
        
        if (!initiateResult.CanInteract())
        {
            // Map the Interactable's failure reason to this State's error enum.
            if (initiateResult.HasFailureReason(InteractionFailureReason.InteractionDisabled))
            {
                Debug.LogWarning($"InteractionState: Interaction '{InteractionDefinition.Value.DisplayName}' is disabled on {targetInteractable.name}.", Self);
                // TriggerExit(InteractionStateOutcome.Error);
                error.Value = InteractActionError.Error;
                return Status.Failure;
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.RoleFailed))
            {
                // TriggerExit(InteractionStateOutcome.RoleCheckFailed);
                error.Value = InteractActionError.RoleCheckFailed;
                return Status.Failure;
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.ProximityFailed))
            {
                // TriggerExit(InteractionStateOutcome.ProximityCheckFailed);
                error.Value = InteractActionError.ProximityCheckFailed;
                return Status.Failure;
            }
            // Add checks for other specific reasons like InteractionDisabled, TargetBusy etc. if needed
            // else if (initiateResult.FailureReasons.Contains(InteractionFailureReason.TargetBusy)) { ... }
            else // Treat other failures as generic initiation failures.
            {
                // TriggerExit(InteractionStateOutcome.Error);
                error.Value = InteractActionError.Error;
                return Status.Failure;
            }
            // OnInteractionRejected?.Invoke(initiateResult.HumanReadableFailureReason);
        }
        
        // --- Initiation Successful ---
        // Debug.Log($"State {StateName} on {gameObject.name}: Successfully initiated interaction '{currentInteractionToPerform.DisplayName}'. Suspicious: {initiateResult.IsSuspicious}", this);

        interactionInProgress = true; // Mark that the interaction timer should start.
        interactionTimer = InteractionDefinition.Value.InteractionDuration; // Get duration from the definition.
        
        // --- Trigger Initiator Animation ---
        // Ensure Animator exists and trigger name is valid before attempting to set trigger.
        if (npcContext.AnimationManager != null && !string.IsNullOrEmpty(InteractionDefinition.Value.InitiatorAnimationTrigger))
        {
            try
            {
                // Set the animation trigger specified in the InteractionDefinitionSO.
                npcContext.AnimationManager.Play(InteractionDefinition.Value.InitiatorAnimationTrigger);
            }
            catch (Exception e) // Catch potential errors if the trigger name is invalid or Animator setup issues occur.
            {
                // Log an error if setting the trigger fails, but don't necessarily fail the whole state unless animation is critical.
                Debug.LogError($"Exception setting initiator animation trigger '{InteractionDefinition.Value.InitiatorAnimationTrigger}' on Animator of {npcContext.gameObject.name}: {e.Message}", Self);
                // Optionally: FailState(InteractionStateError.InitiationFailed, $"Failed to set animation trigger: {e.Message}"); return;
            }
        }
        
        // --- Trigger Sounds ---
        if (InteractionDefinition.Value.InitiatorSoundOnStart.Enabled)
        {
            TriggerSound(InteractionDefinition.Value.InitiatorSoundOnStart, Self);
        }
        if (InteractionDefinition.Value.TargetSoundOnStart.Enabled)
        {
            TriggerSound(InteractionDefinition.Value.TargetSoundOnStart, Interactable.Value);
        }
        
        // Check if the interaction that just started is suspicious based on InteractionDefinitionSO settings.
        if (initiateResult.IsSuspicious)
        {
            npcContext.SuspicionTracker?.AddSuspicionSource(InteractionDefinition.Value.ID, InteractionDefinition.Value.WitnessSuspicionLevel, interactionTimer);
        }
        
        // --- Check for Immediate Completion ---
        // If the interaction has zero duration, complete it immediately after starting.
        if (interactionTimer <= 0)
        {
            interactionInProgress = false; // Mark as not in progress before calling CompleteState.
            
            targetInteractable?.NotifyInteractionComplete(InteractionDefinition, Self);
            npcContext.SuspicionTracker?.RemoveSuspicionSource(InteractionDefinition.Value.ID);
            
            // Signal state success using the base controller.
            // TriggerExit(InteractionStateOutcome.CompletedInteraction);
            
            // We trigger and exited event after the exit so that if the target then sends an interrupt to the controller
            // it will occur during the next state. This helps prevent infinite loops of interactions, but does not
            // outright prevent them as the next state could interrupt back into this state potentially.
            targetInteractable?.NotifyInteractionStateExited(InteractionDefinition, Self);
            return Status.Success;
        }
        
        return Status.Running;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }
    
    void TriggerSound(InteractionSoundResult soundResult, GameObject creator)
    {
        SoundData data = new()
        {
            Clip = soundResult.Clip,
            CreatorObject = creator,
            EmanationPoint = creator.transform.position,
            CausesReactions = soundResult.CausesReactions,
            Loudness = soundResult.Loudness,
            Suspiciousness = soundResult.Suspiciousness,
            SType = soundResult.SType
        };
        npcContext.SoundHandler.RaiseSoundEvent(data);
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();
        
        // Only process timer if the interaction started successfully and hasn't finished or failed.
        if (!interactionInProgress) return Status.Success; // If interaction isn't running, do nothing.
        interactionTimer -= Time.deltaTime; // Decrement timer.
        
        if (interactionTimer <= 0)
        {
            // Timer finished, complete the interaction successfully.
            interactionInProgress = false; // Ensure flag is set before completing.

            // Notify the Interactable component *before* signaling state completion.
            targetInteractable?.NotifyInteractionComplete(InteractionDefinition, Self);
            
            // Trigger sounds
            if (InteractionDefinition.Value.InitiatorSoundOnFinish.Enabled)
            {
                TriggerSound(InteractionDefinition.Value.InitiatorSoundOnFinish, Self);
            }
            if (InteractionDefinition.Value.TargetSoundOnFinish.Enabled)
            {
                TriggerSound(InteractionDefinition.Value.TargetSoundOnFinish, Interactable.Value);
            }
            
            npcContext.SuspicionTracker?.RemoveSuspicionSource(InteractionDefinition.Value.ID);
            
            // TriggerExit(InteractionStateOutcome.CompletedInteraction);
            if (InteractionDefinition.Value.EndAnimationOnFinish)
            {
                npcContext.AnimationManager.End();
            }
            
            // We trigger and exited event after the exit so that if the target then sends an interrupt to the controller
            // it will occur during the next state. This helps prevent infinite loops of interactions, but does not
            // outright prevent them as the next state could interrupt back into this state potentially.
            targetInteractable?.NotifyInteractionStateExited(InteractionDefinition.Value, Self);
            return Status.Success;
        }
        
        return Status.Running;
    }

    protected override void OnEnd()
    {
        base.OnEnd();
        
        Debug.Log("Stopping InteractAction state on " + Self.Value.name);
        // If the interaction timer was still running when we were deactivated,
        // it implies an interruption occurred that wasn't handled by FailState/CompleteState already.
        if (interactionInProgress)
        {
            // Log for debugging purposes that an interruption occurred during deactivation.
            // Debug.LogWarning($"State {StateName} on {gameObject.name} deactivated while interaction was in progress. Notifying target of interruption.", this);
            interactionInProgress = false; // Mark as no longer in progress.

            // Notify the Interactable component that the interaction was cut short.
            // This allows the target to clean up its own state if necessary.
            targetInteractable?.NotifyInteractionInterrupted(InteractionDefinition, Self);
            
            // End the animation if it was running.
            if (npcContext.AnimationManager != null && !string.IsNullOrEmpty(InteractionDefinition.Value.InitiatorAnimationTrigger))
            {
                if (InteractionDefinition.Value.EndAnimationOnInterrupt)
                {
                    npcContext.AnimationManager.End();                    
                }
            }
        }

        npcContext.SuspicionTracker?.RemoveSuspicionSource(InteractionDefinition.Value.ID);
    }
}

