using System;
using UnityEngine;

[NodeInfo("Find Control Trigger State", "Gate States/Find Control Trigger State", nodeWidth: 400)]
public class FindControlTriggerGateStateNode : ConfigurableStateNode<FindControlTriggerGateStateConfiguration>
{
    public FindControlTriggerGateStateNode() : base()
    {
        
    }
    
    public FindControlTriggerGateStateNode(FindControlTriggerGateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(FindControlTriggerGateStateOutcome);
    public override Type StateType => typeof(FindControlTriggerGateState);
}