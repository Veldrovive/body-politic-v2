using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundReactionType
{
    Test,  // Triggers a visible reaction
    LookAtEmanationPoint, // NPC will look at the point where the sound was emitted
    InspectEmanationPoint, // NPC will move to the point where the sound was emitted
}

[Serializable]
public class SoundReactionDefinition
{
    public string name;  // Used in the editor to identify the reaction
    
    [Tooltip("The minimum suspicion level that will trigger this reaction.")]
    public int MinSuspicion;

    [Tooltip("The priority of the graph for StateController interruption. Priority should be 1.")]
    public int Priority = 1;

    [Tooltip("The reaction type to trigger once past the suspicion threshold.")]
    public SoundReactionType ReactionType;

    [Tooltip("The sound type that this reaction applies to.")]
    public SoundType SoundTypeWhitelist = SoundType.Default;

    public bool CanInterruptSelf = true; // If false, if this reaction is already running it will not try to interrupt itself.

    public SoundReactionDefinition()
    {
        MinSuspicion = 1;
        Priority = 1;
        ReactionType = SoundReactionType.LookAtEmanationPoint;
        SoundTypeWhitelist = SoundType.Default;
        CanInterruptSelf = true;
    }
}

[Serializable]
public class SoundSensitivity
{
    [Tooltip("The maximum distance at which the NPC can hear sounds with line of sight.")]
    public float LoSMaxDistance;
    [Tooltip("The maximum distance at which the NPC can hear sounds without line of sight.")]
    public float NoLoSMaxDistance;
}

[CreateAssetMenu(fileName = "NpcSoundReactionSO", menuName = "Body Politic/Npc Sound Reaction SO")]
public class NpcSoundReactionDefinitionSO : ScriptableObject
{
    [Header("Sound Sensitivity")]
    [SerializeField] public SoundSensitivity QuiteSoundSensitivity;
    [SerializeField] public SoundSensitivity NormalSoundSensitivity;
    [SerializeField] public SoundSensitivity LoudSoundSensitivity;
    
    [Header("Sound Volume (For Player)")]
    [SerializeField] public float QuietSoundVolume = 0.5f;
    [SerializeField] public float NormalSoundVolume = 1f;
    [SerializeField] public float LoudSoundVolume = 1.5f;
    
    [Header("Sound Reactions")]
    [SerializeField] public List<SoundReactionDefinition> Reactions;

    [Header("Configration")] [SerializeField]
    public LayerMask LoSObstacleLayerMask;
    
    [Header("Inspect Reaction Configuration")]
    [SerializeField] private float inspectDistance = 2f;  // The distance at which the NPC will inspect the sound emanation point
    [SerializeField] private float inspectMaxDuration = 20f; // The maximum duration for which the NPC will inspect the sound emanation point
    [SerializeField] private float inspectMaxDurationWithoutLoS = 20f; // The maximum duration for which the NPC will inspect the sound emanation point without line of sight
    [SerializeField] private MovementSpeed inspectMovementSpeed = MovementSpeed.Run; // The movement speed at which the NPC will inspect the sound emanation point
    [SerializeField] private string inspectEntryMessage = "Huh?"; // The message that the NPC will say when it starts inspecting the sound emanation point
    [SerializeField] private float inspectEntryMessageDuration = 1f; // The duration for which the NPC will say the entry message

    private void AutoSet()
    {
        if (LoSObstacleLayerMask == 0)
        {
            LoSObstacleLayerMask = LayerMask.GetMask("Default");
        }
    }

    private void Reset()
    {
        AutoSet();
    }
    
    private void OnValidate()
    {
        AutoSet();
    }

    public float GetSoundVolume(SoundData soundData)
    {
        return soundData.Loudness switch
        {
            SoundLoudness.Quiet => QuietSoundVolume,
            SoundLoudness.Normal => NormalSoundVolume,
            SoundLoudness.Loud => LoudSoundVolume,
            _ => QuietSoundVolume
        };
    }
    
    public bool CanHearSound(SoundData soundData, Vector3 reactorPoint)
    {
        Vector3 emanationPoint = soundData.EmanationPoint;
        // We assume that LoSMaxDistance is always greater than NoLoSMaxDistance. So we can check LoSMaxDistance first
        // and avoid computing the linecast if not necessary.
        float withLoSMaxDistance = soundData.Loudness switch
        {
            SoundLoudness.Quiet => QuiteSoundSensitivity.LoSMaxDistance,
            SoundLoudness.Normal => NormalSoundSensitivity.LoSMaxDistance,
            SoundLoudness.Loud => LoudSoundSensitivity.LoSMaxDistance,
            _ => QuiteSoundSensitivity.LoSMaxDistance
        };
        
        if ((emanationPoint - reactorPoint).sqrMagnitude > withLoSMaxDistance * withLoSMaxDistance)
        {
            // The sounds is too far away to hear with line of sight so we do not need to check LoS
            return false;
        }
        
        // Similarly, if the emanation point is closer than the NoLoSMaxDistance, we can know that the NPC can hear the sound
        // without checking line of sight.
        float noLoSMaxDistance = soundData.Loudness switch
        {
            SoundLoudness.Quiet => QuiteSoundSensitivity.NoLoSMaxDistance,
            SoundLoudness.Normal => NormalSoundSensitivity.NoLoSMaxDistance,
            SoundLoudness.Loud => LoudSoundSensitivity.NoLoSMaxDistance,
            _ => QuiteSoundSensitivity.NoLoSMaxDistance
        };
        
        if ((emanationPoint - reactorPoint).sqrMagnitude <= noLoSMaxDistance * noLoSMaxDistance)
        {
            // The sound is close enough to hear without line of sight
            return true;
        }
        
        // Otherwise we need to check if there is LoS. If there is, the NPC can hear the sound.
        RaycastHit hit;
        if (Physics.Linecast(reactorPoint, emanationPoint, out hit, LoSObstacleLayerMask))
        {
            // There is an obstacle in the way, so the NPC cannot hear the sound
            return false;
        }
        
        // No obstacle in the way, so the NPC can hear the sound
        return true;
    }
    
