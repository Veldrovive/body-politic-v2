using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Internal state tracked for each NPC currently unauthorized within the zone.
/// </summary>
internal class ZoneNpcSuspicionState
{
    public NPCIdentity Identity;
    public float TimeCheckStarted; // Time when the current unauthorized/role check period began
    public int CurrentSuspicionTierIndex = -1; // Index into the sorted suspicionTiers list
    public bool IsCurrentlyUnauthorized; // Flag indicating if they currently lack allowed roles

    // Store delegates to allow unsubscribing later
    public Action<NpcRoleSO> RoleAddedHandler;
    public Action<NpcRoleSO> RoleRemovedHandler;
}

/// <summary>
/// Concrete implementation of AbstractNpcDetector for standard zones.
/// Uses a ZoneDefinitionSO to configure role changes and suspicion rules.
/// Handles applying suspicion to NPCs lacking allowed roles based on time spent inside.
/// Uses NpcContext and NpcSuspicionTracker.
/// </summary>
public class Zone : AbstractNpcDetector
{
    [Header("Zone Configuration")]
    [Tooltip("The ScriptableObject defining the behavior and rules for this zone.")]
    [SerializeField] private ZoneDefinitionSO zoneDefinition;
    public ZoneDefinitionSO Definition => zoneDefinition; // Public getter for external access

    /// <summary>
    /// Internal tracking for NPCs that are currently being monitored for suspicion.
    /// Key: NpcContext
    /// </summary>
    private readonly Dictionary<NpcContext, ZoneNpcSuspicionState> suspicionTrackedNpcs = new(); // Assumes ZoneNpcSuspicionState class exists

    /// <summary>
    /// Cached sorted list of suspicion tiers from the definition.
    /// </summary>
    private List<SuspicionTier> sortedSuspicionTiers = new List<SuspicionTier>();

    private List<Collider> managedColliders = new List<Collider>(); // To get bounds

    /// <summary>
    /// Called when the script instance is being loaded.
    /// Validates configuration and prepares suspicion tiers.
    /// </summary>
    protected virtual void Awake()
    {
        if (zoneDefinition == null)
        {
            Debug.LogError($"ZoneDetector on {gameObject.name} is missing its ZoneDefinitionSO! Disabling suspicion and role features.", this);
            // Keep detector running for basic presence detection, but features won't work.
            // Alternatively: enabled = false;
            return;
        }

        // Cache colliders associated with this zone (from self and children with bridges)
        CacheColliders();

        // Create a sorted copy of the suspicion tiers for runtime use
        if (zoneDefinition.SuspicionTiers != null)
        {
            sortedSuspicionTiers = new List<SuspicionTier>(zoneDefinition.SuspicionTiers);
            sortedSuspicionTiers.Sort((a, b) => a.Delay.CompareTo(b.Delay));
        }
    }

    /// <summary>
    /// Finds and stores references to colliders defining this zone's area.
    /// </summary>
    private void CacheColliders()
    {
         managedColliders.Clear();
         // Check self first
         Collider selfCollider = GetComponent<Collider>();
         if (selfCollider != null && selfCollider.isTrigger) // Only if it's a trigger itself
         {
             managedColliders.Add(selfCollider);
         }
         // Check children with bridges
         ZoneColliderBridge[] bridges = GetComponentsInChildren<ZoneColliderBridge>();
         foreach(var bridge in bridges)
         {
             Collider bridgeCollider = bridge.GetComponent<Collider>();
             if(bridgeCollider != null && bridgeCollider.isTrigger) // Ensure bridge has a trigger collider
             {
                  managedColliders.Add(bridgeCollider);
             }
         }
         if(managedColliders.Count == 0)
         {
              Debug.LogWarning($"ZoneDetector on {gameObject.name} found no trigger colliders on self or children with ZoneColliderBridge. Zone area is undefined.", this);
         }
    }

    /// <summary>
    /// Called when the component becomes enabled and active.
    /// Subscribes to base class events.
    /// </summary>
    protected virtual void Start()
    {
        // Register with the runtime registry if definition is valid
        if (zoneDefinition != null && ZoneRegistry.Instance != null)
        {
            ZoneRegistry.Instance.RegisterZone(this);
        }
        else if (zoneDefinition != null) // Only log warning if registry is the issue
        {
             Debug.LogWarning($"ZoneDetector on {gameObject.name} enabled, but ZoneRegistry instance not found. Cannot register.", this);
        }

        OnNpcEnteredZoneEvent += HandleNpcEnteredZone;
        OnNpcExitedZoneEvent += HandleNpcExitedZone;

        // Re-evaluate existing NPCs in case zone was disabled/re-enabled
        if (zoneDefinition != null) // Only if definition exists
        {
             foreach(var npcData in GetDetectedNpcsData())
             {
                  HandleNpcEnteredZone(npcData);
             }
        }
    }

