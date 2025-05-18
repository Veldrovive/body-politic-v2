using System.Collections.Generic;

[NodeInfo("Restart", "Lifecycle/Restart")]
public class RestartNode : StateGraphNode
{
    public static string IN_PORT_NAME = "Restart";
    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        ports.Add(new NodePortContext(new NodePortInfo(IN_PORT_NAME, PortType.StateTransitionIn), typeof(StateNodePort), null));
        return ports;
    }
}