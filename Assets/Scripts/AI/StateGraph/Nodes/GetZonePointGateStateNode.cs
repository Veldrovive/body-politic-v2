using System;
using UnityEngine;

[NodeInfo("Get Zone Point State", "Gate States/Get Zone Point State", 400)]
public class GetZonePointGateStateNode : ConfigurableStateNode<GetZonePointGateStateConfiguration>
{
    public GetZonePointGateStateNode() : base()
    {
        
    }
    
    public GetZonePointGateStateNode(GetZonePointGateStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(GetZonePointGateStateOutcome);
    public override Type StateType => typeof(GetZonePointGateState);
}

