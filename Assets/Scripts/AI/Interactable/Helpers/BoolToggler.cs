using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A interaction reactor that sets a boolean value on part of the interactable lifecycle
/// </summary>
public class BoolToggler : AbstractInteractionReactor
{
    [SerializeField] private InteractionLifecycleEvent interactionLifecycleTrigger;
    [SerializeField] private InteractionDefinitionSO setTrueInteraction;
    [SerializeField] private InteractionDefinitionSO setFalseInteraction;
    [SerializeField] private BoolVariableSO targetBool;

    private void Initialize()
    {
        bool hasDefinitions = true;
        if (setTrueInteraction == null)
        {
            Debug.LogWarning("BoolSetter requires a target interaction definition.", this);
            hasDefinitions = false;
        } else if (!HasInteractionInstanceFor(setTrueInteraction))
        {
            Debug.LogWarning($"BoolSetter requires an interaction instance for {setTrueInteraction.name}.", this);
            hasDefinitions = false;
        }
        
        if (setFalseInteraction == null)
        {
            Debug.LogWarning("BoolSetter requires a target interaction definition.", this);
            hasDefinitions = false;
        } else if (!HasInteractionInstanceFor(setFalseInteraction))
        {
            Debug.LogWarning($"BoolSetter requires an interaction instance for {setFalseInteraction.name}.", this);
            hasDefinitions = false;
        }
        
        if (targetBool == null)
        {
            Debug.LogWarning("BoolSetter requires a target bool reference.", this);
            hasDefinitions = false;
        }


        if (hasDefinitions)
        {
            SafelyRegisterInteractionLifecycleCallback(
                interactionLifecycleTrigger, setTrueInteraction,
                HandleLifecycleEvent
            );
            SafelyRegisterInteractionLifecycleCallback(
                interactionLifecycleTrigger, setFalseInteraction,
                HandleLifecycleEvent
            );
        }
    }
    
    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        base.LoadSaveData(data, blankLoad);
        Initialize();
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        Initialize();
    }

    private void HandleValueChange()
    {
        if (targetBool != null)
        {
            SetInteractionEnabled(setTrueInteraction, !targetBool.Value, true, "The value is already true");
            SetInteractionEnabled(setFalseInteraction, targetBool.Value, true, "The value is already false");
        }
    }

    protected override void OnEnable()
    {
        // Our first job is to the enabled states of the two interactions based on the initial value of the bool
        base.OnEnable();
        HandleValueChange();
    }

    private void HandleLifecycleEvent(InteractionContext interactionContext)
    {
        if (targetBool == null)
        {
            Debug.LogWarning("BoolSetter requires a target bool reference.", this);
            return;
        }

        if (interactionContext.InteractionDefinition == setTrueInteraction)
        {
            targetBool.Value = true;
            HandleValueChange();
        }
        else if (interactionContext.InteractionDefinition == setFalseInteraction)
        {
            targetBool.Value = false;
            HandleValueChange();
        }
        else
        {
            Debug.LogWarning("BoolSetter requires a target bool reference.", this);
        }
    }
}