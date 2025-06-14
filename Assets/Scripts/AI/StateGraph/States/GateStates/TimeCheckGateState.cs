using System;
using UnityEngine;

[Serializable]
public class TimeCheckGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(TimeCheckGateState);
    
    public float EndTime;
}

public enum TimeCheckGateStateOutcome
{
    Timeout,
    Continue
}

public class TimeCheckGateState : GenericAbstractState<TimeCheckGateStateOutcome, TimeCheckGateStateConfiguration>
{
    [SerializeField] private float endTime;
    public override void ConfigureState(TimeCheckGateStateConfiguration configuration)
    {
        endTime = configuration.EndTime;
    }
    
    public override bool InterruptState()
    {
        return true;
    }
    
    private void OnEnable()
    {
        // Check if the current time is past the end time
        if (SaveableDataManager.Instance.time >= endTime)
        {
            TriggerExit(TimeCheckGateStateOutcome.Timeout);
        }
        else
        {
            TriggerExit(TimeCheckGateStateOutcome.Continue);
        }
    }
}