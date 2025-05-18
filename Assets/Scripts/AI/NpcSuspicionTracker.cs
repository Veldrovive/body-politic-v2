// Update Type: Full File
// File: NpcSuspicionTracker.cs
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Sisus.ComponentNames;

/// TODO: Decide whether suspicion should also be able to be pegged to a specific role that will recognize it.
/// So let's say you steal somebody's bag. Should other people recognize that you are suspicious when holding it?
/// Or when you are in an off limit zone, should everyone start to panic or should the guards just get suspicious?

/// <summary>
/// Manages suspicion levels for an NPC based on various timed sources.
/// Calculates the current suspicion level as the maximum level from all active sources.
/// </summary>
public class NpcSuspicionTracker : MonoBehaviour
{
    // --- Internal State Class ---

    /// <summary>
    /// Internal class to store the state of an active suspicion source.
    /// </summary>
    private class SuspicionSourceState
    {
        public string SourceName; // Identifier for the source (e.g., Zone name, Witness name)
        public int Level;        // Suspicion level associated with this source
        public float EndTime;      // Time.time when this source should expire

        public SuspicionSourceState(string name, int level, float endTime)
        {
            SourceName = name;
            Level = level;
            EndTime = endTime;
        }
    }

    // --- Struct for Editor Debugging ---

    /// <summary>
    /// Read-only data structure representing an active suspicion source for debugging purposes.
    /// </summary>
    public readonly struct SuspicionSourceDebugInfo
    {
        public readonly string SourceName;
        public readonly int Level;
        public readonly float EndTime;
        // Calculate remaining time safely, ensuring it doesn't display negative
        public readonly float RemainingTime => Mathf.Max(0f, EndTime - Time.time);

        public SuspicionSourceDebugInfo(string name, int level, float endTime)
        {
            SourceName = name;
            Level = level;
            EndTime = endTime;
        }
    }

    // --- Private Fields ---

    private readonly Dictionary<string, SuspicionSourceState> activeSources = new();
    private int currentMaxSuspicion = 0;
    // Buffer list to avoid modifying dictionary during iteration in Update
    private List<string> sourcesToRemove = new List<string>();

    // --- Public Properties ---

    /// <summary>
    /// Gets the current highest suspicion level from all active sources.
    /// </summary>
    public int CurrentSuspicionLevel => currentMaxSuspicion;

    // --- Events ---

    /// <summary>
    /// Fired whenever the CurrentSuspicionLevel changes. Passes the new level.
    /// </summary>
    public event Action<int> OnSuspicionChanged;

    /// <summary>
    /// Fired only when the CurrentSuspicionLevel increases. Passes the new (higher) level.
    /// </summary>
    public event Action<int> OnSuspicionIncreased;

    /// <summary>
    /// Fired only when the CurrentSuspicionLevel decreases. Passes the new (lower) level.
    /// </summary>
    public event Action<int> OnSuspicionDecreased;

    // --- Unity Methods ---

    /// <summary>
    /// Called every frame to check for expired suspicion sources.
    /// </summary>
    void Update()
    {
        // Optimization: If no sources, nothing to check
        if (activeSources.Count == 0) return;

        // Clear the removal list at the start of the frame
        sourcesToRemove.Clear();
        float currentTime = Time.time;
        bool sourcesExpired = false;

        // Check each active source for expiration
        foreach (var kvp in activeSources)
        {
            if (currentTime >= kvp.Value.EndTime)
            {
                sourcesToRemove.Add(kvp.Key); // Mark for removal
                sourcesExpired = true;
            }
        }

        // Remove expired sources if any were found
        if (sourcesExpired)
        {
            foreach (string key in sourcesToRemove)
            {
                activeSources.Remove(key);
                // Debug.Log($"Suspicion source '{key}' expired and removed from {gameObject.name}.");
            }
            // Recalculate max suspicion level only if sources were actually removed
            RecalculateMaxSuspicion();
        }
    }

    // --- Public Methods ---

