using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

public enum floatingUIAnchor
{
    // Corners
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    // Edges
    TopCenter,
    BottomCenter,
    MiddleLeft,
    MiddleRight,

    // Center
    Center
}

public enum ScreenPositionType
{
    Pixel,  // Screen position in pixels
    Normalized  // Screen position in normalized coordinates (0 to 1)
}

public enum FloatingUIPositionType
{
    Transform,
    WorldPosition,
    ScreenPosition
}

[Serializable]
public abstract class AbstractFloatingUIConfig
{
    [Header("Lifetime")]
    public UnityEngine.Object LifetimeOwner;  // The owner of the floater, used to determine when to destroy it
    
    [Header("Floater Positioning")]
    public FloatingUIPositionType PositionType = FloatingUIPositionType.Transform;
    // One of the following 3 will be selected based on positionType
    public Transform TargetTransform;
    public Vector3 TargetWorldPosition;
    public ScreenPositionType TargetScreenPositionType = ScreenPositionType.Pixel;
    public Vector2 TargetScreenPosition;
    
    public floatingUIAnchor Anchor = floatingUIAnchor.TopLeft;
    public Vector2 ScreenSpaceOffset = Vector2.zero;
    
    public bool KeepOnScreen = false;
    
    [Header("Events")]
    public Action OnCreationComplete;
    public Action OnRemovalComplete;

    [Header("Styling")]
    public float? ContainerMaxWidthPercent = 40f;  // Maximum width of the floater container as a fraction of the screen width
    public float? ContainerMinWidthPercent = null;  // Minimum width of the floater container as a fraction of the screen width
}

