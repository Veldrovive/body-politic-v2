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
    
    [Header("Shoot Reaction")]
    [SerializeField] private ShootGraphConfiguration shootConfiguration;

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

    public (AbstractGraphFactory reactionGraph, int priority) GetReactionFactory(
        NpcContext reactingNpc,
        NpcContext targetNpc,
        float suspicion
    )
    {
        DetectionReactionDefinition reaction = GetReaction(suspicion);
        if (reaction == null)
        {
            // Then there was not an appropriate reaction defined for this suspicion
            return (null, -1);
        }

        AbstractGraphFactory factory = reaction.ReactionType switch
        {
            DetectionReactionType.Test => GetTestReactionFactory(reactingNpc, targetNpc),
            DetectionReactionType.Curious => GetCuriousReactionFactory(reactingNpc, targetNpc),
            DetectionReactionType.Follow => GetFollowReactionFactory(reactingNpc, targetNpc),
            DetectionReactionType.Shoot => GetShootReactionFactory(reactingNpc, targetNpc),
            _ => throw new ArgumentOutOfRangeException(nameof(reaction.ReactionType), $"Unhandled reaction type: {reaction.ReactionType}")
        };
        
        if (factory == null)
        {
            // There was a problem creating the factory
            return (null, -1);
        }
        
        return (factory, reaction.Priority);
    }

    #region Graph Factories
    
    private AbstractGraphFactory GetTestReactionFactory(NpcContext reactingNpc, NpcContext targetNpc)
    {
        Debug.Log($"Test reaction triggered on {reactingNpc.name} for {targetNpc.name}.");
        MoveGraphFactory factory = new(new MoveGraphConfiguration()
        {
            moveToStateConfig = new MoveToStateConfiguration(targetNpc.transform)
            {
                RequireExactPosition = false,
                AcceptanceRadius = 1f,
            },
            ArrivedMessage = "I guess it's fine...",
            PreStartMessage = "Hey, what are you doing?!",
        });
        return factory;
    }
    
    private AbstractGraphFactory GetCuriousReactionFactory(NpcContext reactingNpc, NpcContext targetNpc)
    {
        LookTowardGraphFactory factory = new(new LookTowardGraphConfiguration()
        {
            TargetingType = FollowStateTargetingType.Transform,
            TargetTransform = new TransformReference(targetNpc.transform),

            SightDistance = 10f,
            MaxDuration = 10f,
            MaxDurationWithoutLoS = 2f,

            EntryMessage = "What was that?",
            EntryMessageDuration = 2f,
            EntryWaitDuration = 0f, // Immediately start looking while talking

            ExitMessage = "Hmm.",
            ExitMessageDuration = 1.5f,
            ExitWaitDuration = 1.5f,
        });
        return factory;
    }
    
    private AbstractGraphFactory GetFollowReactionFactory(NpcContext reactingNpc, NpcContext targetNpc)
    {
        FollowGraphFactory factory = new(new FollowGraphConfiguration()
        {
            TargetingType = FollowStateTargetingType.Transform,
            TargetTransform = new TransformReference(targetNpc.transform),
            FollowDistance = 5f,
            MaxDuration = 15f,
            MaxDurationWithoutLoS = 5f,
            Speed = MovementSpeed.Walk,

            EntryMessage = "Hmm...",
            EntryMessageDuration = 2f,
            EntryWaitDuration = 0f, // Immediately start following while talking

            ExitMessage = "I guess its ok.",
            ExitMessageDuration = 2f,
            ExitWaitDuration = 2f, // Immediately exit the graph
        });
        return factory;
    }
    
    private AbstractGraphFactory GetShootReactionFactory(NpcContext reactingNpc, NpcContext targetNpc)
    {
        shootConfiguration.TargetInteractable = targetNpc.InteractableNpc;
        ShootGraphFactory factory = new(shootConfiguration);
        return factory;
    }

    #endregion
}