    /// <summary>
    /// Called when the component becomes disabled.
    /// Unsubscribes from events and cleans up tracking.
    /// </summary>
    protected virtual void OnDisable()
    {
        // Unregister from the runtime registry
        if (zoneDefinition != null && ZoneRegistry.Instance != null)
        {
            ZoneRegistry.Instance.UnregisterZone(this);
        }
        
        OnNpcEnteredZoneEvent -= HandleNpcEnteredZone;
        OnNpcExitedZoneEvent -= HandleNpcExitedZone;

        // Clean up tracking for all NPCs as if they exited
        List<NpcContext> trackedContexts = new List<NpcContext>(suspicionTrackedNpcs.Keys);
        foreach (NpcContext npcCtx in trackedContexts)
        {
             if (detectedNpcs.TryGetValue(npcCtx, out var npcData))
             {
                 HandleNpcExitedZone(npcData);
             } else {
                  StopSuspicionTracking(npcCtx);
                  if(zoneDefinition != null) ApplyRoleChanges(npcCtx, zoneDefinition.RolesToAddOnExit, zoneDefinition.RolesToRemoveOnExit);
             }
        }
         suspicionTrackedNpcs.Clear();
    }

    /// <summary>
    /// Handles an NPC entering the logical zone. Applies roles and starts suspicion tracking based on ZoneDefinitionSO.
    /// </summary>
    private void HandleNpcEnteredZone(DetectedNpcData data)
    {
        if (zoneDefinition == null || data?.NpcContext == null) return; // Need definition and context
        NpcContext npcContext = data.NpcContext;

        // --- Apply Role Changes ---
        ApplyRoleChanges(npcContext, zoneDefinition.RolesToAddOnEnter, zoneDefinition.RolesToRemoveOnEnter);

        // --- Start Suspicion Tracking (if applicable) ---
        // Check if suspicion is configured in the definition AND identity exists
        bool needsSuspicionCheck = zoneDefinition.AllowedRoles.Count > 0
                                && sortedSuspicionTiers.Count > 0
                                && npcContext.Identity != null;

        if (needsSuspicionCheck)
        {
            if (suspicionTrackedNpcs.ContainsKey(npcContext)) return; // Already tracking

            bool isUnauthorized = !npcContext.Identity.HasAnyRole(zoneDefinition.AllowedRoles);
            StartSuspicionTracking(npcContext, isUnauthorized);
        }
    }

    /// <summary>
    /// Handles an NPC exiting the logical zone. Applies roles and stops suspicion tracking based on ZoneDefinitionSO.
    /// </summary>
    private void HandleNpcExitedZone(DetectedNpcData data)
    {
        if (zoneDefinition == null || data?.NpcContext == null) return; // Need definition and context
        NpcContext npcContext = data.NpcContext;

        // --- Apply Role Changes ---
        ApplyRoleChanges(npcContext, zoneDefinition.RolesToAddOnExit, zoneDefinition.RolesToRemoveOnExit);

        // --- Stop Suspicion Tracking ---
        StopSuspicionTracking(npcContext);

        // --- Explicitly remove suspicion source from this zone ---
        if (npcContext.SuspicionTracker != null)
        {
            npcContext.SuspicionTracker.RemoveSuspicionSource(gameObject.name);
        }
    }

    /// <summary>
    /// Applies specified role additions and removals to an NPC via its context.
    /// </summary>
    private void ApplyRoleChanges(NpcContext npcContext, List<NpcRoleSO> rolesToAdd, List<NpcRoleSO> rolesToRemove)
    {
        if (npcContext?.Identity == null) return; // Need identity to change roles

        // Removals first
        foreach (var role in rolesToRemove)
        {
            if (role != null) npcContext.Identity.RemoveDynamicRole(role);
        }
        // Then additions
        foreach (var role in rolesToAdd)
        {
            if (role != null) npcContext.Identity.AddDynamicRole(role);
        }
    }

    // --- Suspicion Tracking Logic ---

