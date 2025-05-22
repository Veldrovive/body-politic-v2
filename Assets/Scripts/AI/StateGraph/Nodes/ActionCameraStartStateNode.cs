using System;
using UnityEngine;

[NodeInfo("Action Camera Start State", "Action States/Action Camera Start", nodeWidth: 200)]
public class ActionCameraStartStateNode : ConfigurableStateNode<ActionCameraStartStateConfiguration>
{
    public ActionCameraStartStateNode() : base()
    {
    }
    
    public ActionCameraStartStateNode(ActionCameraStartStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(ActionCameraStartStateOutcome);
    public override Type StateType => typeof(ActionCameraStartState);
}