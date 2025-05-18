using System.Collections.Generic;
using UnityEngine;

[NodeInfo("Jump Input", "Lifecycle/Jump Input")]
public class JumpInputNode : StateGraphNode
{
    public static string IN_PORT_NAME = "Jump";
    [SerializeField] private string jumpKey = "";
    public string JumpKey => jumpKey;
    
    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        ports.Add(new NodePortContext(new NodePortInfo(IN_PORT_NAME, PortType.StateTransitionIn), typeof(StateNodePort), null));
        return ports;
    }
}

[NodeInfo("Jump Output", "Lifecycle/Jump Output")]
public class JumpOutputNode : StateGraphNode
{
    public static string OUT_PORT_NAME = "Jump";
    [SerializeField] private string jumpKey = "";
    public string JumpKey => jumpKey;
    
    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        ports.Add(new NodePortContext(new NodePortInfo(OUT_PORT_NAME, PortType.StateTransitionOut), typeof(StateNodePort), null));
        return ports;
    }
}