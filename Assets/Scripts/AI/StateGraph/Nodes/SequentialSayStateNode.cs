using System;
using UnityEngine;

[NodeInfo("Sequential Say State", "States/Sequential Say State", nodeWidth: 400)]
public class SequentialSayStateNode : ConfigurableStateNode<SequentialSayStateConfiguration>
{
    public SequentialSayStateNode() : base()
    {
        
    }
    
    public SequentialSayStateNode(SequentialSayStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(SequentialSayStateOutcome);
    public override Type StateType => typeof(SequentialSayState);
}