using System;
using System.Collections.Generic;
using UnityEngine;

public enum DetectionReactionType
{
    Test,  // Triggers a visible reaction
    Shoot,
    Curious,
    Follow,
    RatOut, // Triggers RatOutState
    Panic, // Triggers PanicState
}

[Serializable]
class DetectionReactionDefinition
{
    [Tooltip("The minimum suspicion level that will trigger this reaction.")]
    public float MinSuspicion;
    
    [Tooltip("The priority of the graph for StateController interruption. Priority should be 2.")]
    public int Priority = 2;

    [Tooltip("The reaction type to trigger once past the suspicion threshold.")]
    public DetectionReactionType ReactionType;
    
    public DetectionReactionDefinition()
    {
        MinSuspicion = 1f;
        Priority = 2;
        ReactionType = DetectionReactionType.Curious;
    }
}

[CreateAssetMenu(fileName = "NpcDetectorReactionSO", menuName = "Body Politic/Npc Detector Reaction SO")]
public class NpcDetectorReactionDefinitionSO : ScriptableObject
{
    [SerializeField] [Tooltip("The detection reaction definitions.")]
    private List<DetectionReactionDefinition> reactionDefinitions;

    [Header("Curious Reaction")]
    [SerializeField] private CuriousBehaviorFactory curiousBehaviorFactory;
    
    [Header("Follow Reaction")]
    [SerializeField] private FollowBehaviorFactory followBehaviorFactory;
    
    [Header("Shoot Reaction")] 
    [SerializeField] private ShootBehaviorFactory shootBehaviorFactory;

    [Header("Panic Reaction")]
    [SerializeField] private PanicBehaviorFactory panicBehaviorFactory;

    /// <summary>
    /// Finds the reaction definition that has the maximum minimum suspicion that is less than or equal to the given suspicion.
    /// </summary>
    /// <param name="suspicion"></param>
    /// <returns></returns>
    private DetectionReactionDefinition GetReaction(float suspicion)
    {
        DetectionReactionDefinition highestReaction = null;
        foreach (DetectionReactionDefinition reaction in reactionDefinitions)
        {
            if (reaction.MinSuspicion <= suspicion)
            {
                if (highestReaction == null || reaction.MinSuspicion > highestReaction.MinSuspicion)
                {
                    highestReaction = reaction;
                }
            }
        }
        return highestReaction;
    }

    public InterruptBehaviorDefinition GetBehaviorFactory(NpcContext reactingNpc, NpcContext targetNpc, float suspicion)
    {
        DetectionReactionDefinition reaction = GetReaction(suspicion);
        if (reaction == null)
        {
            // Then there was not an appropriate reaction defined for this suspicion
            return null;
        }

        AbstractCustomActionBehaviorFactory behaviorFactory = reaction.ReactionType switch
        {
            DetectionReactionType.Test => null,
            DetectionReactionType.Curious => curiousBehaviorFactory,
            DetectionReactionType.Shoot => shootBehaviorFactory,
            DetectionReactionType.Panic => panicBehaviorFactory,
            DetectionReactionType.Follow => followBehaviorFactory,
            DetectionReactionType.RatOut => null,
            _ => throw new ArgumentOutOfRangeException(nameof(reaction.ReactionType),
                $"Unhandled reaction type: {reaction.ReactionType}")
        };
        if (behaviorFactory == null)
        {
            throw new NotSupportedException($"Behavior factory for reaction type {reaction.ReactionType} is not supported.");
        }
        
        CustomActionBehaviorParameters parameters = new CustomActionBehaviorParameters()
        {
            InitiatorGO = reactingNpc.gameObject,
            TargetGO = targetNpc.gameObject,
            TargetType = CustomActionTargetType.GameObject
        };
        InterruptBehaviorDefinition behaviorDefinition = behaviorFactory.GetInterruptDefinition(parameters);
        if (behaviorDefinition == null)
        {
            // There was a problem creating the behavior definition
            return null;
        }
        behaviorDefinition.Priority = reaction.Priority;
        behaviorDefinition.Id = $"{reactingNpc.name}_{targetNpc.name}_{reaction.ReactionType}";
        
        return behaviorDefinition;
    }
}