[RequireComponent(typeof(UIDocument))]
public abstract class AbstractFloatingUIManager<TConfig> : MonoBehaviour
    where TConfig : AbstractFloatingUIConfig
{
    [SerializeField] protected VisualTreeAsset floaterTemplate;
    [SerializeField] protected UIDocument uiDocument;
    [SerializeField] protected Camera viewCamera;
    
    protected class FloaterData
    {
        public bool IsInitialized = false;  // Indicates if the floater has been initialized and geometry is ready
        public bool IsDestroyed = false;
        public string Id;  // Unique identifier for the floater
        public VisualElement Container;  // Reference to the absolutely positioned container of the floater
        public VisualElement Instance;  // Reference to the root of the floater template instance
        public TConfig Config;
    }
    protected Dictionary<string, FloaterData> floaterDatas = new Dictionary<string, FloaterData>();
    
    private IPanel cachedPanel;  // Cached panel reference for efficiency
    
    protected abstract bool OnSetupFloater(VisualElement floaterRoot, TConfig floaterConfig);
    protected abstract void OnUpdateFloater(VisualElement floaterRoot, TConfig floaterConfig);
    protected abstract void OnRemoveFloater(VisualElement floaterRoot, TConfig floaterConfig);

    protected virtual void Awake()
    {
        if (floaterTemplate == null)
        {
            Debug.LogError("FloatingUIManager: No floater template found", this);
        }
        
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
        
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                Debug.LogWarning("FloatingUIManager: No view camera assigned and Camera.main is not found.", this);
            }
        }
    }

    private List<string> floatersToRemove = new List<string>();
    private void LateUpdate()
    {
        floatersToRemove.Clear();
        foreach (FloaterData floaterData in floaterDatas.Values)
        {
            if (!floaterData.IsInitialized || floaterData.IsDestroyed) continue;

            if (floaterData.Config.LifetimeOwner == null) 
            {
                // The owner of the floater has been destroyed, mark it for removal
                floatersToRemove.Add(floaterData.Id);
                continue;
            }
            
            // Otherwise we are good to update the floater content and position
            OnUpdateFloater(floaterData.Instance, floaterData.Config);
            UpdateFloaterPosition(floaterData);
        }
        
        // Remove floaters that were marked for removal
        foreach (string floaterId in floatersToRemove)
        {
            RemoveFloater(floaterId);
        }
    }

    protected virtual bool ValidateConfig(TConfig floaterConfig)
    {
        if (floaterConfig == null) 
        {
            Debug.LogWarning("Cannot create floater with a null config.", this);
            return false;
        }

        if (floaterConfig.PositionType == FloatingUIPositionType.Transform && floaterConfig.TargetTransform == null)
        {
            // This also implicitly handles if the transform was destroyed before creation.
            Debug.LogWarning("Floater config requires a TargetTransform for PositionType.Transform, but it was null.", this);
            return false;
        }
        
        if (floaterConfig.LifetimeOwner == null)
        {
            Debug.LogWarning($"Floater has no LifetimeOwner set. It will be destroyed immediately.", this);
        }

        return true;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="floaterConfig"></param>
    /// <returns>Unique id for this floater. Used to remove it.</returns>
    [CanBeNull]
    public virtual string CreateFloater(TConfig floaterConfig)
    {
        if (floaterTemplate == null)
        {
            Debug.LogError("FloatingUIManager: No floater template found", this);
            return null;
        }

        if (!ValidateConfig(floaterConfig))
        {
            Debug.LogWarning("FloatingUIManager: Floater config validation failed. Floater will not be created.", this);
            return null;
        }
        
        FloaterData floaterData = new FloaterData()
        {
            Config = floaterConfig
        };
        floaterData.Id = Guid.NewGuid().ToString();
        
        floaterData.Container = new VisualElement();
        floaterData.Container.name = $"FloaterContainer_{floaterData.Id}";
        if (floaterConfig.ContainerMaxWidthPercent.HasValue)
        {
            floaterData.Container.style.maxWidth = Length.Percent(floaterConfig.ContainerMaxWidthPercent.Value);
        }

        if (floaterConfig.ContainerMinWidthPercent.HasValue)
        {
            floaterData.Container.style.minWidth = Length.Percent(floaterConfig.ContainerMinWidthPercent.Value);
        }
        floaterData.Container.style.position = Position.Absolute;
        floaterData.Container.style.visibility = Visibility.Hidden;  // Start hidden until position is set correctly

        floaterData.Instance = floaterTemplate.CloneTree();
        floaterData.Instance.name = $"Floater_{floaterData.Id}";
        floaterData.Container.Add(floaterData.Instance);
        
        bool setupSucceeded = OnSetupFloater(floaterData.Instance, floaterConfig);  // Sets up the internals so that the geometry is correct next frame
        if (!setupSucceeded)
        {
            Debug.LogWarning($"FloatingUIManager: Floater setup failed for {typeof(TConfig).Name}. Floater will not be created.", this);
            return null;
        }
        
        uiDocument.rootVisualElement.Add(floaterData.Container);
        
        // Before the geometry of the floater is updated, we keep it in the Initializing state
        floaterDatas[floaterData.Id] = floaterData;
        floaterData.Container.RegisterCallbackOnce<GeometryChangedEvent>(evt =>
        {
            if (floaterDatas.ContainsKey(floaterData.Id) && !floaterData.IsInitialized)
            {
                OnInitialGeometryReady(floaterData);
            }
        });
        return floaterData.Id;
    }
    
    public virtual void RemoveFloater(string floaterId)
    {
        if (!floaterDatas.TryGetValue(floaterId, out FloaterData floaterData) || floaterData.IsDestroyed)
        {
            Debug.LogWarning($"Attempted to remove floater with ID {floaterId}, but it does not exist or has already been removed.");
            return;
        }
        
        // Step 1: Remove the floater from the UI
        uiDocument.rootVisualElement.Remove(floaterData.Container);
        
        // Step 2: Call the remove callback to clean up any internal state
        OnRemoveFloater(floaterData.Instance, floaterData.Config);
        
        // Step 3: Remove the floater from the dictionary and set the IsDestroyed flag so that it is not updated anymore
        floaterData.IsDestroyed = true;
        floaterDatas.Remove(floaterId);
        
        // Step 4: Call the removal complete event if set
        floaterData.Config.OnRemovalComplete?.Invoke();
    }
    
    #region Helpers
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="floaterData"></param>
    /// <returns>If the floater is visible. Used to selectively call UpdateFloater.</returns>
    private bool UpdateFloaterPosition(FloaterData floaterData)
    {
        float width = floaterData.Container.resolvedStyle.width;
        float height = floaterData.Container.resolvedStyle.height;
        
        float panelWidth = uiDocument.rootVisualElement.resolvedStyle.width;
        float panelHeight = uiDocument.rootVisualElement.resolvedStyle.height;
        
        // Step 1: Get the screen space position of the pivot
        Vector2? potentialScreenPosition = floaterData.Config.PositionType switch
        {
            FloatingUIPositionType.Transform => ConvertWorldToPanelPosition(floaterData.Config.TargetTransform.position),
            FloatingUIPositionType.WorldPosition => ConvertWorldToPanelPosition(floaterData.Config.TargetWorldPosition),
            FloatingUIPositionType.ScreenPosition => floaterData.Config.TargetScreenPosition,
            _ => null
        };
        if (floaterData.Config.PositionType == FloatingUIPositionType.ScreenPosition && floaterData.Config.TargetScreenPositionType == ScreenPositionType.Normalized)
        {
            // Convert normalized screen position to pixel position
            potentialScreenPosition = new Vector2(
                floaterData.Config.TargetScreenPosition.x * panelWidth,
                floaterData.Config.TargetScreenPosition.y * panelHeight
            );
        }
        
        if (!potentialScreenPosition.HasValue)
        {
            // There is no valid screen position. Indicate that to the caller
            Debug.LogWarning("Floating UI Manager has no screen position for " + typeof(TConfig).Name);
            return false;
        }
        
        // Step 2: Calculate the top-left position based on the anchor.
        Vector2 elementPosition = potentialScreenPosition.Value;
        switch (floaterData.Config.Anchor)
        {
            // The default is TopLeft, which requires no adjustment.
            // case floatingUIAnchor.TopLeft:
            //     elementPosition += new Vector2(0, 0);
            //     break;
            case floatingUIAnchor.TopRight:
                elementPosition += new Vector2(-width, 0);
                break;
            case floatingUIAnchor.BottomLeft:
                elementPosition += new Vector2(0, -height);
                break;
            case floatingUIAnchor.BottomRight:
                elementPosition += new Vector2(-width, -height);
                break;
            case floatingUIAnchor.TopCenter:
                elementPosition += new Vector2(-width / 2, 0);
                break;
            case floatingUIAnchor.BottomCenter:
                elementPosition += new Vector2(-width / 2, -height);
                break;
            case floatingUIAnchor.MiddleLeft:
                elementPosition += new Vector2(0, -height / 2);
                break;
            case floatingUIAnchor.MiddleRight:
                elementPosition += new Vector2(-width, -height / 2);
                break;
            case floatingUIAnchor.Center:
                elementPosition += new Vector2(-width / 2, -height / 2);
                break;
        }

        // Apply the user-defined screen space offset after anchoring.
        Vector2 elementScreenPosition = elementPosition + floaterData.Config.ScreenSpaceOffset;
        
        // Step 3: Check if the floater needs to be moved to keep it on screen or if it's off-screen
        bool isVisible;

        if (floaterData.Config.KeepOnScreen)
        {
            // Clamp the position to ensure the entire element stays within the panel bounds.
            // By definition, if we clamp it, it will be visible on screen.
            elementScreenPosition.x = Mathf.Clamp(elementScreenPosition.x, 0, panelWidth - width);
            elementScreenPosition.y = Mathf.Clamp(elementScreenPosition.y, 0, panelHeight - height);
            
            isVisible = true;
        }
        else
        {
            // We are not clamping, so we must check if the element is off-screen.
            // An element is considered visible if any part of it intersects with the screen area.
            bool isHorizontallyOffscreen = (elementScreenPosition.x + width <= 0) || (elementScreenPosition.x >= panelWidth);
            bool isVerticallyOffscreen = (elementScreenPosition.y + height <= 0) || (elementScreenPosition.y >= panelHeight);
            
            // If it's not off-screen either horizontally or vertically, then it must be at least partially visible.
            isVisible = !(isHorizontallyOffscreen || isVerticallyOffscreen);
        }
        
        // Step 4: Apply the final calculated position to the container's style
        floaterData.Container.style.left = elementScreenPosition.x;
        floaterData.Container.style.top = elementScreenPosition.y;
        
        // Automatically hide off-screen floaters
        floaterData.Container.style.visibility = isVisible ? Visibility.Visible : Visibility.Hidden;

        return isVisible; // Return whether the floater is on-screen.
    }
    
    private void OnInitialGeometryReady(FloaterData floaterData)
    {
        bool isVisible = UpdateFloaterPosition(floaterData);
        floaterData.Container.style.visibility = isVisible ? Visibility.Visible : Visibility.Hidden;
        floaterData.IsInitialized = true;  // Mark as initialized
        // Call the creation complete event if set
        floaterData.Config.OnCreationComplete?.Invoke();
    }
    
    #endregion
    
    #region Coordinate Helpers

    /// <summary>
    /// Gets the IPanel associated with the UIDocument, caching it for efficiency.
    /// </summary>
    /// <returns>The IPanel interface, or null if unavailable.</returns>
    private IPanel GetPanel()
    {
        // Return cached panel if available
        if (cachedPanel == null && uiDocument?.rootVisualElement != null)
        {
            // Cache the panel reference the first time it's needed
            cachedPanel = uiDocument.rootVisualElement.panel;
        }
        return cachedPanel;
    }

    /// <summary>
    /// Converts a world-space position to a UI Toolkit panel position (origin top-left).
    /// Handles points behind the camera.
    /// </summary>
    /// <param name="worldPos">The world-space position to convert.</param>
    /// <returns>The corresponding panel position (Vector2), or null if conversion fails or point is behind camera.</returns>
    private Vector2? ConvertWorldToPanelPosition(Vector3 worldPos)
    {
        if (viewCamera == null) return null;
        IPanel panel = GetPanel();
        if (panel == null) return null;

        // Convert world position to screen coordinates (pixels, origin bottom-left)
        // screenPoint3D.z is distance from camera plane
        Vector3 screenPoint3D = viewCamera.WorldToScreenPoint(worldPos);

        // Check if the point is behind the camera's near clipping plane
        if (screenPoint3D.z < viewCamera.nearClipPlane)
        {
            return null; // Point is behind the camera, not visible on screen
        }

        // Convert screen coordinates (Vector2 part) to panel coordinates (origin bottom-left)
        // This accounts for panel scaling and offset within the screen.
        Vector2 panelPosBottomOrigin = RuntimePanelUtils.ScreenToPanel(panel, screenPoint3D);

        // Get the actual resolved height of the panel's root visual element
        float panelHeight = panel.visualTree.resolvedStyle.height;

        // Fallback to camera pixel height if panel height is invalid (e.g., during initial layout, or if panel covers whole screen)
        if (float.IsNaN(panelHeight) || float.IsInfinity(panelHeight) || panelHeight <= 0)
        {
            panelHeight = viewCamera.pixelHeight; // Use camera's render target height
            if (panelHeight <= 0) return null; // Cannot determine height
        }

        // Convert Y coordinate from bottom-left origin to top-left origin
        // Panel Y (top-left) = Panel Height - Panel Y (bottom-left)
        return new Vector2(panelPosBottomOrigin.x, panelHeight - panelPosBottomOrigin.y);
    }

    #endregion
}