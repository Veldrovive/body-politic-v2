using System;
using Unity.Behavior;
using UnityEngine;

public class SaveableBehaviorGraphAgent : BehaviorGraphAgent
{
    public string DisplayName;
    public string DisplayDescription;
    
    public string AgentId = Guid.NewGuid().ToString();  // Unique identifier for this agent, used for saving and loading
    public float Priority = 0;

    public void SetPriority(float priority)
    {
        Priority = priority;
    }
    
    public void SetId(string id = null)
    {
        AgentId = id ?? Guid.NewGuid().ToString();
    }
    
    public void SetDisplayData(string name, string description)
    {
        DisplayName = name;
        DisplayDescription = description;
    }
}