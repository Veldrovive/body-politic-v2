using System;
using UnityEngine;
using System.Collections.Generic;

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

/// Checks VisibleNpcs every frame.
/// If suspicion level changes for any NPC (this includes entering with suspicion), checks against the reaction
/// definitions. If min suspicion is surpassed for any, interrupt with the defined reaction type of the one that
/// requires the maximum suspicion.
/// This could be done using the events system that suspicion tracker exposes, but that seems not worth it for now.
///
/// In Update:
/// Run base first.
/// Iterate through VisibleNpcs. Track the highest suspicion seen.
/// Find the highest min suspicion that is <= than actual highest suspicion seen
/// If one exists, call the handler for that reaction

[RequireComponent(typeof(NpcContext))]
public class NpcDetectorReactor : LoSNpcDetector
{
    [SerializeField] [Tooltip("The detection reaction definitions.")]
    private List<DetectionReactionDefinition> reactionDefinitions;
    private List<DetectionReactionDefinition> sortedReactionDefinitions;

    private NpcContext ownNpcContext;
    private float lastMaxSuspicion = 0;

    [Header("Shoot Reaction")]
    [SerializeField] private ShootGraphConfiguration shootConfiguration;

    protected override void InitializeFields()
    {
        base.InitializeFields();
        
        if (reactionDefinitions == null || reactionDefinitions.Count == 0)
        {
            reactionDefinitions = new List<DetectionReactionDefinition>()
            {
                new DetectionReactionDefinition()
            };
        }
    }
    
    void Awake()
    {   
        ownNpcContext = GetComponent<NpcContext>();
        // We sort in descending order to make it easier to find the highest min suspicion
        sortedReactionDefinitions = new List<DetectionReactionDefinition>(reactionDefinitions);
        sortedReactionDefinitions.Sort((a, b) => b.MinSuspicion.CompareTo(a.MinSuspicion));
    }
    
    protected override void Update()
    {
        base.Update();

        float maxSuspicion = 0;
        NpcContext mostSuspiciousNpcContext = null;
        foreach (NpcContext npcContext in VisibleNpcs)
        {
            if (npcContext.SuspicionTracker.CurrentSuspicionLevel > maxSuspicion)
            {
                maxSuspicion = npcContext.SuspicionTracker.CurrentSuspicionLevel;
                mostSuspiciousNpcContext = npcContext;
            }
        }

        if (Mathf.Approximately(maxSuspicion, lastMaxSuspicion))
        {
            // No change in suspicion level
            return;
        }

        lastMaxSuspicion = maxSuspicion;
        
        // Find the highest min suspicion that is <= than actual highest suspicion seen
        DetectionReactionDefinition highestReaction = null;
        int priority = 0;
        foreach (DetectionReactionDefinition reaction in sortedReactionDefinitions)
        {
            if (reaction.MinSuspicion <= maxSuspicion)
            {
                highestReaction = reaction;
                priority = reaction.Priority;
                break;
            }
        }
        
        // If one exists, call the handler for that reaction
        if (highestReaction != null)
        {
            switch (highestReaction.ReactionType)
            {
                case DetectionReactionType.Test:
                    // Call the test reaction handler
                    HandleTestReaction(mostSuspiciousNpcContext, priority);
                    break;
                case DetectionReactionType.Curious:
                    HandleCuriousReaction(mostSuspiciousNpcContext, priority);
                    break;
                case DetectionReactionType.Follow:
                    HandleFollowReaction(mostSuspiciousNpcContext, priority);
                    break;
                case DetectionReactionType.Shoot:
                    HandleShootReaction(mostSuspiciousNpcContext, priority);
                    break;
                default:
                    Debug.LogError($"Unhandled reaction type: {highestReaction.ReactionType}");
                    break;
            }
        }
    }

    #region Helpers

    private void TriggerOverride(AbstractGraphFactory factory, int priority)
    {
        // ownNpcContext.StateGraphController.EnqueueStateGraph(
        //     factory, true, false, false, priority
        // );
        ownNpcContext.StateGraphController.TryInterrupt(factory, false, false, priority);
    }

    #endregion

    #region Reaction Handlers

    private void HandleTestReaction(NpcContext targetNpc, int priority)
    {
        Debug.Log($"Test reaction triggered on {gameObject.name} for {targetNpc.name}.");
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
        TriggerOverride(factory, priority);
    }

    private void HandleCuriousReaction(NpcContext targetNpc, int priority)
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
        TriggerOverride(factory, priority);
    }
    
    private void HandleFollowReaction(NpcContext targetNpc, int priority)
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
        TriggerOverride(factory, priority);
    }
    
    private void HandleShootReaction(NpcContext targetNpc, int priority)
    {
        shootConfiguration.TargetInteractable = targetNpc.InteractableNpc;
        ShootGraphFactory factory = new(shootConfiguration);
        TriggerOverride(factory, priority);
    }
    
    #endregion
}
