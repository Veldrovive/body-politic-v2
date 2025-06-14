using System;
using UnityEngine;

[Serializable]
public class WaitStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(WaitState);
    
    [SerializeField] public float m_duration;
}

public enum WaitStateOutcome
{
    Timeout,
}

public class WaitState : GenericAbstractState<WaitStateOutcome, WaitStateConfiguration>
{
    [Tooltip("The duration to wait before transitioning to the next state.")]
    [SerializeField] private float m_duration;

    private float startTime;

    public override void ConfigureState(WaitStateConfiguration configuration)
    {
        m_duration = configuration.m_duration;
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        startTime = SaveableDataManager.Instance.time;
    }

    private void Update()
    {
        if (SaveableDataManager.Instance.time - startTime >= m_duration)
        {
            // Transition to the next state
            TriggerExit(WaitStateOutcome.Timeout);
        }
    }
}