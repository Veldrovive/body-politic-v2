using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

/// <summary>
/// Utility for easily executing a move and then an interact.
/// Uses control triggers to find whether moving is necessary and whether a custom action is used.
/// If an exact position is required or we do not start within range, we begin moving towards the move to target transform.
/// 
/// </summary>

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Use", story: "[Self] uses [Interaction_Definition] on [Interactable]", category: "Action", id: "f423492ef0f5d30fc2903616c7f2a622")]
public partial class UseAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<MoveAndUseGraphOutcome> Outcome;
    
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<InteractionDefinitionSO> Interaction_Definition;
    [SerializeReference] public BlackboardVariable<GameObject> Interactable;
    
    [SerializeReference] public BlackboardVariable<MovementSpeed> Speed = new(MovementSpeed.NpcSpeed);
    private enum UseActionState
    {
        Unstarted,
        Moving,
        Interacting,
        Completed,
    }

    private enum UseActionMovementState
    {
        InProgress,
        Success,
        Failed
    }

    private UseActionState state = UseActionState.Unstarted;
    private Interactable targetInteractable = null;
    private PlayerControlTrigger trigger = null;
    private NpcContext npcContext;
    private UseActionMovementState useActionMovementState = UseActionMovementState.InProgress;
    private float interactionTimer = 0f;

    private List<PlayerControlTrigger> FindControlTriggers()
    {
        // We want to know what control trigger components are present on the Interactable or its children.
        var triggers = new List<PlayerControlTrigger>();
        if (Interactable.Value != null)
        {
            triggers.AddRange(Interactable.Value.GetComponentsInChildren<PlayerControlTrigger>());
        }
        return triggers;
    }
    
    protected override Status OnLoad()
    {
        npcContext = Self.Value.GetComponent<NpcContext>();
        if (npcContext == null)
        {
            Debug.LogError($"NpcContext not found on {Self.Value.name}. UseAction requires a valid NpcContext.");
            return Status.Failure;
        }
        
        // Try to find a control trigger with the correct interaction definition.
        trigger = null;
        foreach (var controlTrigger in FindControlTriggers())
        {
            if (controlTrigger.TargetInteractionDefinition == Interaction_Definition.Value)
            {
                trigger = controlTrigger;
                break;
            }
        }

        if (trigger == null)
        {
            Debug.LogError($"No control trigger found for interaction definition {Interaction_Definition.Value.name} on {Interactable.Value.name}");
            Outcome.Value = MoveAndUseGraphOutcome.Error;
            return Status.Failure;
        }

        targetInteractable = trigger.TargetInteractable;
        if (targetInteractable == null)
        {
            Debug.LogError($"No target interactable found for interaction definition {Interaction_Definition.Value.name} on {Interactable.Value.name}");
            Outcome.Value = MoveAndUseGraphOutcome.Error;
            return Status.Failure;
        }

        if (trigger.IsCustomAction)
        {
            // Then this will interrupt the controller Self's behaviors with the custom action.
            // TODO: Figure out how this should work. Right now this will cause the current graph to stop and the custom action to run.
            // This ends up restarting the current graph. I guess we could just check if this has a child and if it does
            // throw an error telling the designer that this is not allowed. We can't use a dynamic subgraph since those
            // are not serializable.
            // If we do restart, the designer also needs to know to put the state variable change before the UseAction node.
            throw new NotSupportedException("Custom actions are not supported in UseAction. Please use a different action node.");
        }
        else
        {
            // Otherwise, we can handle everything internally.
            return StartMoveAndUse();
        }
    }

    private bool IsWithinRange()
    {
        float requiredProximity = Interaction_Definition.Value.RequiredProximity;
        float requiredProximitySqr = requiredProximity * requiredProximity;
        return (Interactable.Value.transform.position - Self.Value.transform.position).sqrMagnitude <= requiredProximitySqr;
    }

    private Status StartMoveAndUse()
    {
        // Check if we need to move first.
        bool mustMove = trigger.RequireExactPosition || !IsWithinRange();
        return mustMove ? StartMove() : StartUse();
    }
    
    private NpcMovementRequest ConstructMovementRequest(Transform moveTarget, bool exactPosition, bool finalAlignment)
    {
        var request = new NpcMovementRequest(moveTarget)
        {
            DesiredSpeed = Speed,
            RequireExactPosition = exactPosition,
            RequireFinalAlignment = finalAlignment,
            ExitOnComplete = true,
            SampleRadius = Interaction_Definition.Value.RequiredProximity,
            SamplePointSearchRadius = Interaction_Definition.Value.RequiredProximity
        };

        return request;
    }

    private Status StartMove()
    {
        Transform moveTarget = trigger.MoveToTargetTransform;
        bool requireExactPosition = trigger.RequireExactPosition;
        bool requireFinalAlignment = trigger.RequireFinalAlignment;

        if (moveTarget == null)
        {
            Debug.LogError($"No move target found for {trigger.name}");
            Outcome.Value = MoveAndUseGraphOutcome.Error;
            return Status.Failure;
        }
        
        var movementRequest = ConstructMovementRequest(moveTarget, requireExactPosition, requireFinalAlignment);
        
        var (canPath, _) = npcContext.MovementManager.CanSatisfyRequest(movementRequest);
        if (!canPath)
        {
            Debug.LogWarning($"Can't satisfy {movementRequest}");
            Outcome.Value = MoveAndUseGraphOutcome.Error;
            return Status.Failure;
        }
        
        var failureReason = npcContext.MovementManager.SetMovementTarget(movementRequest);

        if (failureReason.HasValue)
        {
            OnMovementFailed(failureReason.Value, null);
            Outcome.Value = MoveAndUseGraphOutcome.Error;
            return Status.Failure;
        }
        else
        {
            npcContext.MovementManager.OnRequestCompleted -= OnMovementCompleted;
            npcContext.MovementManager.OnRequestCompleted += OnMovementCompleted;
            npcContext.MovementManager.OnRequestFailed -= OnMovementFailed;
            npcContext.MovementManager.OnRequestFailed += OnMovementFailed;
            
            useActionMovementState = UseActionMovementState.InProgress;
            state = UseActionState.Moving;
            return Status.Running;
        }
    }

    private void OnMovementFailed(MovementFailureReason failureReason, object failureData)
    {
        npcContext.MovementManager.OnRequestCompleted -= OnMovementCompleted;
        npcContext.MovementManager.OnRequestFailed -= OnMovementFailed;
        
        switch (failureReason)
        {
            case MovementFailureReason.TargetTransformNull:
            case MovementFailureReason.TargetPositionInvalid:
            case MovementFailureReason.AgentNotOnNavMesh:
            case MovementFailureReason.NoValidPathFound:
            case MovementFailureReason.RequestNull:
            case MovementFailureReason.InvalidRequestParameters:
                useActionMovementState = UseActionMovementState.Failed;
                Outcome.Value = MoveAndUseGraphOutcome.Error;
                break;
            case MovementFailureReason.LinkTraversalFailed:
                useActionMovementState = UseActionMovementState.Failed;
                Outcome.Value = MoveAndUseGraphOutcome.DoorRoleFailed;
                break;
            case MovementFailureReason.Interrupted:
            case MovementFailureReason.ReplanningFailed:
                // These are not errors. We just ignore them.
                break;
            default:
                Debug.LogError($"Unexpected movement failure reason: {failureReason}");
                useActionMovementState = UseActionMovementState.Failed;
                Outcome.Value = MoveAndUseGraphOutcome.Error;
                break;
        }
    }

    private void OnMovementCompleted()
    {
        npcContext.MovementManager.OnRequestCompleted -= OnMovementCompleted;
        npcContext.MovementManager.OnRequestFailed -= OnMovementFailed;
    
        useActionMovementState = UseActionMovementState.Success;
    }

    /// <summary>
    /// Called in the node update loop when the node is in the moving state.
    /// </summary>
    /// <returns></returns>
    private Status ProcessMove()
    {
        // TODO: Implement a check to see if we are in range and do not require exact position as an early exit.
        switch (useActionMovementState)  // Gets set in OnMovementCompleted or OnMovementFailed between updates.
        {
            case UseActionMovementState.InProgress:
                return Status.Running;
            case UseActionMovementState.Success:
                return Status.Success;
            case UseActionMovementState.Failed:
                return Status.Failure;
            default:
                Debug.LogError("Unknown use action state: " + useActionMovementState);
                return Status.Failure;
        }
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
    
    private Status StartUse()
    {
        // --- Attempt to Initiate Interaction via Interactable ---
        // Interactable.TryInitiateInteraction handles its own internal checks (proximity, role, etc.)
        
        InteractionStatus initiateResult = targetInteractable.TryInitiateInteraction(
            Interaction_Definition,
            Self, // Initiator GameObject
            priority: 5
        );
        
        if (!initiateResult.CanInteract())
        {
            // Map the Interactable's failure reason to this State's error enum.
            if (initiateResult.HasFailureReason(InteractionFailureReason.InteractionDisabled))
            {
                Debug.LogWarning($"InteractionState: Interaction '{Interaction_Definition.Value.DisplayName}' is disabled on {targetInteractable.name}.", Self);
                Outcome.Value = MoveAndUseGraphOutcome.Error;
                return Status.Failure;
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.RoleFailed))
            {
                Outcome.Value = MoveAndUseGraphOutcome.Error;
                return Status.Failure;
            }
            else if (initiateResult.HasFailureReason(InteractionFailureReason.ProximityFailed))
            {
                Outcome.Value = MoveAndUseGraphOutcome.InteractionProximityFailed;
                return Status.Failure;
            }
            // Add checks for other specific reasons like InteractionDisabled, TargetBusy etc. if needed
            // else if (initiateResult.FailureReasons.Contains(InteractionFailureReason.TargetBusy)) { ... }
            else // Treat other failures as generic initiation failures.
            {
                Outcome.Value = MoveAndUseGraphOutcome.Error;
                return Status.Failure;
            }
        }
        
        interactionTimer = Interaction_Definition.Value.InteractionDuration; // Get duration from the definition.
        
        // --- Trigger Initiator Animation ---
        // Ensure Animator exists and trigger name is valid before attempting to set trigger.
        if (npcContext.AnimationManager != null && !string.IsNullOrEmpty(Interaction_Definition.Value.InitiatorAnimationTrigger))
        {
            try
            {
                // Set the animation trigger specified in the InteractionDefinitionSO.
                npcContext.AnimationManager.Play(Interaction_Definition.Value.InitiatorAnimationTrigger);
            }
            catch (Exception e) // Catch potential errors if the trigger name is invalid or Animator setup issues occur.
            {
                // Log an error if setting the trigger fails, but don't necessarily fail the whole state unless animation is critical.
                Debug.LogError($"Exception setting initiator animation trigger '{Interaction_Definition.Value.InitiatorAnimationTrigger}' on Animator of {npcContext.gameObject.name}: {e.Message}", Self);
                // Optionally: FailState(InteractionStateError.InitiationFailed, $"Failed to set animation trigger: {e.Message}"); return;
            }
        }
        
        // --- Trigger Sounds ---
        if (Interaction_Definition.Value.InitiatorSoundOnStart.Enabled)
        {
            TriggerSound(Interaction_Definition.Value.InitiatorSoundOnStart, Self);
        }
        if (Interaction_Definition.Value.TargetSoundOnStart.Enabled)
        {
            TriggerSound(Interaction_Definition.Value.TargetSoundOnStart, Interactable.Value);
        }
        
        // Check if the interaction that just started is suspicious based on InteractionDefinitionSO settings.
        if (initiateResult.IsSuspicious)
        {
            npcContext.SuspicionTracker?.AddSuspicionSource(Interaction_Definition.Value.ID, Interaction_Definition.Value.WitnessSuspicionLevel, interactionTimer);
        }
        
        // --- Check for Immediate Completion ---
        // If the interaction has zero duration, complete it immediately after starting.
        if (interactionTimer <= 0)
        {
            targetInteractable?.NotifyInteractionComplete(Interaction_Definition, Self);
            npcContext.SuspicionTracker?.RemoveSuspicionSource(Interaction_Definition.Value.ID);
            
            // We trigger and exited event after the exit so that if the target then sends an interrupt to the controller
            // it will occur during the next state. This helps prevent infinite loops of interactions, but does not
            // outright prevent them as the next state could interrupt back into this state potentially.
            targetInteractable?.NotifyInteractionStateExited(Interaction_Definition, Self);

            Outcome.Value = MoveAndUseGraphOutcome.Completed;
            state = UseActionState.Completed;
            return Status.Success;
        }
        
        state = UseActionState.Interacting; // Move to the interacting state.
        return Status.Running;
    }

    /// <summary>
    /// Called in the node update loop when the node is in the interacting state.
    /// </summary>
    /// <returns></returns>
    private Status ProcessUse()
    {
        interactionTimer -= Time.deltaTime; // Decrement timer.
        
        if (interactionTimer <= 0)
        {
            // Notify the Interactable component *before* signaling state completion.
            targetInteractable?.NotifyInteractionComplete(Interaction_Definition, Self);
            
            // Trigger sounds
            if (Interaction_Definition.Value.InitiatorSoundOnFinish.Enabled)
            {
                TriggerSound(Interaction_Definition.Value.InitiatorSoundOnFinish, Self);
            }
            if (Interaction_Definition.Value.TargetSoundOnFinish.Enabled)
            {
                TriggerSound(Interaction_Definition.Value.TargetSoundOnFinish, Interactable.Value);
            }
            
            npcContext.SuspicionTracker?.RemoveSuspicionSource(Interaction_Definition.Value.ID);
            
            // TriggerExit(InteractionStateOutcome.CompletedInteraction);
            if (Interaction_Definition.Value.EndAnimationOnFinish)
            {
                npcContext.AnimationManager.End();
            }
            
            // We trigger and exited event after the exit so that if the target then sends an interrupt to the controller
            // it will occur during the next state. This helps prevent infinite loops of interactions, but does not
            // outright prevent them as the next state could interrupt back into this state potentially.
            targetInteractable?.NotifyInteractionStateExited(Interaction_Definition.Value, Self);
            
            Outcome.Value = MoveAndUseGraphOutcome.Completed;
            return Status.Success;
        }
        
        return Status.Running;
    }
    
    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();

        switch (state)
        {
            case UseActionState.Unstarted:
                // We should have moved to another state in OnLoad.
                Debug.LogWarning("UseAction was not started properly. This should not happen.");
                return Status.Failure;
            case UseActionState.Moving:
            {
                Status moveStatus = ProcessMove();
                if (moveStatus is Status.Running or Status.Failure)
                {
                    return moveStatus;
                }
                else if (moveStatus == Status.Success)
                {
                    // Then instead of succeeding the entire action, we start the interaction.
                    state = UseActionState.Interacting;
                    return StartUse();
                }
                else
                {
                    Debug.LogError($"Unexpected status from ProcessMove: {moveStatus}");
                    return Status.Failure;
                }
            }
            case UseActionState.Interacting:
            {
                Status useStatus = ProcessUse();
                if (useStatus is Status.Running or Status.Failure)
                {
                    return useStatus;
                }
                else if (useStatus == Status.Success)
                {
                    state = UseActionState.Completed;
                    return Status.Success;
                }
                else
                {               
                    Debug.LogError($"Unexpected status from ProcessUse: {useStatus}");
                    return Status.Failure;
                }
            }
            case UseActionState.Completed:
                // The action has already completed.
                return Status.Success;
            default:
                Debug.LogError($"UseAction is in an unknown state: {state}");
                return Status.Failure;
        }
    }

    protected override void OnEnd()
    {
        base.OnEnd();

        // Always try to unsubscribe from movement events to prevent leaks.
        if (npcContext?.MovementManager != null)
        {
            npcContext.MovementManager.OnRequestCompleted -= OnMovementCompleted;
            npcContext.MovementManager.OnRequestFailed -= OnMovementFailed;
        }

        if (state == UseActionState.Moving)
        {
            // If we were moving, we must interrupt the movement.
            npcContext.MovementManager.InterruptCurrentRequest();
        }
        else if (state == UseActionState.Interacting)
        {
            // If we were interrupted during the interaction, notify the target.
            targetInteractable?.NotifyInteractionInterrupted(Interaction_Definition, Self);

            // Clean up animation if configured to do so.
            if (npcContext?.AnimationManager != null && 
                !string.IsNullOrEmpty(Interaction_Definition.Value.InitiatorAnimationTrigger) &&
                Interaction_Definition.Value.EndAnimationOnInterrupt)
            {
                npcContext.AnimationManager.End();
            }
        }
    
        // Always remove the suspicion source on exit, regardless of state.
        npcContext?.SuspicionTracker?.RemoveSuspicionSource(Interaction_Definition.Value.ID);

        // Reset internal state for the next run.
        state = UseActionState.Unstarted;
        useActionMovementState = UseActionMovementState.InProgress;
        interactionTimer = 0f;
    }
}