    /// <summary>
    /// Starts monitoring an NPC for suspicion based on allowed roles defined in the ZoneDefinitionSO.
    /// Subscribes to the NPC's role change events.
    /// </summary>
    private void StartSuspicionTracking(NpcContext npcContext, bool isInitiallyUnauthorized)
    {
        if (suspicionTrackedNpcs.ContainsKey(npcContext)) return; // Already tracking
         if (npcContext.Identity == null) return; // Cannot track without identity

        var state = new ZoneNpcSuspicionState // Assumes ZoneNpcSuspicionState class exists
        {
            // Identity = npcContext.Identity, // No longer need to store identity separately
            IsCurrentlyUnauthorized = isInitiallyUnauthorized,
            TimeCheckStarted = Time.time,
            CurrentSuspicionTierIndex = -1
        };

        // Create delegates that capture the NpcContext
        state.RoleAddedHandler = (role) => HandleRoleChangeForSpecificNpc(npcContext, role, true);
        state.RoleRemovedHandler = (role) => HandleRoleChangeForSpecificNpc(npcContext, role, false);

        // Subscribe via the Identity component held by the context
        npcContext.Identity.OnRoleAdded += state.RoleAddedHandler;
        npcContext.Identity.OnRoleRemoved += state.RoleRemovedHandler;

        suspicionTrackedNpcs.Add(npcContext, state);
    }

    /// <summary>
    /// Stops monitoring an NPC for suspicion and unsubscribes from role events.
    /// </summary>
    private void StopSuspicionTracking(NpcContext npcContext)
    {
         if (npcContext == null) return;

        if (suspicionTrackedNpcs.TryGetValue(npcContext, out ZoneNpcSuspicionState state))
        {
            // Unsubscribe using stored delegates (needs Identity)
            if (npcContext.Identity != null) {
                if (state.RoleAddedHandler != null)
                    npcContext.Identity.OnRoleAdded -= state.RoleAddedHandler;
                if (state.RoleRemovedHandler != null)
                    npcContext.Identity.OnRoleRemoved -= state.RoleRemovedHandler;
            }

            suspicionTrackedNpcs.Remove(npcContext);
        }
    }

    /// <summary>
    /// Handles role changes for NPCs currently tracked for suspicion.
    /// Re-evaluates authorization status based on ZoneDefinitionSO and resets tracking time if needed.
    /// </summary>
    private void HandleRoleChangeForSpecificNpc(NpcContext npcContext, NpcRoleSO changedRole, bool wasAdded)
    {
         // Check if we are still tracking this NPC and have necessary components/definition
        if (zoneDefinition == null || npcContext?.Identity == null || !suspicionTrackedNpcs.TryGetValue(npcContext, out ZoneNpcSuspicionState state))
        {
            return;
        }

        // Only need to re-evaluate if the changed role is one of the 'allowedRoles' from the definition
        if (!zoneDefinition.AllowedRoles.Contains(changedRole))
        {
            return;
        }

        bool isNowUnauthorized = !npcContext.Identity.HasAnyRole(zoneDefinition.AllowedRoles);

        // Check if authorization status changed
        if (isNowUnauthorized != state.IsCurrentlyUnauthorized)
        {
            state.IsCurrentlyUnauthorized = isNowUnauthorized;
            state.TimeCheckStarted = Time.time;
            state.CurrentSuspicionTierIndex = -1;

             // If they just became authorized, explicitly remove suspicion source from this zone
             if (!isNowUnauthorized && npcContext.SuspicionTracker != null) {
                npcContext.SuspicionTracker.RemoveSuspicionSource(gameObject.name);
             }
        }
    }

    /// <summary>
    /// Called every frame to update suspicion levels for unauthorized NPCs based on ZoneDefinitionSO.
    /// </summary>
    protected virtual void Update()
    {
        // Need definition and tiers to apply suspicion
        if (zoneDefinition == null || sortedSuspicionTiers.Count == 0) return;

        float currentTime = Time.time;

        // Iterate through tracked NPCs
        foreach (var kvp in suspicionTrackedNpcs) // KeyValuePair<NpcContext, ZoneNpcSuspicionState>
        {
            NpcContext npcContext = kvp.Key;
            ZoneNpcSuspicionState state = kvp.Value;

            // Ensure context and required components are valid
            if (npcContext?.SuspicionTracker == null) continue; // Need suspicion tracker
            if (!state.IsCurrentlyUnauthorized) continue;      // Skip if currently authorized

            float timeSpentUnauthorized = currentTime - state.TimeCheckStarted;
            int targetTierIndex = -1;

            // Find the highest applicable tier using the sorted list
            for (int i = 0; i < sortedSuspicionTiers.Count; i++)
            {
                if (timeSpentUnauthorized >= sortedSuspicionTiers[i].Delay)
                {
                    targetTierIndex = i;
                }
                else
                {
                    break; // Tiers are sorted by delay
                }
            }

            // Apply suspicion if a tier is applicable
            if (targetTierIndex > -1)
            {
                SuspicionTier currentTier = sortedSuspicionTiers[targetTierIndex];
                // Continuously add/refresh the source
                 npcContext.SuspicionTracker.AddSuspicionSource(
                    gameObject.name, // Use zone GameObject name as source identifier
                    currentTier.SuspicionLevel,
                    currentTier.RemovalDuration
                );
                 state.CurrentSuspicionTierIndex = targetTierIndex; // Update tracked tier
            } else {
                // If no tier applies currently, ensure index is reset
                state.CurrentSuspicionTierIndex = -1;
                // If they just dropped below threshold, explicitly remove suspicion source
                npcContext.SuspicionTracker.RemoveSuspicionSource(gameObject.name);
            }
        }
    }
    
