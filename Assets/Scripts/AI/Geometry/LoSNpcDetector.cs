#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to objects that hook into Line of Sight triggers on NPCs
/// Exposes events for NPCs entering or leaving line of sight 
/// </summary>
public class LoSNpcDetector : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] [Tooltip("The transform to use as the origin for the line of sight rays")]
    private Transform losOrigin;
    public Transform LoSOrigin => losOrigin;
    
    [Header("Field of View Configuration")]
    [SerializeField] [Tooltip("The max distance this NPC can see.")]
    private float maxViewDistance = 100f;
    public float MaxViewDistance => maxViewDistance;

    [SerializeField]
    [Tooltip("The field of view of the agent (degrees)")]
    private float viewAngle = 90f;
    public float ViewAngle => viewAngle;
    
    [SerializeField] [Tooltip("The collider layer to look for NPCs on")]
    private LayerMask npcColliderLayer;
    static string DEFAULT_NPC_COLLIDER_LAYER_NAME = "NPC";
    
    [SerializeField] [Tooltip("The collider layer to look for obstacles on")]
    private LayerMask obstacleColliderLayer;
    static string DEFAULT_OBSTACLE_COLLIDER_LAYER_NAME = "Default";

    #endregion

    
    #region Internal Fields

    private static int NUM_OVERLAP_RESULTS = 100;
    private Collider[] overlapResultCache = new Collider[NUM_OVERLAP_RESULTS];
    private HashSet<GameObject> seenObjectCache = new HashSet<GameObject>();
    private Dictionary<GameObject, NpcContext> seenGoNpcMapCache = new Dictionary<GameObject, NpcContext>();
    private HashSet<NpcContext> visibleNpcCache = new HashSet<NpcContext>();
    private HashSet<NpcContext> currentlyVisibleNpcs = new HashSet<NpcContext>();

    #endregion

    #region Events

    public event Action<NpcContext> OnNpcEnter;
    public event Action<NpcContext> OnNpcLeave;

    #endregion

    #region Public Methods
    
    /// <summary>
    /// TODO: I need to decide what to do about this. visibleNpcCache is not correct because it is updated after the
    /// events are thrown so if a event handler needed to read all visible NPCs it would be wrong. However,
    /// currentlyVisibleNpcs might potentially have a race condition where it is only partially filled. It is filled
    /// by the time any events are raised though so on balance it will probably be better.
    /// </summary>
    public IEnumerable<NpcContext> VisibleNpcs => currentlyVisibleNpcs;

    #endregion

    
    #region Npc Detection

    /// <summary>
    /// Resets currentlyVisibleNpcs and fills it with Npc contexts for npcs within the max view distance
    /// without regard for line of sight.
    /// </summary>
    private void populateCandidateVisibleNpcs()
    {
        int numFound = Physics.OverlapSphereNonAlloc(losOrigin.position, maxViewDistance, overlapResultCache, npcColliderLayer);
        if (numFound == NUM_OVERLAP_RESULTS)
        {
            Debug.LogWarning($"Line of Sight overlap on {gameObject.name} ran out of space in the overlap result cache");
        }

        currentlyVisibleNpcs.Clear();  // We build this up anew every update
        for (int i = 0; i < numFound; i++)
        {
            GameObject obj = overlapResultCache[i].gameObject;
            if (obj == this.gameObject)
            {
                // We don't need to detect ourself
                continue;
            }
            
            if (seenObjectCache.Contains(obj))
            {
                // Then we have already seen this game object and know whether it is an NPC. We just need to look it up.
                if (seenGoNpcMapCache.ContainsKey(obj))
                {
                    currentlyVisibleNpcs.Add(seenGoNpcMapCache[obj]);
                }
                // else: This isn't an npc and is already in the cache so nothing to do
            }
            else
            {
                // Then this is an unseen object. We at least need to put it in the cache.
                seenObjectCache.Add(obj);
                // And we also need to find out if it is an NPC
                bool hasNpcContext = obj.TryGetComponent<NpcContext>(out NpcContext context);
                if (hasNpcContext)
                {
                    // Cool, it's an NPC. So it is a candidate visible NPC and also needs to be cached.
                    currentlyVisibleNpcs.Add(context);
                    seenGoNpcMapCache.Add(obj, context);
                }
            }
        }
    }

    /// <summary>
    /// Assumes that currentlyVisibleNpcs is full of NpcContext for npcs within max view distance.
    /// Uses a physics raycast to see if it hit anything before getting to the npc gameobject. If so, the npc is
    /// removed from the set.
    /// </summary>
    private void filterVisibleNpcsByLoS()
    {
        currentlyVisibleNpcs.RemoveWhere(npcContext =>
        {
            // First, we check if the target is within the FoV
            if (Vector3.Angle(losOrigin.transform.forward, npcContext.transform.position - losOrigin.position) >
                viewAngle / 2f)
            {
                return true;
            }
            
            return Physics.Linecast(losOrigin.position, npcContext.transform.position, out RaycastHit hit, obstacleColliderLayer);
        });
    }
    
    #endregion

    
    #region Unity Lifecycle
    
    private void CleanUpDestroyedNpcsFromCache()
    {
        // Clean the HashSet of null GameObjects
        seenObjectCache.RemoveWhere(obj => obj == null);

        // Clean the Dictionary by removing entries where the Key (GameObject) is null
        // This requires iterating and collecting keys to remove, as modifying during foreach is not allowed.
        List<GameObject> keysToRemove = null;
        foreach (var kvp in seenGoNpcMapCache)
        {
            if (kvp.Key == null)
            {
                if (keysToRemove == null) keysToRemove = new List<GameObject>();
                keysToRemove.Add(kvp.Key); // Key here will be null reference
            }
        }

        if (keysToRemove != null)
        {
            foreach (var key in keysToRemove)
            {
                seenGoNpcMapCache.Remove(key);
            }
        }
    }
    
    protected virtual void Update()
    {
        // 1: Fill the currentlyVisibleNpcs set with candidates within the max view distance
        populateCandidateVisibleNpcs();
        
        // 2: Filter these by line of sight
        filterVisibleNpcsByLoS();

        // 3: Find npcs that are now visible that previously were not
        foreach (NpcContext npcContext in currentlyVisibleNpcs)
        {
            if (!visibleNpcCache.Contains(npcContext))
            {
                // Debug.Log($"{npcContext.gameObject.name} entered into LoS of {gameObject.name}");
                OnNpcEnter?.Invoke(npcContext);
            }
        }
        
        // 4: Find Npcs that are now not visible that previously were
        foreach (NpcContext npcContext in visibleNpcCache)
        {
            if (!currentlyVisibleNpcs.Contains(npcContext))
            {
                // Debug.Log($"{npcContext.gameObject.name} exited LoS of {gameObject.name}");
                OnNpcLeave?.Invoke(npcContext);
            }
        }
        
        // end: Copy the data from currentlyVisibleNpcs to visibleNpcCache to prep for next frame comparisons
        visibleNpcCache.Clear();
        visibleNpcCache.UnionWith(currentlyVisibleNpcs);
        
        // Clean up the caches to prevent memory leaks for dynamically generated NPCs
        CleanUpDestroyedNpcsFromCache();
    }

    /// <summary>
    /// Initializes serialized fields with default values in the editor,
    /// attempting to find a "Head" child for the LoS origin before defaulting to self.
    /// </summary>
    protected virtual void InitializeFields()
    {
        // --- LoS Origin Initialization ---
        // Only attempt auto-detection if the field is not already assigned in the Inspector
        if (losOrigin == null)
        {
            // Attempt to find an immediate child transform named exactly "Head"
            Transform headTransform = transform.Find("Head"); // Case-sensitive search

            if (headTransform != null)
            {
                // Found the "Head" child, assign its transform
                losOrigin = headTransform;
                 // Optional: Log assignment in editor for clarity during setup/validation
                 #if UNITY_EDITOR
                 // Debug.Log($"LoSNpcDetector on {gameObject.name}: Automatically assigned 'Head' child transform as LoS Origin.", this);
                 #endif
            }
            else
            {
                // No "Head" child found, default to the transform this component is attached to
                losOrigin = transform;
                // Optional: Log assignment in editor for clarity during setup/validation
                 #if UNITY_EDITOR
                 // Debug.Log($"LoSNpcDetector on {gameObject.name}: No 'Head' child found. Defaulting LoS Origin to self ({transform.name}).", this);
                 #endif
            }
        }
        // If losOrigin was already set in the inspector, we leave it untouched.


        // --- LayerMask Initialization ---
        // Default NPC layer if not set
        if (npcColliderLayer == 0) // LayerMask value is 0 if nothing is selected
        {
            // Attempt to get the layer index by name
            int npcLayerIndex = LayerMask.NameToLayer(DEFAULT_NPC_COLLIDER_LAYER_NAME);
            if (npcLayerIndex != -1) // LayerToName returns -1 if layer doesn't exist
            {
                // Convert layer index to bitmask
                npcColliderLayer = 1 << npcLayerIndex;
            }
            else
            {
                 #if UNITY_EDITOR
                 Debug.LogWarning($"LoSNpcDetector on {gameObject.name}: Default NPC Layer '{DEFAULT_NPC_COLLIDER_LAYER_NAME}' not found in Layer settings. Please assign manually.", this);
                 #endif
            }
        }

        // Default Obstacle layer if not set
        if (obstacleColliderLayer == 0)
        {
             // Attempt to get the layer index by name
            int obstacleLayerIndex = LayerMask.NameToLayer(DEFAULT_OBSTACLE_COLLIDER_LAYER_NAME);
             if (obstacleLayerIndex != -1)
             {
                 // Convert layer index to bitmask
                obstacleColliderLayer = 1 << obstacleLayerIndex;
             }
             else
             {
                 #if UNITY_EDITOR
                 Debug.LogWarning($"LoSNpcDetector on {gameObject.name}: Default Obstacle Layer '{DEFAULT_OBSTACLE_COLLIDER_LAYER_NAME}' not found in Layer settings. Please assign manually.", this);
                 #endif
             }
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        InitializeFields();
#endif
    }

    private void Reset()
    {
#if UNITY_EDITOR
        InitializeFields();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws Gizmos in the Editor scene view when the GameObject is selected.
    /// Visualizes the forward direction (sight line), the view cone edges,
    /// a wireframe arc indicating range, and lines to currently visible NPCs.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Ensure we have a valid origin to draw from
        if (losOrigin == null)
        {
            // Attempt to initialize fields if origin is missing (e.g., after script reload)
            InitializeFields();
             // If still null after trying to initialize, exit to prevent errors
            if (losOrigin == null) return;
        }

        // Store original Handles color. We'll primarily use Handles now.
        Color originalHandlesColor = Handles.color;
        // Thickness for lines can be adjusted here
        float lineThickness = 1.0f;
        float npcLineThickness = 1.5f; // Make NPC lines slightly thicker

        // --- Draw Sight Line (Forward Direction) ---
        Handles.color = Color.blue; // Use blue for the main sight line
        // Draw a line representing the forward direction up to the max view distance
        Handles.DrawLine(losOrigin.position, losOrigin.position + losOrigin.forward * maxViewDistance, lineThickness);


        // --- Draw View Cone Edges (Lines) ---
        Handles.color = Color.green; // Use solid green for the cone edges
        // Calculate the direction vectors for the left and right edges of the view cone
        // These are correctly calculated relative to the object's forward direction
        Vector3 startDirection = Quaternion.Euler(0, -viewAngle / 2, 0) * losOrigin.forward;
        Vector3 rightDirection = Quaternion.Euler(0, viewAngle / 2, 0) * losOrigin.forward;
        // Draw lines from the origin along these directions to the max view distance
        Handles.DrawLine(losOrigin.position, losOrigin.position + startDirection * maxViewDistance, lineThickness);
        Handles.DrawLine(losOrigin.position, losOrigin.position + rightDirection * maxViewDistance, lineThickness);

        // --- Draw Lines to Visible NPCs ---
        Handles.color = Color.red; // Use red for lines to currently visible NPCs
        // Check if the application is playing, as currentlyVisibleNpcs is only populated during play mode Update
        if (Application.isPlaying && currentlyVisibleNpcs != null)
        {
            foreach (NpcContext npcContext in currentlyVisibleNpcs)
            {
                // Ensure the NPC context and its transform are still valid before drawing
                if (npcContext != null && npcContext.transform != null)
                {
                    Handles.DrawLine(losOrigin.position, npcContext.transform.position, npcLineThickness);
                }
            }
        }

        // Restore original color
        Handles.color = originalHandlesColor;
    }
#endif // UNITY_EDITOR
    
    #endregion
}
