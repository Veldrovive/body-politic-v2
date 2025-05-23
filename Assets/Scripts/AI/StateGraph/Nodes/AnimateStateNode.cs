using System;
using UnityEngine;

[NodeInfo("Animate State", "States/Animate State")]
public class AnimateStateNode : ConfigurableStateNode<AnimateStateConfiguration>
{
    public AnimateStateNode() : base()
    {
        
    }
    
    public AnimateStateNode(AnimateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(AnimateStateOutcome);
    public override Type StateType => typeof(AnimateState);
}