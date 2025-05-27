using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; // Needed for LINQ

/// <summary>
/// Holds data about a detected NPC.
/// </summary>
public class DetectedNpcData
{
    public NpcContext NpcContext { get; } // The context of the detected NPC
    public float EnterTime { get; } // Time when the NPC entered the zone

    public DetectedNpcData(NpcContext npcContext)
    {
        NpcContext = npcContext;
        EnterTime = Time.time; // Record the time of entry
    }
}

/// <summary>
/// Base class for components that detect NPCs within a defined area (potentially composed of multiple colliders).
/// Manages the state of which NPCs are currently inside the logical area based on reports from ZoneColliderBridge components.
/// </summary>
public abstract class AbstractNpcDetector : MonoBehaviour // Consider adding ", INpcDetector" if you defined that interface
{
    /// <summary>
    /// Tracks which colliders each detected NPC is currently in contact with.
    /// Key: NpcContext, Value: Set of bridges reporting contact.
    /// </summary>
    private readonly Dictionary<NpcContext, HashSet<ZoneColliderBridge>> npcColliderContacts = new();

    /// <summary>
    /// Stores data for NPCs currently considered inside the logical zone.
    /// Key: NpcContext, Value: Associated data including GameObject.
    /// </summary>
    protected readonly Dictionary<NpcContext, DetectedNpcData> detectedNpcs = new();

    /// <summary>
    /// Fired when an NPC enters the logical zone (enters the first associated collider).
    /// </summary>
    public event Action<DetectedNpcData> OnNpcEnteredZoneEvent;

    /// <summary>
    /// Fired when an NPC exits the logical zone (exits the last associated collider).
    /// </summary>
    public event Action<DetectedNpcData> OnNpcExitedZoneEvent;

    // ******* External Event Handlers (Called by ZoneColliderBridge) ********
    
    public int NpcCount => detectedNpcs.Count; // Read-only property to get the number of currently detected NPCs

    /// <summary>
    /// Called by a ZoneColliderBridge when an NPC enters its specific trigger volume.
    /// Updates internal tracking and fires OnNpcEnteredZoneEvent if this is the first contact for the logical zone.
    /// </summary>
    /// <param name="npcContext">The context object of the NPC that entered.</param>
    /// <param name="colliderBridge">The specific bridge that reported the entry.</param>
    public void NotifyNpcEnteredCollider(NpcContext npcContext, ZoneColliderBridge colliderBridge)
    {
        if (npcContext == null || colliderBridge == null)
        {
            Debug.LogWarning($"{gameObject.name}: Received null NPC identity or collider bridge.", this);
            return;
        }

        // Check if this is the first collider entered for this NPC
        bool isNewZoneEntry = !npcColliderContacts.ContainsKey(npcContext);

        if (isNewZoneEntry)
        {
            // First contact: create new entry for collider tracking
            npcColliderContacts[npcContext] = new HashSet<ZoneColliderBridge> { colliderBridge };
            // Also add to the main detected list and fire the zone entry event
            OnNpcEnteredZone(npcContext);
        }
        else
        {
            // Existing contact: just add the new collider bridge to the set
            // Note: HashSet automatically handles duplicates if the same bridge reports enter twice.
            npcColliderContacts[npcContext].Add(colliderBridge);
        }
    }

    /// <summary>
    /// Called by a ZoneColliderBridge when an NPC exits its specific trigger volume.
    /// Updates internal tracking and fires OnNpcExitedZoneEvent if this is the last contact for the logical zone.
    /// </summary>
    /// <param name="npcContext">The identity of the NPC that exited.</param>
    /// <param name="colliderBridge">The specific bridge that reported the exit.</param>
    public void NotifyNpcExitedCollider(NpcContext npcContext, ZoneColliderBridge colliderBridge)
    {
         if (npcContext == null || colliderBridge == null)
        {
            Debug.LogWarning($"{gameObject.name}: Received null NPC identity or collider bridge.", this);
            return;
        }

        // Check if we are tracking this NPC
        if (npcColliderContacts.TryGetValue(npcContext, out HashSet<ZoneColliderBridge> contacts))
        {
            // Remove the collider bridge from the set
            contacts.Remove(colliderBridge);

            // If the NPC is no longer in contact with any colliders for this zone...
            if (contacts.Count == 0)
            {
                // Remove the tracking entry entirely
                npcColliderContacts.Remove(npcContext);
                // Remove from the main detected list and fire the zone exit event
                OnNpcExitedZone(npcContext);
            }
            // else: NPC is still inside other colliders belonging to this zone.
        }
        else
        {
            // This might happen if exit events fire slightly out of order or if the NPC was removed for other reasons.
            // Usually safe to ignore, but warning can be helpful during debugging.
            // Debug.LogWarning($"NPC {npcContext.name} exited collider {colliderBridge.name} of zone {gameObject.name}, but was not in the contacts dictionary.", this);
        }
    }
    // ******** END OF External Event Handlers ********


    // ******* Internal Zone Event Handlers ********