    /// <summary>
    /// Samples a random point within one of the zone's managed colliders.
    /// The selection of a collider is weighted by its calculated world-space volume.
    /// Assumes managed colliders are either BoxColliders or SphereColliders.
    /// For BoxColliders, samples within the Oriented Bounding Box (OBB).
    /// For SphereColliders, samples within the sphere (which becomes an ellipsoid if non-uniformly scaled).
    /// </summary>
    /// <returns>A random Vector3 point in world space. Returns the zone's transform position if no suitable colliders are found, they have no volume, or an error occurs.</returns>
    public Vector3 GetRandomPointInZone()
    {
        // Ensure there are colliders to work with.
        if (managedColliders == null || managedColliders.Count == 0)
        {
            Debug.LogWarning($"Zone '{gameObject.name}' has no managed colliders to sample a point from. Returning zone's transform position.", this);
            return transform.position; // Fallback to the zone's own origin.
        }

        List<float> colliderVolumes = new List<float>(managedColliders.Count);
        float totalVolume = 0f;

        // Calculate the effective volume of each managed collider.
        foreach (Collider col in managedColliders)
        {
            // Skip any colliders that are not enabled.
            if (col == null || !col.enabled)
            {
                colliderVolumes.Add(0f); // Add a placeholder zero volume for disabled/null colliders.
                continue;
            }

            float volume = 0f;
            Transform colTransform = col.transform; // Cache transform for convenience.

            if (col is BoxCollider boxCollider)
            {
                // Calculate the world-space scaled size of the box.
                // The volume is the product of its world-scaled dimensions.
                Vector3 worldScaledSize = Vector3.Scale(boxCollider.size, colTransform.lossyScale);
                volume = Mathf.Abs(worldScaledSize.x * worldScaledSize.y * worldScaledSize.z);
            }
            else if (col is SphereCollider sphereCollider)
            {
                // A sphere scaled non-uniformly becomes an ellipsoid.
                // The volume of an ellipsoid with semi-axes a, b, c is (4/3) * pi * a * b * c.
                // Here, a = radius * |scale.x|, b = radius * |scale.y|, c = radius * |scale.z|.
                Vector3 lossyScale = colTransform.lossyScale;
                float r = sphereCollider.radius;
                volume = (4f/3f) * Mathf.PI * Mathf.Abs(r * lossyScale.x) * Mathf.Abs(r * lossyScale.y) * Mathf.Abs(r * lossyScale.z);
            }
            else
            {
                // Log a warning for unsupported collider types, their volume will be treated as 0 for selection.
                Debug.LogWarning($"Collider '{col.name}' on '{gameObject.name}' of type '{col.GetType()}' is not supported for volume calculation in GetRandomPointInZone. It will not be weighted for selection.", col);
                volume = 0f;
            }
            colliderVolumes.Add(volume);
            totalVolume += volume;
        }

        // If total volume is zero or less, proportional selection is not possible.
        if (totalVolume <= 0f)
        {
            Debug.LogWarning($"Zone '{gameObject.name}' managed colliders have a total calculable volume of zero or less. Cannot sample a point proportionally. Returning zone's transform position.", this);
            // As a fallback, try to return the center of the first enabled managed collider.
            foreach(Collider col in managedColliders) {
                if (col != null && col.enabled) return col.bounds.center;
            }
            return transform.position; // Absolute fallback to the zone's own origin.
        }

        // Select a collider based on weighted random selection (proportional to its volume).
        float randomValue = UnityEngine.Random.Range(0f, totalVolume);
        float cumulativeVolume = 0f;
        Collider selectedCollider = null;

        for (int i = 0; i < managedColliders.Count; i++)
        {
            // Skip colliders that contributed no volume (e.g., disabled, unsupported, or genuinely zero-volume).
            if (colliderVolumes[i] <= 0f) continue;

            cumulativeVolume += colliderVolumes[i];
            if (randomValue <= cumulativeVolume)
            {
                selectedCollider = managedColliders[i];
                break;
            }
        }
        
        // Fallback: if no collider was selected (e.g., due to floating point nuances with randomValue == totalVolume),
        // attempt to pick the last valid collider that contributed to totalVolume.
        if (selectedCollider == null)
        {
            for (int i = managedColliders.Count - 1; i >= 0; i--)
            {
                if (colliderVolumes[i] > 0f && managedColliders[i] != null && managedColliders[i].enabled)
                {
                    selectedCollider = managedColliders[i];
                    Debug.LogWarning($"GetRandomPointInZone on '{gameObject.name}' used fallback collider selection for index {i}.", this);
                    break;
                }
            }
        }

        // If still no collider is selected, something went wrong.
        if (selectedCollider == null)
        {
            Debug.LogError($"Zone '{gameObject.name}': Failed to select a collider for random point generation despite a positive total volume ({totalVolume}). Returning zone's transform position.", this);
            return transform.position;
        }

        // Sample a random point within the bounds of the selected collider.
        Transform selectedTransform = selectedCollider.transform; // Cache transform.

        if (selectedCollider is BoxCollider selectedBox)
        {
            // Sample a point in the local space of the box collider (within its oriented bounding box - OBB).
            // selectedBox.center is the offset of the box's center from its transform's origin, in local space.
            // selectedBox.size is its dimensions in local space.
            Vector3 localPoint = new Vector3(
                UnityEngine.Random.Range(-selectedBox.size.x * 0.5f, selectedBox.size.x * 0.5f),
                UnityEngine.Random.Range(-selectedBox.size.y * 0.5f, selectedBox.size.y * 0.5f),
                UnityEngine.Random.Range(-selectedBox.size.z * 0.5f, selectedBox.size.z * 0.5f)
            );
            // Combine the local center with the random local point, then transform to world space.
            return selectedTransform.TransformPoint(selectedBox.center + localPoint);
        }
        else if (selectedCollider is SphereCollider selectedSphere)
        {
            // Sample a point in the local space of the sphere collider.
            // selectedSphere.center is its offset, selectedSphere.radius is its local radius.
            // Random.insideUnitSphere returns a point within a sphere of radius 1, centered at local origin (0,0,0).
            // Scale this by the sphere's radius and add its local center.
            Vector3 localPoint = selectedSphere.center + UnityEngine.Random.insideUnitSphere * selectedSphere.radius;
            // Transform this local point (relative to the sphere's transform) to world space.
            // This correctly handles non-uniform scaling, sampling from the resulting ellipsoid.
            return selectedTransform.TransformPoint(localPoint);
        }

        // This block should ideally not be reached if colliders are guaranteed to be Box or Sphere
        // and were correctly selected based on positive volume.
        Debug.LogError($"Zone '{gameObject.name}': Selected collider '{selectedCollider.name}' is of an unexpected type '{selectedCollider.GetType()}' or could not be processed after volume-based selection. Returning its bounds.center.", selectedCollider);
        return selectedCollider.bounds.center;
    }
    
