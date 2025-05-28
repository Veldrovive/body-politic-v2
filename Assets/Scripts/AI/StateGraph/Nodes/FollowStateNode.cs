using System;

[NodeInfo("Follow State", "States/Follow State", 400)]
public class FollowStateNode : ConfigurableStateNode<FollowStateConfiguration>
{
    public FollowStateNode() : base()
    {
        
    }
    
    public FollowStateNode(FollowStateConfiguration config) : base(config)
    {
        
    }
    
    public override Type OutcomeEnumType => typeof(FollowStateOutcome);
    public override Type StateType => typeof(FollowState);
}