    /// <summary>
    /// Internal handler called when an NPC is confirmed to have entered the logical zone.
    /// </summary>
    /// <param name="npcContext">The identity of the NPC.</param>
    private void OnNpcEnteredZone(NpcContext npcContext)
    {
        // Defensive check: Ensure we don't add duplicates if logic error occurs elsewhere
        if (detectedNpcs.ContainsKey(npcContext))
        {
            // Debug.LogWarning($"OnNpcEnteredZone called for {npcContext.name} but they were already in the detectedNpcs list.", this);
            return;
        }

        // Create the data payload
        DetectedNpcData detectedNpcData = new DetectedNpcData(npcContext);
        // Add to the main dictionary
        detectedNpcs.Add(npcContext, detectedNpcData);

        // Fire the public event
        OnNpcEnteredZoneEvent?.Invoke(detectedNpcData);
        // Debug.Log($"NPC {npcContext.name} entered the zone of {gameObject.name}");
    }

    /// <summary>
    /// Internal handler called when an NPC is confirmed to have exited the logical zone.
    /// </summary>
    /// <param name="npcContext">The identity of the NPC.</param>
    private void OnNpcExitedZone(NpcContext npcContext)
    {
        // Try to get the data payload *before* removing
        if (detectedNpcs.TryGetValue(npcContext, out DetectedNpcData detectedNpcData))
        {
            // Remove from the main dictionary
            detectedNpcs.Remove(npcContext);
            // Fire the public event *after* removal
            OnNpcExitedZoneEvent?.Invoke(detectedNpcData);
            // Debug.Log($"NPC {npcContext.name} exited the zone of {gameObject.name}");
        }
        else
        {
            // This indicates a potential logic error, as OnNpcExitedZone should only be called
            // if the NPC was previously considered inside.
            Debug.LogWarning($"OnNpcExitedZone called for {npcContext?.name ?? "null NPC"}, but they were not found in the detectedNpcs list.", this);
        }
    }
    // ******** END OF Internal Event Handlers ********


    // ******* Public Methods (Implement INpcDetector if using interface) ********

    /// <summary>
    /// Gets data for all NPCs currently detected within the logical zone.
    /// </summary>
    /// <returns>A read-only collection of DetectedNpcData.</returns>
    public IReadOnlyCollection<DetectedNpcData> GetDetectedNpcsData()
    {
        // Returning Values directly gives an ICollection<DetectedNpcData>, which is implicitly read-only enough for many cases.
        // If strict IReadOnlyCollection is needed, you might need ToList() or similar, but Values is efficient.
        return detectedNpcs.Values;
    }

     /// <summary>
    /// Gets identities for all NPCs currently detected within the logical zone.
    /// </summary>
    /// <returns>An enumerable collection of npcContext.</returns>
    public IEnumerable<NpcContext> GetDetectedNpcs()
    {
        return detectedNpcs.Keys; // Or detectedNpcs.Values.Select(data => data.NpcContext);
    }

    /// <summary>
    /// Gets NPCs currently detected that satisfy the specified role filters.
    /// Implements the filtering logic based on INpcDetector requirements.
    /// </summary>
    /// <param name="requiredAnyRoles">If not null or empty, NPC must have at least one of these roles.</param>
    /// <param name="requiredAllRoles">If not null or empty, NPC must have all of these roles.</param>
    /// <param name="excludedAnyRoles">If not null or empty, NPC must NOT have any of these roles.</param>
    /// <returns>An enumerable collection of matching npcContext.</returns>
    public IEnumerable<DetectedNpcData> GetDetectedNpcs(List<NpcRoleSO> requiredAnyRoles = null, List<NpcRoleSO> requiredAllRoles = null, List<NpcRoleSO> excludedAnyRoles = null)
    {
        // Start with all detected NPCs
        IEnumerable<NpcContext> filteredNpcs = detectedNpcs.Keys;

        // Apply filters using LINQ's Where clause for conciseness
        bool requireAny = requiredAnyRoles != null && requiredAnyRoles.Count > 0;
        if (requireAny)
        {
            // NPC must have at least one role from the list
            filteredNpcs = filteredNpcs.Where(npc => npc != null && npc.Identity.HasAnyRole(requiredAnyRoles));
        }

        bool requireAll = requiredAllRoles != null && requiredAllRoles.Count > 0;
        if (requireAll)
        {
            // NPC must have all roles from the list
            filteredNpcs = filteredNpcs.Where(npc => npc != null && npc.Identity.HasAllRoles(requiredAllRoles));
        }

        bool excludeAny = excludedAnyRoles != null && excludedAnyRoles.Count > 0;
        if (excludeAny)
        {
            // NPC must NOT have any role from the list
            filteredNpcs = filteredNpcs.Where(npc => npc != null && !npc.Identity.HasAnyRole(excludedAnyRoles));
        }

        return filteredNpcs.Select(npc => detectedNpcs[npc]);
    }
    // ******* END OF Public Methods ********
}