    /// <summary>
    /// Adds or updates a suspicion source. If a source with the same name already exists,
    /// its level and duration are updated, and its timer is reset.
    /// </summary>
    /// <param name="sourceName">A unique identifier for the source of suspicion.</param>
    /// <param name="level">The suspicion level associated with this source.</param>
    /// <param name="duration">How long (in seconds) this suspicion source should remain active.</param>
    public void AddSuspicionSource(string sourceName, int level, float duration)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            Debug.LogWarning("Cannot add suspicion source with a null or empty name.", this);
            return;
        }
        // Treat duration <= 0 as needing immediate removal if level isn't positive,
        // otherwise it might stick around for one frame if added late in a frame.
        if (duration <= 0 && level <= 0)
        {
            // If adding 0 or negative suspicion with no duration, just remove existing if any.
            RemoveSuspicionSource(sourceName); // This handles recalculation
            return;
        }
        if (duration <= 0 && level > 0)
        {
             // Debug.LogWarning($"Suspicion source '{sourceName}' added with non-positive duration ({duration}s). It may expire immediately.", this);
             // Let it be added, it will likely expire next Update frame.
        }


        float endTime = Time.time + Mathf.Max(0f, duration); // Ensure endTime is not in the past
        var newState = new SuspicionSourceState(sourceName, level, endTime);

        // Add or overwrite the entry in the dictionary
        activeSources[sourceName] = newState;
        // Debug.Log($"Suspicion source '{sourceName}' added/updated on {gameObject.name}. Level: {level}, Duration: {duration}s");

        // Recalculate the maximum level and fire events if it changed
        RecalculateMaxSuspicion();
    }

    /// <summary>
    /// Immediately removes a suspicion source by its name.
    /// </summary>
    /// <param name="sourceName">The unique identifier of the source to remove.</param>
    /// <returns>True if the source was found and removed, false otherwise.</returns>
    public bool RemoveSuspicionSource(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName)) return false;

        if (activeSources.Remove(sourceName))
        {
            // Debug.Log($"Suspicion source '{sourceName}' removed manually from {gameObject.name}.");
            // Recalculate max level as the removal might have changed it
            RecalculateMaxSuspicion();
            return true;
        }
        return false; // Source name not found
    }

    /// <summary>
    /// Gets a read-only collection of debug information for currently active suspicion sources.
    /// Intended for use by editor scripts.
    /// </summary>
    /// <returns>A list of SuspicionSourceDebugInfo.</returns>
    public List<SuspicionSourceDebugInfo> GetActiveSourcesDebugInfo()
    {
        var debugList = new List<SuspicionSourceDebugInfo>(activeSources.Count);
        foreach (var kvp in activeSources)
        {
            // Use the state directly from the dictionary value
            debugList.Add(new SuspicionSourceDebugInfo(kvp.Key, kvp.Value.Level, kvp.Value.EndTime));
        }
        // Sort the list for consistent display by remaining time (ascending)
        debugList.Sort((a, b) => a.RemainingTime.CompareTo(b.RemainingTime));
        return debugList;
    }

    // --- Private Helper Methods ---

    /// <summary>
    /// Recalculates the maximum suspicion level based on currently active sources
    /// and fires events if the maximum level has changed.
    /// </summary>
    private void RecalculateMaxSuspicion()
    {
        int newMaxSuspicion = 0;
        if (activeSources.Count > 0)
        {
            // Use LINQ to find the maximum level, defaulting to 0 if no sources exist (or all are <= 0)
            newMaxSuspicion = activeSources.Values.Select(source => source.Level).DefaultIfEmpty(0).Max();

            // Ensure max suspicion isn't negative
            if (newMaxSuspicion < 0) newMaxSuspicion = 0;
        }
        // else: newMaxSuspicion remains 0 if activeSources is empty

        // Check if the max level actually changed
        if (newMaxSuspicion != currentMaxSuspicion)
        {
            int oldMaxSuspicion = currentMaxSuspicion;
            currentMaxSuspicion = newMaxSuspicion;
            // Debug.Log($"Suspicion level changed on {gameObject.name} from {oldMaxSuspicion} to {currentMaxSuspicion}");

            // Fire events
            try // Wrap event invocation in try-catch as subscribers could throw exceptions
            {
                OnSuspicionChanged?.Invoke(currentMaxSuspicion);
                if (currentMaxSuspicion > oldMaxSuspicion)
                {
                    OnSuspicionIncreased?.Invoke(currentMaxSuspicion);
                }
                else // Must have decreased
                {
                    OnSuspicionDecreased?.Invoke(currentMaxSuspicion);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred in OnSuspicion event handler: {ex.Message}", this);
            }

            this.SetName($"Suspicion: {currentMaxSuspicion}"); // Update the GameObject name for debugging
        }
    }
}