using System;
using UnityEngine;

[NodeInfo("Hold Item State", "Gate States/Hold Item State", nodeWidth: 300)]
public class HoldItemGateStateNode : ConfigurableStateNode<HoldItemGateStateConfiguration>
{
    public HoldItemGateStateNode() : base()
    {
        
    }
    
    public HoldItemGateStateNode(HoldItemGateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(HoldItemGateStateOutcome);
    public override Type StateType => typeof(HoldItemGateState);
}