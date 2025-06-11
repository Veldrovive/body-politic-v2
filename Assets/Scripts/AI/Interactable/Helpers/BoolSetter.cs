using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A interaction reactor that sets a boolean value on part of the interactable lifecycle
/// </summary>
public class BoolSetter : AbstractInteractionReactor
{
    [SerializeField] private InteractionLifecycleEvent interactionLifecycleTrigger;
    [SerializeField] private InteractionDefinitionSO targetInteractionDefinition;
    [SerializeField] private BoolVariableSO targetBool;
    [SerializeField] private TargetValue targetValue;

    private enum TargetValue
    {
        True,
        False,
        Toggle
    }

    private void Initialize()
    {
        if (targetInteractionDefinition == null)
        {
            Debug.LogWarning("BoolSetter requires a target interaction definition.", this);
            return;
        }

        if (!HasInteractionInstanceFor(targetInteractionDefinition))
        {
            Debug.LogWarning($"BoolSetter requires an interaction instance for {targetInteractionDefinition.name}.", this);
            return;
        }

        SafelyRegisterInteractionLifecycleCallback(
            interactionLifecycleTrigger, targetInteractionDefinition,
            HandleLifecycleEvent
        );
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

    private void HandleLifecycleEvent(InteractionContext interactionContext)
    {
        if (targetBool == null)
        {
            Debug.LogWarning("BoolSetter requires a target bool reference.", this);
            return;
        }

        switch (targetValue)
        {
            case TargetValue.True:
                targetBool.Value = true;
                break;
            case TargetValue.False:
                targetBool.Value = false;
                break;
            case TargetValue.Toggle:
                targetBool.Value = !targetBool.Value;
                break;
            default:
                Debug.LogWarning("BoolSetter requires a target bool reference.", this);
                break;
        }
    }
}