    /// <summary>
    /// Finds the reaction definition that has the maximum minimum suspicion that is less than or equal to the given suspicion.
    /// </summary>
    /// <param name="suspicion"></param>
    /// <returns></returns>
    private SoundReactionDefinition GetReaction(int suspicion, SoundType soundType)
    {
        float maxMinSuspicion = 0;
        SoundReactionDefinition bestReaction = null;
        foreach (var reaction in Reactions)
        {
            // Check if the sound type is whitelisted for this reaction
            // soundType is a bitwise flag, so we can use bitwise AND to check if the sound type is included
            if ((reaction.SoundTypeWhitelist & soundType) == 0)
            {
                // The sound type is not whitelisted for this reaction
                continue;
            }
            
            if (reaction.MinSuspicion <= suspicion && reaction.MinSuspicion > maxMinSuspicion)
            {
                maxMinSuspicion = reaction.MinSuspicion;
                bestReaction = reaction;
            }
        }
        return bestReaction;
    }

    public (AbstractGraphFactory reactionGraph, int priority) GetReactionFactory(NpcContext reactingNpc, SoundData soundData)
    {
        SoundReactionDefinition reaction = GetReaction(soundData.Suspiciousness, soundData.SType);
        if (reaction == null)
        {
            // Then there was not an appropriate reaction defined for this sound
            return (null, -1);
        }

        
        AbstractGraphFactory factory = reaction.ReactionType switch 
        {
            SoundReactionType.Test => GetTestReactionFactory(reactingNpc, soundData),
            SoundReactionType.LookAtEmanationPoint => GetLookAtEmanationPointFactory(reactingNpc, soundData),
            SoundReactionType.InspectEmanationPoint => GetInspectEmanationPointFactory(reactingNpc, soundData),
            _ => throw new ArgumentOutOfRangeException(nameof(reaction.ReactionType), $"Unhandled reaction type: {reaction.ReactionType}")
        };
        if (factory == null)
        {
            // There was a problem creating the factory
            return (null, -1);
        }

        if (reaction.CanInterruptSelf)
        {
            // Then we include the current time in the graph ID to ensure that it is unique
            // Non-unique graph IDs will cause the SoundHandler to not interrupt the current graph
            factory.SetGraphId($"{reaction.ReactionType.ToString()}-{SaveableDataManager.Instance.time}");
        }
        else
        {
            // Then we just use the reaction type as the graph ID so that we do not interrupt the current reaction
            // with a new one of the same type.
            factory.SetGraphId(reaction.ReactionType.ToString());
        }
        
        return (factory, reaction.Priority);
    }

    #region Factory Helpers

    private AbstractGraphFactory GetTestReactionFactory(NpcContext reactingNpc, SoundData soundData)
    {
        Debug.LogWarning("Test reaction is not implemented yet. This is a placeholder for testing purposes.", reactingNpc.gameObject);
        return null;
    }
    
    private AbstractGraphFactory GetLookAtEmanationPointFactory(NpcContext reactingNpc, SoundData soundData)
    {
        return new LookTowardGraphFactory(new LookTowardGraphConfiguration()
        {
            TargetingType = FollowStateTargetingType.Position,
            TargetPosition = new(soundData.EmanationPoint),

            SightDistance = 10f,
            MaxDuration = 5f,
            MaxDurationWithoutLoS = 2f,

            EntryMessage = "Huh?",
            EntryMessageDuration = 1f,
        });
    }
    
    private AbstractGraphFactory GetInspectEmanationPointFactory(NpcContext reactingNpc, SoundData soundData)
    {
        var config = new FollowGraphConfiguration()
        {
            TargetingType = FollowStateTargetingType.Position,
            TargetPosition = new(soundData.EmanationPoint),
            FollowDistance = inspectDistance,
            MaxDuration = inspectMaxDuration,
            MaxDurationWithoutLoS = inspectMaxDurationWithoutLoS,
            Speed = inspectMovementSpeed,
            EntryMessage = inspectEntryMessage,
            EntryMessageDuration = inspectEntryMessageDuration,
            EntryWaitDuration = inspectEntryMessageDuration
        };
        return new FollowGraphFactory(config);
    }

    #endregion
}