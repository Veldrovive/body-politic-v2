using System;
using UnityEngine;

[NodeInfo("Move To State", "States/Move To State", nodeWidth: 400)]
public class MoveToStateNode : ConfigurableStateNode<MoveToStateConfiguration>
{
    public MoveToStateNode() : base()
    {
        
    }
    
    public MoveToStateNode(MoveToStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(MoveToStateOutcome);
    public override Type StateType => typeof(MoveToState);
}