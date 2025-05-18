using System;
using System.ComponentModel;
using UnityEngine;

public abstract class AbstractState : MonoBehaviour
{
    public event Action<AbstractState, string> OnExit;
    protected void TriggerExit(string outcome)
    {
        OnExit?.Invoke(this, outcome);
    }
    
    public abstract void Configure(AbstractStateConfiguration configuration);

    public abstract bool Interrupt();
}