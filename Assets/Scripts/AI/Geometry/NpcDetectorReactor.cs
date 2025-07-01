using System;
using UnityEngine;
using System.Collections.Generic;



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
    [SerializeField] private NpcDetectorReactionDefinitionSO reactionDefinition;

    private class ReactionStateBehaviorContext
    {
        public string AgentId;
        public float CausalSuspicion;  // The suspicion level that caused this reaction
    }
    private ReactionStateBehaviorContext _currentReactionStateBehaviorContext = null;

    private bool IsStillQueued(ReactionStateBehaviorContext context)
    {
        // Checks if this graph is still queued in the Controller
        return ownNpcContext.BehaviorController.HasBehaviorInQueue(context.AgentId);
    }
    
    private (float maxSuspicion, NpcContext mostSuspiciousNpcContext) GetMaxSuspicion()
    {
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
        return (maxSuspicion, mostSuspiciousNpcContext);
    }

    private InterruptBehaviorDefinition GetBehaviorFactory(NpcContext targetNpc, float suspicion)
    {
        if (reactionDefinition == null)
        {
            return null;
        }

        return reactionDefinition.GetBehaviorFactory(ownNpcContext, targetNpc, suspicion);
    }

    protected override void Update()
    {
        base.Update();
        
        // Check if the current reaction state has exited
        if (_currentReactionStateBehaviorContext != null && !IsStillQueued(_currentReactionStateBehaviorContext))
        {
            // The current reaction state has exited, so we reset it
            _currentReactionStateBehaviorContext = null;
        }
        
        var (maxSuspicion, mostSuspiciousNpcContext) = GetMaxSuspicion();
        if (
            Mathf.Approximately(maxSuspicion, 0) ||
            (_currentReactionStateBehaviorContext != null && maxSuspicion <= _currentReactionStateBehaviorContext.CausalSuspicion)
        )
        {
            // Then the suspicion level has not risen enough to trigger a new reaction,
            return;
        }
        
        // If we have gotten to this point, then we should trigger a reaction
        var behaviorFactory = GetBehaviorFactory(mostSuspiciousNpcContext, maxSuspicion);
        behaviorFactory.Id = $"{ownNpcContext.name}_{mostSuspiciousNpcContext.name}_{maxSuspicion}";
        bool reactionStarted = ownNpcContext.BehaviorController.TryInterrupt(behaviorFactory);
        
        if (reactionStarted)
        {
            // We have successfully started a reaction, so we write the new reaction state context
            _currentReactionStateBehaviorContext = new ReactionStateBehaviorContext()
            {
                AgentId = behaviorFactory.Id,
                CausalSuspicion = maxSuspicion,
            };
        }
        else
        {
            // The reaction was not started, so we log a warning
            Debug.LogWarning($"NpcDetectorReactor: Could not start reaction for {ownNpcContext.name} with suspicion {maxSuspicion}.", this);
        }
    }

    private NpcContext ownNpcContext;
    
    
    private void Awake()
    {   
        ownNpcContext = GetComponent<NpcContext>();
    }
    
}
