using System;
using UnityEngine;

/// Derives from Interactable
/// Initiating interaction now also requires a priority. The interaction is rejected if the NPC can't be interrupted
/// with the given priority.
/// An interaction request succeeding interrupts into a state where we play the animation for the given duration of the
/// interaction.

[RequireComponent(typeof(NpcContext))]
public class InteractableNpc : Interactable
{
    private NpcContext npcContext;

    private class NpcInteractionInstance
    {
        public InteractionDefinitionSO chosenDefinition;
        public GameObject initiator;
        public int priority;
        public string interruptGraphId;
    }
    private NpcInteractionInstance _currentNpcInteractionInstance;
    
    private void Awake()
    {
        // Get the NpcContext component attached to this GameObject
        npcContext = GetComponent<NpcContext>();
        if (npcContext == null)
        {
            Debug.LogError("NpcContext component not found on this GameObject.");
        }
    }

    /// <summary>
    /// Called by an initiator (e.g., InteractionState) to attempt starting an interaction.
    /// Modifies the base's TryInitiateInteraction to account for the fact that NPC interactions always interrupt
    /// the StateGraphController to play the animation correctly
    /// </summary>
    /// <param name="chosenDefinition"></param>
    /// <param name="initiator"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public override InteractionStatus TryInitiateInteraction(InteractionDefinitionSO chosenDefinition, GameObject initiator,
        int priority = 0)
    {
        InteractionStatus statusResult = GetInteractionStatus(chosenDefinition, initiator);
        if (!statusResult.CanInteract())
        {
            // Then we should not move forward with the interaction
            return statusResult;
        }

        if (_currentNpcInteractionInstance != null)
        {
            // Then we are already in an interaction and we refuse to interrupt it. Mostly because we have no way of
            // telling the initiator that the interaction was interrupted. Perhaps something to add later so that
            // we can interrupt if there is a higher priority interaction
            statusResult.AddFailureReason(chosenDefinition.GetHumanReadableFailureReason(InteractionFailureReason.NpcInterruptFailed));
            return statusResult;
        }
        
        // There are no immediately concerns that would cause the interaction to fail. The last check we need to make is
        // to establish whether we can interrupt the current state with the given priority.
        // Since it is the last check, we can just attempt the interrupt. If it succeeds we can move forward. If it fails
        // we add a FailureReason to the status and return it which informs the initiator that the interaction failed.
        string graphId = Guid.NewGuid().ToString();
        AnimateGraphFactory factory = new(new AnimateGraphConfiguration()
        {
            animateStateConfig = new(
                chosenDefinition.TargetAnimationTrigger,
                chosenDefinition.InteractionDuration,
                chosenDefinition.EndAnimationOnFinish,
                chosenDefinition.EndAnimationOnInterrupt
            ),
            GraphId = graphId
        });
        if (!npcContext.StateGraphController.TryInterrupt(factory, false, false, priority))
        {
            // The interrupt failed. We should add a failure reason to the status so that the initiator can handle the failure
            statusResult.AddFailureReason(chosenDefinition.GetHumanReadableFailureReason(InteractionFailureReason.NpcInterruptFailed));
            return statusResult;
        }

        _currentNpcInteractionInstance = new()
        {
            chosenDefinition = chosenDefinition,
            initiator = initiator,
            priority = priority,
            interruptGraphId = graphId
        };
        
        // Otherwise, we have now started the animation. We raise the event to inform any listeners that an interaction
        // has started and return the status result that has CanInteract == true
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
        
        return statusResult;
    }

    public void EndCurrentAnimationState()
    {
        // Used to hard exit the animation state if we are still in it when the interaction ends
        if (_currentNpcInteractionInstance != null)
        {
            // We can use the RemoveStateGraphById utility to ensure that we entirely remove the state graph in case it
            // somehow got into the queue. Setting includeCurrent will force end the graph if it is currently running.
            // If the graph had already exited, this will do nothing
            npcContext.StateGraphController.RemoveStateGraphById(_currentNpcInteractionInstance.interruptGraphId, includeCurrent: true);
        }
    }
    
    public override void NotifyInteractionInterrupted(InteractionDefinitionSO interruptedDefinition, GameObject initiator)
    {
        EndCurrentAnimationState();
        _currentNpcInteractionInstance = null;
        
        base.NotifyInteractionInterrupted(interruptedDefinition, initiator);
    }

    public override void NotifyInteractionComplete(InteractionDefinitionSO completedDefinition, GameObject initiator)
    {
        EndCurrentAnimationState();
        _currentNpcInteractionInstance = null;
        
        base.NotifyInteractionComplete(completedDefinition, initiator);
    }
}