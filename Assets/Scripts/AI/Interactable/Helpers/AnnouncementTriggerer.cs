using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A interaction reactor that takes a AnnouncementBubbleFloatingUIConfig and triggers it to say something.
/// </summary>
public class AnnouncementTriggerer : AbstractInteractionReactor
{
    [SerializeField] private AnnouncementBubbleFloatingUIManager announcementBubbleManager;
    [SerializeField] private InteractionLifecycleEvent interactionLifecycleTrigger;
    [SerializeField] private InteractionDefinitionSO targetInteractionDefinition;
    [SerializeField] private List<string> toSayList = new List<string>();
    [SerializeField] private float defaultDuration = 3f;
    
    private bool initialized = false;
    
    private void Initialize()
    {
        initialized = false;
        if (announcementBubbleManager == null)
        {
            Debug.LogWarning("AnnouncementTriggerer requires a reference to the AnnouncementBubbleFloatingUIManager.", this);
            return;
        }
        
        if (targetInteractionDefinition == null)
        {
            Debug.LogWarning("AnnouncementTriggerer requires a target interaction definition.", this);
            return;
        }

        if (!HasInteractionInstanceFor(targetInteractionDefinition))
        {
            Debug.LogWarning($"AnnouncementTriggerer requires an interaction instance for {targetInteractionDefinition.name}.", this);
            return;
        }

        SafelyRegisterInteractionLifecycleCallback(
            interactionLifecycleTrigger, targetInteractionDefinition,
            HandleLifecycleEvent
        );
        initialized = true;
    }
    
    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        base.LoadSaveData(data, blankLoad);
        Initialize();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        Initialize();
    }
    
    private void HandleLifecycleEvent(InteractionContext interactionContext)
    {
        if (!initialized)
        {
            Debug.LogWarning("AnnouncementTriggerer is not initialized. Cannot handle lifecycle event.", this);
            return;
        }
        
        // Step 1: Choose a random string from the list
        if (toSayList.Count == 0)
        {
            Debug.LogWarning("AnnouncementTriggerer has no strings to say.", this);
            return;
        }
        int randomIndex = Random.Range(0, toSayList.Count);
        string randomString = toSayList[randomIndex];
        
        // Step 2: Trigger the announcement bubble manager to say the string
        announcementBubbleManager.ShowBubble(randomString, defaultDuration);
    }
}