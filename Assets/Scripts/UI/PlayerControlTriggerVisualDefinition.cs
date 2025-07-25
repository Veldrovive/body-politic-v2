using System.Linq;
using UnityEngine;

public class PlayerControlTriggerVisualDefinition : MonoBehaviour
{
    // --- Feedback ---
    [Header("Feedback")]
    [Tooltip("The Renderer component used for visual highlighting feedback. If null, attempts to find one on this or child GameObjects.")]
    [SerializeField] private GameObject highlightObject;
    [Tooltip("The layer mask used for highlighting.")]
    [SerializeField] private RenderingLayerMask highlightLayerMask;
    public GameObject HighlightObject => highlightObject;
    private Renderer[] highlightRenderers;  
    private uint[] originalRendererLayers;

    // --- UI ---
    [Header("UI")]
    [Tooltip("The prefab to use for the floating icon above this trigger. If null, uses the default icon prefab.")]
    [SerializeField] private GameObject overrideIconPrefab;
    public GameObject OverrideIconPrefab => overrideIconPrefab;

    [Tooltip("The transform to use to position the floating icon and menu above this trigger. If null, uses the transform of this GameObject.")]
    [SerializeField] private Transform iconPositionTransform;
    public Transform IconPositionTransform => iconPositionTransform;

    void AutoSetHighlightObject()
    {
        // Similarly, we try the parent first and then default to this object
        if (highlightObject == null)
        {
            highlightObject = transform.parent.gameObject;
        }
        
        highlightRenderers = highlightObject.TryGetComponent<Renderer>(out var meshRenderer)  
            ? new[] {meshRenderer}  
            : highlightObject.GetComponentsInChildren<Renderer>();  
        originalRendererLayers = highlightRenderers.Select(r => r.renderingLayerMask).ToArray();
    }

    public void SetHighlightEnabled(bool enabled)
    {
        if (highlightObject == null)
        {
            Debug.LogWarning("Highlight object is not set. Please assign a highlight object in the inspector or ensure it is set in code.");
            return;
        }
        
        Debug.Log($"Setting highlight enabled: {enabled} for {highlightObject.name} with layer mask {highlightLayerMask} on {highlightRenderers.Length} renderers.");
        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            var highlightRenderer = highlightRenderers[i];
            if (highlightRenderer != null)
            {
                highlightRenderer.renderingLayerMask = enabled ? 
                    highlightLayerMask | originalRendererLayers[i] : 
                    originalRendererLayers[i];
            }
        }
        
    }

    void AutoSetIconTransform()
    {
        // If the icon position transform is not set, we default to this GameObject's transform
        if (iconPositionTransform == null)
        {
            iconPositionTransform = transform;
        }
    }

    void Awake()
    {
        // Auto-assign renderer if not set
        AutoSetHighlightObject();

        // Auto-assign icon position transform if not set
        AutoSetIconTransform();
    }

    #if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Called when the component is first added or Reset is used.
    /// Sets the initial layer for the GameObject.
    /// </summary>
    void Reset()
    {
        AutoSetHighlightObject();
        AutoSetIconTransform();
    }
    #endif
}
