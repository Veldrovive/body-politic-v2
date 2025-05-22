using System;
using UnityEngine;

[NodeInfo("Action Camera End State", "Action States/Action Camera End", nodeWidth: 200)]
public class ActionCameraEndStateNode : ConfigurableStateNode<ActionCameraEndStateConfiguration>
{
    public ActionCameraEndStateNode() : base()
    {
    }
    
    public ActionCameraEndStateNode(ActionCameraEndStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(ActionCameraEndStateOutcome);
    public override Type StateType => typeof(ActionCameraEndState);
}