using UnityEngine;

public class PlayerControlTriggerVisualDefinition : MonoBehaviour
{
    // --- Feedback ---
    [Header("Feedback")]
    [Tooltip("The Renderer component used for visual highlighting feedback. If null, attempts to find one on this or child GameObjects.")]
    [SerializeField] private Renderer meshForHighlighting;

    // --- UI ---
    [Header("UI")]
    [Tooltip("The prefab to use for the floating icon above this trigger. If null, uses the default icon prefab.")]
    [SerializeField] private GameObject overrideIconPrefab;
    public GameObject OverrideIconPrefab => overrideIconPrefab;

    [Tooltip("The transform to use to position the floating icon and menu above this trigger. If null, uses the transform of this GameObject.")]
    [SerializeField] private Transform iconPositionTransform;
    public Transform IconPositionTransform => iconPositionTransform;

    /// <summary>
    /// Gets the Renderer component intended for highlighting this interactable.
    /// </summary>
    /// <returns>The Renderer component, or null if none is available.</returns>
    public Renderer GetHighlightRenderer()
    {
        return meshForHighlighting;
    }

    void AutoSetHighlightMesh()
    {
        // Similarly, we try the parent first and then default to this object
        if (meshForHighlighting == null)
        {
            meshForHighlighting = GetComponentInParent<Renderer>();
            if (meshForHighlighting == null)
            {
                meshForHighlighting = GetComponent<Renderer>();
            }
            #if UNITY_EDITOR
            if (meshForHighlighting == null)
            {
                // Debug.LogWarning($"PlayerControlTrigger on {gameObject.name}: No Renderer component found for highlighting. Please assign one in the Inspector or ensure it's on this GameObject or its parent.", this);
            }
            #endif
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
        AutoSetHighlightMesh();

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
        AutoSetHighlightMesh();
        AutoSetIconTransform();
    }
    #endif
}
