using System;
using UnityEngine;

[NodeInfo("Bool Select State", "Gate States/Bool Select State")]
public class BoolSelectGateStateNode : ConfigurableStateNode<BoolSelectGateStateConfiguration>
{
    public BoolSelectGateStateNode() : base()
    {
        
    }
    
    public BoolSelectGateStateNode(BoolSelectGateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(BoolSelectGateStateOutcome);
    public override Type StateType => typeof(BoolSelectGateState);
}