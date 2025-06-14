using System;
using UnityEngine;

[NodeInfo("Time Check State", "Gate States/Time Check State")]
public class TimeCheckGateStateNode : ConfigurableStateNode<TimeCheckGateStateConfiguration>
{
    public TimeCheckGateStateNode() : base()
    {
        
    }
    
    public TimeCheckGateStateNode(TimeCheckGateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(TimeCheckGateStateOutcome);
    public override Type StateType => typeof(TimeCheckGateState);
}