// Which update method you are using: Full File
using System.Collections.Generic;
using UnityEngine; // Still needed for Physics, Vector3, GameObject, LayerMask etc.

/// <summary>
/// Contains the results of a proximity check, listing which GameObjects
/// entered and exited the detection radius since the last check.
/// </summary>
public readonly struct ProximityChanges
{
    /// <summary>
    /// List of GameObjects that entered the detection radius during the last check.
    /// </summary>
    public readonly IReadOnlyList<GameObject> EnteredObjects;

    /// <summary>
    /// List of GameObjects that exited the detection radius during the last check.
    /// </summary>
    public readonly IReadOnlyList<GameObject> ExitedObjects;

    // Internal constructor for creating the struct
    internal ProximityChanges(List<GameObject> entered, List<GameObject> exited)
    {
        // Use ReadOnlyCollectionWrappers or simply assign the lists if mutation isn't a concern post-return
        EnteredObjects = entered ?? new List<GameObject>(); // Ensure non-null
        ExitedObjects = exited ?? new List<GameObject>(); // Ensure non-null
    }

    /// <summary>
    /// Helper to create an empty result.
    /// </summary>
    /// <returns>A ProximityChanges struct with empty lists.</returns>
    public static ProximityChanges Empty() => new ProximityChanges(null, null);
}


/// <summary>
/// Detects GameObjects on specified layers within a radius around a check point.
/// Calculates which GameObjects have entered or exited the radius since the last check.
/// This is a regular C# class, not a MonoBehaviour.
/// </summary>
public class ProximityDetector
{
    private readonly float _detectionRadius;
    private readonly LayerMask _detectionLayerMask;
    private readonly HashSet<GameObject> _previouslyNearbyObjects; // Stores state between checks

    /// <summary>
    /// Initializes a new ProximityDetector.
    /// </summary>
    /// <param name="detectionRadius">The radius for the overlap check.</param>
    /// <param name="detectionLayerMask">The layer mask to use for the overlap check.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if detectionRadius is negative.</exception>
    public ProximityDetector(float detectionRadius, LayerMask detectionLayerMask)
    {
        if (detectionRadius < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(detectionRadius), "Detection radius cannot be negative.");
        }

        _detectionRadius = detectionRadius;
        _detectionLayerMask = detectionLayerMask;
        _previouslyNearbyObjects = new HashSet<GameObject>();
    }

    /// <summary>
    /// Performs a proximity check around the given position and returns the changes
    /// (entered/exited GameObjects) since the last call to this method.
    /// </summary>
    /// <param name="checkPosition">The world-space position to check around.</param>
    /// <returns>A ProximityChanges struct detailing which GameObjects entered or exited.</returns>
    public ProximityChanges UpdateProximityCheck(Vector3 checkPosition)
    {
        // --- Perform the Physics Overlap Check ---
        // Find all colliders on the specified layer(s) within the radius
        // Note: Consider using Physics.OverlapSphereNonAlloc for better performance
        // if this becomes a bottleneck, but requires managing the results array.
        Collider[] hitColliders = Physics.OverlapSphere(checkPosition, _detectionRadius, _detectionLayerMask);

        // --- Collect current GameObjects ---
        // Use a HashSet for efficient lookup and automatic duplicate handling.
        HashSet<GameObject> currentFrameObjects = new HashSet<GameObject>();
        foreach (var hitCollider in hitColliders)
        {
            // Add the GameObject associated with the collider.
            // No component check here - we detect any GameObject on the layer.
            currentFrameObjects.Add(hitCollider.gameObject);
        }

        // --- Calculate Changes ---
        List<GameObject> enteredObjects = new List<GameObject>();
        List<GameObject> exitedObjects = new List<GameObject>();

        // 1. Find entered objects: Check objects in the current frame against the previous set.
        foreach (GameObject currentObject in currentFrameObjects)
        {
            // If the current object was NOT in the previous set, it has entered.
            if (!_previouslyNearbyObjects.Contains(currentObject))
            {
                enteredObjects.Add(currentObject);
            }
        }

        // 2. Find exited objects: Check objects from the previous set against the current frame.
        foreach (GameObject previousObject in _previouslyNearbyObjects)
        {
            // If a previously known object is NOT in the current frame's set, it has exited.
            // Important: Check if the object hasn't been destroyed between frames.
            if (previousObject != null && !currentFrameObjects.Contains(previousObject))
            {
                exitedObjects.Add(previousObject);
            }
        }

        // --- Update State for Next Check ---
        // Replace the previous set with the current set.
        // We clear and add rather than assigning a new HashSet to potentially reuse the existing allocation.
        _previouslyNearbyObjects.Clear();
        foreach(GameObject obj in currentFrameObjects)
        {
            _previouslyNearbyObjects.Add(obj);
        }
        // Alternatively, if performance is critical and HashSet copying is measured as slow:
        // _previouslyNearbyObjects = currentFrameObjects; // Replace the reference (simpler code)


        // --- Return Changes ---
        return new ProximityChanges(enteredObjects, exitedObjects);
    }

    /// <summary>
    /// Resets the internal state, clearing the set of previously detected objects.
    /// Useful if the detector should start fresh (e.g., after a scene load or teleport).
    /// </summary>
    public void Reset()
    {
        _previouslyNearbyObjects.Clear();
    }
}