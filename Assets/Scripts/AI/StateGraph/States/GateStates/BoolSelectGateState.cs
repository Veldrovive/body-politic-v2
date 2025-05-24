using System;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class BoolSelectGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(BoolSelectGateState);
    
    public BoolVariableSO BoolVariable;
}

public enum BoolSelectGateStateOutcome
{
    True,
    False
}

public class BoolSelectGateState : GenericAbstractState<BoolSelectGateStateOutcome, BoolSelectGateStateConfiguration>
{
    [Tooltip("The variableSO to store the boolean value in.")]
    [SerializeField] private BoolVariableSO boolVariable;
    
    public override void ConfigureState(BoolSelectGateStateConfiguration configuration)
    {
        boolVariable = configuration.BoolVariable;
    }

    public override bool InterruptState()
    {
        return true;
    }

    private void OnEnable()
    {
        if (boolVariable == null)
        {
            Debug.LogWarning("BoolVariable is null. Please assign a BoolVariableSO in the inspector.");
            TriggerExit(BoolSelectGateStateOutcome.False);
            return;
        }
        
        // Trigger the exit based on the value of the boolean variable
        if (boolVariable.Value)
        {
            TriggerExit(BoolSelectGateStateOutcome.True);
        }
        else
        {
            TriggerExit(BoolSelectGateStateOutcome.False);
        }
    }
}