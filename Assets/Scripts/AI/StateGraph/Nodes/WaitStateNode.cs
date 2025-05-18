using System;
using UnityEngine;

[NodeInfo("Wait State", "States/Wait State")]
public class WaitStateNode : ConfigurableStateNode<WaitStateConfiguration>
{
    public WaitStateNode() : base()
    {
        
    }
    
    public WaitStateNode(WaitStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(WaitStateOutcome);
    public override Type StateType => typeof(WaitState);
}

