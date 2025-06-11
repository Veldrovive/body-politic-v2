using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class SpeechBubbleDefinition
{
    public string ToSay;
    public float Duration;
}


public class SpeechBubbleFloatingUIConfig : AbstractFloatingUIConfig
{
    public SpeechBubbleDefinition bubbleDefinition;
    
    // Internal variables
    public float StartTime = -1;
}

public class SpeechBubbleFloatingUIManager : AbstractFloatingUIManager<SpeechBubbleFloatingUIConfig>
{
    [SerializeField] [Tooltip("The element to position the bubble on.")]
    private Transform trackedTransform;
    
    [SerializeField] [Tooltip("The time it takes to transition in a bubble.")]
    private float transitionInTime = 0.4f;

    [SerializeField] [Tooltip("The time it takes to transition out a bubble.")]
    private float transitionOutTime = 0.2f;
    
    [SerializeField] [Tooltip("Screen space offset for the bubble.")]
    private Vector2 screenSpaceOffset = new Vector2(0, 0);

    [SerializeField] private float maxWidth = 40f;
    
    private FloaterData currentFloaterData;

    public bool ShowBubble(string toSay, float duration, Action onFinishCallback = null)
    {
        if (currentFloaterData != null)
        {
            // New overwrites old. Remove the current floater.
            RemoveFloater(currentFloaterData.Id);
        }

        SpeechBubbleDefinition def = new SpeechBubbleDefinition()
        {
            ToSay = toSay,
            Duration = duration
        };

        SpeechBubbleFloatingUIConfig config = new SpeechBubbleFloatingUIConfig()
        {
            LifetimeOwner = this,

            PositionType = FloatingUIPositionType.Transform,
            TargetTransform = trackedTransform,
            
            Anchor = floatingUIAnchor.BottomRight,
            ScreenSpaceOffset = screenSpaceOffset,
            
            OnRemovalComplete = onFinishCallback,
            
            ContainerMaxWidthPercent = maxWidth,
            
            bubbleDefinition = def
        };

        string floaterId = CreateFloater(config);
        if (floaterId == null)
        {
            // Failed to create floater, log an error
            Debug.LogError("Failed to create Speech Bubble floater.", this);
            return false;
        }
        
        // Otherwise, store the floater data
        currentFloaterData = floaterDatas[floaterId];

        return true;
    }
    
    protected override bool OnSetupFloater(VisualElement floaterRoot, SpeechBubbleFloatingUIConfig floaterConfig)
    {
        // Ensure that nothing here can be interacted with
        floaterRoot.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        
        // We also need to apply this to the parent container
        floaterRoot.parent.pickingMode = PickingMode.Ignore;
        
        // Ensure the element exists and is a Label
        Label bubbleTextLabel = floaterRoot.Q<Label>("SpeechText");
        if (bubbleTextLabel == null)
        {
            Debug.LogError("SpeechText Label element not found in the UI Document.", this);
            return false;
        }
        bubbleTextLabel.text = floaterConfig.bubbleDefinition.ToSay;
        
        floaterConfig.StartTime = Time.time;

        floaterRoot.style.opacity = 0f;  // Start with the bubble invisible

        return true;
    }

    protected override void OnUpdateFloater(VisualElement floaterRoot, SpeechBubbleFloatingUIConfig floaterConfig)
    {
        float timeSinceStart = Time.time - floaterConfig.StartTime;
        float timeTillEnd = floaterConfig.bubbleDefinition.Duration - timeSinceStart;

        if (timeTillEnd > floaterConfig.bubbleDefinition.Duration)
        {
            // Then we can remove the floater
            Debug.Log("Speech bubble duration exceeded, removing floater.");
            RemoveFloater(currentFloaterData.Id);
            return;
        }
        
        // Update the opacity based on the time
        if (timeTillEnd < transitionOutTime)
        {
            float currentOpacity = Mathf.Clamp01(timeTillEnd / transitionOutTime);
            floaterRoot.style.opacity = currentOpacity;
        }
        else if (timeSinceStart < transitionInTime)
        {
            float currentOpacity = Mathf.Clamp01(timeSinceStart / transitionInTime);
            floaterRoot.style.opacity = currentOpacity;
        }
        else
        {
            floaterRoot.style.opacity = 1f;
        }
    }

    protected override void OnRemoveFloater(VisualElement floaterRoot, SpeechBubbleFloatingUIConfig floaterConfig)
    {
        currentFloaterData = null;
    }
}