using System.Collections.Generic;
using System.Linq;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.Serialization;

public class AnimateBehaviorParameters : BehaviorParameters
{
    public string AnimationTrigger;
    public float Duration;
    public bool EndAnimationOnFinish;
}

[CreateAssetMenu(fileName = "AnimateBehaviorFactory", menuName = "Body Politic/Interrupt Factories/Animate Behavior Factory")]
public class AnimateBehaviorFactory : InterruptBehaviorFactory<AnimateBehaviorParameters>
{
    [SerializeField] protected BehaviorGraph graph;
    [SerializeField] protected string displayName = "";
    [SerializeField] protected string displayDescription = "";

    public override InterruptBehaviorDefinition GetInterruptDefinition(AnimateBehaviorParameters interruptParameters)
    {
        if (graph == null) return null;

        

        return new InterruptBehaviorDefinition(interruptParameters)
        {
            BehaviorGraph = graph,
            BlackboardData = new Dictionary<string, object>
            { 
                { "Animation Trigger", interruptParameters.AnimationTrigger },
                { "Duration", interruptParameters.Duration },
                { "End On Finish", interruptParameters.EndAnimationOnFinish }
            },

            DisplayName = displayName,
            DisplayDescription = displayDescription
        };
    }
}