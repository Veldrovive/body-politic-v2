using UnityEngine;

/// <summary>
/// A helper component attached to individual trigger colliders that form part of a larger logical zone.
/// Detects NPC entries/exits for its specific collider and reports them to a central AbstractNpcDetector.
/// </summary>
[RequireComponent(typeof(Collider))] // Ensure a Collider is present
public class ZoneColliderBridge : MonoBehaviour
{
    [Tooltip("Optional: Manually assign the main detector. If null, GetComponentInParent will be used.")]
    [SerializeField] private AbstractNpcDetector mainDetector;

    /// <summary>
    /// Called when the script instance is being loaded.
    /// </summary>
    void Awake()
    {
        // Ensure the collider is set to be a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"Collider on {gameObject.name} is not set to 'Is Trigger'. ZoneColliderBridge may not function correctly.", this);
            // Optionally force it: col.isTrigger = true;
        }

        // Get a reference to the parent detector if not assigned
        if (mainDetector == null)
        {
            mainDetector = GetComponentInParent<AbstractNpcDetector>();
            mainDetector ??= GetComponent<AbstractNpcDetector>();  // If it wasn't found in the parent, check this object
            if (mainDetector == null)
            {
                Debug.LogError($"ZoneColliderBridge on {gameObject.name} could not find an AbstractNpcDetector in its parents! Disabling bridge.", this);
                enabled = false; // Disable this component if it cannot find its controller
            }
        }
    }

    /// <summary>
    /// Called by Unity when another Collider enters this trigger.
    /// </summary>
    /// <param name="other">The Collider that entered.</param>
    void OnTriggerEnter(Collider other)
    {
        // Ignore if the bridge or detector isn't properly set up
        if (!enabled || mainDetector == null) return;

        // Check if the collider belongs to an NPC by seeing if it has a NpcIdentity component
        if (other.TryGetComponent(out NpcContext npcContext))
        {
            // Notify the main detector, passing the identity directly
            mainDetector.NotifyNpcEnteredCollider(npcContext, this);
        }
    }

    /// <summary>
    /// Called by Unity when another Collider exits this trigger.
    /// </summary>
    /// <param name="other">The Collider that exited.</param>
    void OnTriggerExit(Collider other)
    {
        // Ignore if the bridge or detector isn't properly set up
        if (!enabled || mainDetector == null) return;

        // Check if the collider belongs to an NPC by seeing if it has a NpcIdentity component
        if (other.TryGetComponent(out NpcContext npcContext))
        {
            // Notify the main detector, passing the identity directly
            mainDetector.NotifyNpcExitedCollider(npcContext, this);
        }
    }
}