using System;
using UnityEngine;

[NodeInfo("Debug Log", "Debug/Log", nodeWidth: 400)]
public class DebugLogNode : EventListenerNode
{
    [SerializeField] private string m_prefix;

    [EventInputPort("Log Message")]
    public void Log(string message)
    {
        Debug.Log($"{m_prefix}: {message}");
    }
}