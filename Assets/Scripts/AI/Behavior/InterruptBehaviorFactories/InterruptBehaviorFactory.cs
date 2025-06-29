using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

public class InterruptBehaviorDefinition
{
    public BehaviorGraph BehaviorGraph = null;
    public Dictionary<string, object> BlackboardData = new();

    public float Priority = 0f;
    public bool SaveContext = false;

    public string Id = null;
    public string DisplayName = "";
    public string DisplayDescription = "";

    public InterruptBehaviorDefinition() { }

    public InterruptBehaviorDefinition(BehaviorParameters parameters)
    {
        Id = parameters.AgentId;
        Priority = parameters.Priority;
        SaveContext = parameters.SaveContext;
    }
}

public abstract class BehaviorParameters
{
    public float Priority = 0f;
    public bool SaveContext = false;
    public string AgentId = null;
}

public abstract class InterruptBehaviorFactory<TInterruptParameters> : ScriptableObject
    where TInterruptParameters : BehaviorParameters
{
    public abstract InterruptBehaviorDefinition GetInterruptDefinition(TInterruptParameters interruptParameters);
}