using System;
using UnityEngine;

/// Handles the reaction of NPCs to sounds and emitting of sound events. Must be attached to an NPC GameObject.

[RequireComponent(typeof(NpcContext))]
public class NpcSoundHandler : GameEventListenerBase<SoundData, SoundEventSO>
{
    [SerializeField] private NpcSoundReactionDefinitionSO soundReactionDefinition;
    
    private NpcContext npcContext;

    private void AutoSet()
    {
        if (gameEvent == null)
        {
            gameEvent = GlobalData.Instance?.SoundEvent;
        }
    }

    private void OnValidate()
    {
        AutoSet();
    }

    private void Reset()
    {
        AutoSet();
    }

    private void Awake()
    {
        npcContext = GetComponent<NpcContext>();

        AutoSet();
    }

    protected override void HandleEventRaised(SoundData data)
    {
        if (soundReactionDefinition == null)
        {
            Debug.LogWarning("NpcSoundHandler: sensitivityDefinition is not set. Cannot process sound data.", this);
            return;
        }

        bool CanHearSound = soundReactionDefinition.CanHearSound(data, gameObject.transform.position);
        if (!CanHearSound)
        {
            // This NPC cannot hear the sound, so we do not process it.
            return;
        }
        
        // At this point we have decided that the NPC can hear the sound.
        if (PlayerManager.Instance.CurrentFocusedNpc == npcContext && data.Clip != null)
        {
            // This is the focused NPC. We should actually play the sound clip.
            AudioSource.PlayClipAtPoint(data.Clip, data.EmanationPoint, soundReactionDefinition.GetSoundVolume(data));
        }

        if (data.CausesReactions && data.CreatorObject != gameObject)
        {
            // Then we should also trigger a reaction
            var (reactionFactory, interruptPriority) = soundReactionDefinition.GetReactionFactory(npcContext, data);
            if (reactionFactory != null)
            {
                // Check we if we already executing this reaction
                if (npcContext.StateGraphController.CurrentStateGraph?.id != reactionFactory.AbstractConfig.GraphId)
                {
                    npcContext.StateGraphController.TryInterrupt(reactionFactory, false, false, interruptPriority);
                    // We don't care if the interrupt fails as sounds are one-time things and not critical to the NPC's state.
                }
            }
            // else: There was no reaction defined for this sound, so we do nothing.
        }
    }

    public void RaiseSoundEvent(SoundData data)
    {
        gameEvent?.Raise(data);
    }
}