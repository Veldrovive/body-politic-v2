using System.Collections.Generic;

[NodeInfo("Exit", "Lifecycle/Exit")]
public class ExitNode : StateGraphNode
{
    public static string IN_PORT_NAME = "Exit";
    
    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        ports.Add(new NodePortContext(new NodePortInfo(IN_PORT_NAME, PortType.StateTransitionIn), typeof(StateNodePort), null));
        return ports;
    }
}