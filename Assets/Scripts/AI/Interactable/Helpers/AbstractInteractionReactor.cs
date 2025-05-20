using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using System;

public enum InteractionLifecycleEvent
{
    OnStart,
    OnEnd,
    OnInterrupted
}

[RequireComponent(typeof(Interactable))]
public abstract class AbstractInteractionReactor : MonoBehaviour
{
    private Interactable _interactable;
    public Interactable GetInteractable()
    {
        if (_interactable == null)
        {
            _interactable = GetComponent<Interactable>();
        }
        return _interactable;
    }
    
    protected virtual void Awake()
    {
        Interactable interactable = GetInteractable();
        if (interactable == null)
        {
            Debug.LogError($"{GetType().Name} requires an Interactable component.", this);
        }
    }
    
    /// <summary>
    /// Checks if the connected Interactable has an interaction instance with a InteractionDefinition matching
    /// the one provided.
    /// Useful for providing warnings if the reactor requires interaction events that are not present.
    /// </summary>
    /// <param name="interactionDef">The InteractionDefinition to check for.</param>
    /// <returns></returns>
    public bool HasInteractionInstanceFor(InteractionDefinitionSO interactionDef)
    {
        Interactable interactable = GetInteractable();
        if (interactable == null) return false;
        
        return interactable.InteractionInstances.Any(
            x => x.InteractionDefinition == interactionDef
        );
    }
    
    /// <summary>
    /// Attempts to add a listener to the lifecycle event of the interaction instance matching the provided
    /// OnEnd corresponds to a completed event while OnInterrupt corresponds to an event that was not completed.
    /// </summary>
    /// <param name="lifecycleEvent"></param>
    /// <param name="interactionDef"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    public bool SafelyRegisterInteractionLifecycleCallback(InteractionLifecycleEvent lifecycleEvent, InteractionDefinitionSO interactionDef, UnityAction<InteractionContext> handler)
    {
        Interactable interactable = GetInteractable();
        if (interactable == null) return false;
        
        if (!HasInteractionInstanceFor(interactionDef))
        {
            Debug.LogError($"Cannot register lifecycle event {lifecycleEvent} for {interactionDef.name} on {gameObject.name}. No matching interaction instance found.", this);
            return false;
        }

        InteractionInstance interactionInstance = interactable.InteractionInstances.First(x => x.InteractionDefinition == interactionDef);
        UnityEvent<InteractionContext> eventToRegister = null;
        switch (lifecycleEvent)
        {
            case InteractionLifecycleEvent.OnStart:
                eventToRegister = interactionInstance.OnInteractionStart;
                break;
            case InteractionLifecycleEvent.OnEnd:
                eventToRegister = interactionInstance.OnInteractionEnd;
                break;
            case InteractionLifecycleEvent.OnInterrupted:
                eventToRegister = interactionInstance.OnInteractionInterrupted;
                break;
        }

        if (eventToRegister == null)
        {
            Debug.LogError($"Cannot register lifecycle event {lifecycleEvent} for {interactionDef.name} on {gameObject.name}. No matching event found.", this);
            return false;
        }
        
        // To safely add the event, we attempt to remove and then add the event.
        eventToRegister.RemoveListener(handler);
        eventToRegister.AddListener(handler);
        return true;
    }

    public bool SetInteractionEnabled(InteractionDefinitionSO interactionDef, bool isEnabled,
        bool disabledImpliesHidden = true, string reason = null)
    {
        return GetInteractable().SetInteractionEnableInfo(interactionDef, isEnabled, disabledImpliesHidden, reason);
    }
}
