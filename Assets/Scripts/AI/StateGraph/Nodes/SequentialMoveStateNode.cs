using System;
using UnityEngine;

[NodeInfo("Sequential Move State", "States/Sequential Move State", nodeWidth: 400)]
public class SequentialMoveStateNode : ConfigurableStateNode<SequentialMoveStateConfiguration>
{
    public SequentialMoveStateNode() : base()
    {
        
    }
    
    public SequentialMoveStateNode(SequentialMoveStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(SequentialMoveStateOutcome);
    public override Type StateType => typeof(SequentialMoveState);
}