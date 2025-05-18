using System;
using UnityEngine;

[NodeInfo("Interaction State", "States/Interaction State", nodeWidth: 400)]
public class InteractionStateNode : ConfigurableStateNode<InteractionStateConfiguration>
{
    public InteractionStateNode() : base()
    {
        
    }
    
    public InteractionStateNode(InteractionStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(InteractionStateOutcome);
    public override Type StateType => typeof(InteractionState);
}