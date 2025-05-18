using System.Collections.Generic;

[NodeInfo("Start", "Lifecycle/Start")]
public class StartNode : StateGraphNode
{
    public static string OUT_PORT_NAME = "Start";
    
    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        ports.Add(new NodePortContext(new NodePortInfo(OUT_PORT_NAME, PortType.StateTransitionOut), typeof(StateNodePort), -3));
        return ports;
    }
}