    /// <summary>
    /// Checks if the given world-space point is inside any of the zone's enabled managed colliders.
    /// This method relies on Collider.ClosestPoint(), which works reliably for convex colliders like BoxColliders and SphereColliders.
    /// </summary>
    /// <param name="worldPoint">The world-space point to check.</param>
    /// <returns>True if the point is inside any enabled managed collider, false otherwise (including if there are no colliders or all are disabled).</returns>
    public bool IsPointInsideZone(Vector3 worldPoint)
    {
        // If there are no colliders defined for this zone, the point cannot be inside.
        if (managedColliders == null || managedColliders.Count == 0)
        {
            return false;
        }

        foreach (Collider col in managedColliders)
        {
            // Skip any null entries or colliders that are currently disabled.
            // Disabled colliders should not be considered part of the active zone area.
            if (col == null || !col.enabled)
            {
                continue;
            }

            // Collider.ClosestPoint(point) returns the closest point on the collider to the given 'point'.
            // If the 'point' itself is inside the collider, ClosestPoint will return the 'point'.
            // The Vector3 == operator in Unity uses an approximation, comparing the squared magnitude
            // of the difference between the vectors to a small epsilon, which makes this check robust.
            if (col.ClosestPoint(worldPoint) == worldPoint)
            {
                // The point is inside this collider, so it's within the zone.
                return true;
            }
        }

        // If the point was not found inside any of the enabled managed colliders.
        return false;
    }
}