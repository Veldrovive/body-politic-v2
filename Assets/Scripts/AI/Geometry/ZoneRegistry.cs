// Update Type: Full File
// File: ZoneRegistry.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages active ZoneDetector instances in the scene, allowing lookup by their ZoneDefinitionSO.
/// Implements a simple Singleton pattern for easy access.
/// </summary>
public class ZoneRegistry : MonoBehaviour
{
    // Simple Singleton instance
    public static ZoneRegistry Instance { get; private set; }

    // Dictionary mapping Zone Definition asset to list of active scene instances using it
    private readonly Dictionary<ZoneDefinitionSO, List<Zone>> zonesByDefinition = new();

    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate ZoneRegistry instance found. Destroying this one.", gameObject);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Optional: DontDestroyOnLoad(gameObject); // If registry should persist across scenes
    }

    /// <summary>
    /// Registers an active zone detector instance. Called by ZoneDetector on enable.
    /// </summary>
    /// <param name="zoneDetector">The zone detector instance.</param>
    public void RegisterZone(Zone zoneDetector)
    {
        if (zoneDetector == null || zoneDetector.Definition == null)
        {
            Debug.LogWarning("Attempted to register a null zone or zone with null definition.", zoneDetector);
            return;
        }

        ZoneDefinitionSO definition = zoneDetector.Definition;

        // Find or create the list for this definition
        if (!zonesByDefinition.TryGetValue(definition, out List<Zone> zoneList))
        {
            zoneList = new List<Zone>();
            zonesByDefinition[definition] = zoneList;
        }

        // Add the detector if it's not already in the list
        if (!zoneList.Contains(zoneDetector))
        {
            zoneList.Add(zoneDetector);
            // Debug.Log($"Registered zone '{zoneDetector.gameObject.name}' with definition '{definition.name}'. Count: {zoneList.Count}");
        }
    }

    /// <summary>
    /// Unregisters a zone detector instance. Called by ZoneDetector on disable.
    /// </summary>
    /// <param name="zoneDetector">The zone detector instance.</param>
    public void UnregisterZone(Zone zoneDetector)
    {
        if (zoneDetector == null || zoneDetector.Definition == null)
        {
            return; // Nothing to unregister
        }

        ZoneDefinitionSO definition = zoneDetector.Definition;

        // If the definition exists in the dictionary, attempt to remove the zone
        if (zonesByDefinition.TryGetValue(definition, out List<Zone> zoneList))
        {
            if(zoneList.Remove(zoneDetector))
            {
                 // Debug.Log($"Unregistered zone '{zoneDetector.gameObject.name}' with definition '{definition.name}'. Remaining: {zoneList.Count}");
            }

            // Optional: Clean up dictionary entry if list becomes empty
            // if (zoneList.Count == 0)
            // {
            //     zonesByDefinition.Remove(definition);
            // }
        }
    }

    /// <summary>
    /// Gets all active ZoneDetector instances associated with a specific ZoneDefinitionSO.
    /// </summary>
    /// <param name="definition">The definition asset to look up.</param>
    /// <returns>An IEnumerable of matching ZoneDetectors (empty if none found).</returns>
    public IEnumerable<Zone> GetZones(ZoneDefinitionSO definition)
    {
        if (definition != null && zonesByDefinition.TryGetValue(definition, out List<Zone> zoneList))
        {
            // Return a defensive copy or wrapper if needed, but IEnumerable is often fine
            return zoneList;
        }
        // Return an empty enumerable if the definition isn't found or is null
        return Enumerable.Empty<Zone>();
    }

     void OnDestroy()
     {
         // Clear singleton instance if this object is destroyed
         if (Instance == this)
         {
             Instance = null;
             // Optionally clear the dictionary? Depends if it should survive domain reloads in editor
             // zonesByDefinition.Clear();
         }
     }
}