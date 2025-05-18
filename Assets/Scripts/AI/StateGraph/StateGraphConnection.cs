using System;
using UnityEngine;

[Serializable]
public struct StateGraphConnectionPort
{
    public string nodeId;
    public NodePortInfo portInfo;
    
    public StateGraphConnectionPort(string nodeId, NodePortInfo portInfo)
    {
        this.nodeId = nodeId;
        this.portInfo = portInfo;
    }
}

[Serializable]
public struct StateGraphConnection
{
    public StateGraphConnectionPort inputPort;
    public StateGraphConnectionPort outputPort;
    
    public StateGraphConnection(StateGraphConnectionPort inputPort, StateGraphConnectionPort outputPort)
    {
        this.inputPort = inputPort;
        this.outputPort = outputPort;
    }
    
    public StateGraphConnection(string inputNodeId, NodePortInfo inputPort, string outputNodeId, NodePortInfo outputPort)
    {
        this.inputPort = new StateGraphConnectionPort(inputNodeId, inputPort);
        this.outputPort = new StateGraphConnectionPort(outputNodeId, outputPort);
    }
}
