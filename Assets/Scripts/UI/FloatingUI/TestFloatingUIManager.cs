using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A simple, serializable configuration class for our test floater.
/// In a more complex scenario, this could hold additional data like text,
/// icon sprites, colors, etc., specific to this type of floater.
/// </summary>
[System.Serializable]
public class TestFloatingUIConfig : AbstractFloatingUIConfig
{
    // No additional properties are needed for this basic test,
    // but you would add them here. For example:
    // public string Title;
    // public string Description;
}


/// <summary>
/// A concrete implementation of the AbstractFloatingUIManager.
/// This manager is designed to create a single, configurable floater on Start
/// and manage its lifecycle. The floater's properties are set via the Inspector.
/// </summary>
public class TestFloatingUIManager : AbstractFloatingUIManager<TestFloatingUIConfig>
{
    [Header("Default Floater Configuration")]
    [Tooltip("The type of positioning to use for the default floater.")]
    [SerializeField] private FloatingUIPositionType positionType = FloatingUIPositionType.Transform;

    [Tooltip("The Transform to follow. Used if Position Type is 'Transform'.")]
    [SerializeField] private Transform targetTransform;
    
    [Tooltip("The World Position to follow. Used if Position Type is 'WorldPosition'.")]
    [SerializeField] private Vector3 targetWorldPosition;
    
    [SerializeField] private ScreenPositionType screenPositionType = ScreenPositionType.Pixel;

    [Tooltip("The Screen Position to use. Used if Position Type is 'ScreenPosition'.")]
    [SerializeField] private Vector2 targetScreenPosition;

    [Tooltip("The anchor point on the floater that will align with the target position.")]
    [SerializeField] private floatingUIAnchor anchor = floatingUIAnchor.BottomLeft;

    [Tooltip("An additional pixel offset applied after anchoring.")]
    [SerializeField] private Vector2 screenSpaceOffset = new Vector2(10, 10);
    
    [Tooltip("If true, the floater will be clamped to stay within the screen boundaries.")]
    [SerializeField] private bool keepOnScreen = true;

    private int updateCounter = 0;

    /// <summary>
    /// On Start, we create a single floater instance using the configuration
    /// specified in the Inspector.
    /// </summary>
    void Start()
    {
        // If no target transform is assigned for transform-based positioning,
        // default to this object's transform.
        if (positionType == FloatingUIPositionType.Transform && targetTransform == null)
        {
            Debug.Log("No Target Transform assigned, defaulting to self.", this);
            targetTransform = this.transform;
        }

        // Create a configuration object from our serialized fields.
        var defaultConfig = new TestFloatingUIConfig
        {
            // --- Lifetime ---
            // Tie the floater's lifetime to this GameObject. When this object
            // is destroyed, the floater will be automatically removed.
            LifetimeOwner = this.gameObject,

            // --- Positioning ---
            PositionType = this.positionType,
            TargetTransform = this.targetTransform,
            TargetWorldPosition = this.targetWorldPosition,
            TargetScreenPositionType = this.screenPositionType,
            TargetScreenPosition = this.targetScreenPosition,
            Anchor = this.anchor,
            ScreenSpaceOffset = this.screenSpaceOffset,
            KeepOnScreen = this.keepOnScreen,
            
            // --- Events (Optional) ---
            OnCreationComplete = () => Debug.Log("Default floater has been created!"),
            OnRemovalComplete = () => Debug.Log("Default floater has been removed.")
        };

        // Create the floater using the base class method.
        CreateFloater(defaultConfig);
    }

    private void Update()
    {
        // Do some surgery on the floater configuration to update the values
        FloaterData data = floaterDatas.Values.First();
        data.Config.PositionType = positionType;
        data.Config.TargetTransform = targetTransform;
        data.Config.TargetWorldPosition = targetWorldPosition;
        data.Config.TargetScreenPositionType = screenPositionType;
        data.Config.TargetScreenPosition = targetScreenPosition;
        data.Config.Anchor = anchor;
        data.Config.ScreenSpaceOffset = screenSpaceOffset;
        data.Config.KeepOnScreen = keepOnScreen;
    }

    /// <summary>
    /// Called once when the floater is first created. Use this to find elements
    /// in the UXML and set their initial state.
    /// </summary>
    protected override bool OnSetupFloater(VisualElement floaterRoot, TestFloatingUIConfig floaterConfig)
    {
        Debug.Log("OnSetupFloater called.");
        var titleLabel = floaterRoot.Q<Label>("title-label");
        if (titleLabel != null)
        {
            titleLabel.text = "Tracking Target";
        }

        return true;
    }

    /// <summary>
    /// Called every LateUpdate. Use this to update the content of the floater,
    /// for example, changing text or progress bars.
    /// </summary>
    protected override void OnUpdateFloater(VisualElement floaterRoot, TestFloatingUIConfig floaterConfig)
    {
        // This is just for demonstration to show the update is working.
        var updateLabel = floaterRoot.Q<Label>("update-label");
        if (updateLabel != null)
        {
            updateLabel.text = $"Update Frame: {updateCounter++}";
        }
    }

    /// <summary>
    /// Called when the floater is about to be removed. Use this to perform any
    /// cleanup, like unsubscribing from events.
    /// </summary>
    protected override void OnRemoveFloater(VisualElement floaterRoot, TestFloatingUIConfig floaterConfig)
    {
        Debug.Log("OnRemoveFloater called. Cleaning up test floater.");
        // No special cleanup needed for this simple example.
    }
}