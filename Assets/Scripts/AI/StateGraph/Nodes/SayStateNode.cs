using System;
using UnityEngine;

[NodeInfo("Say State", "States/Say State", nodeWidth: 400)]
public class SayStateNode : ConfigurableStateNode<SayStateConfiguration>
{
    public SayStateNode() : base()
    {
        
    }
    
    public SayStateNode(SayStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(SayStateOutcome);
    public override Type StateType => typeof(SayState);
}