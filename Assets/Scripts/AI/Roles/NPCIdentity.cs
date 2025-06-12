using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using Sisus.ComponentNames; // Required for ReadOnlyDictionary

public class NpcIdentitySaveableData : SaveableData
{
    public List<NpcRoleSO> DynamicRoles;
}

public class NPCIdentity : SaveableGOConsumer, IRoleProvider
{
    // ******** Unity Inspector Variables ********
    /// <summary>
    /// The default roles that this NPC starts with. For now, these roles cannot be removed. If in the future I decide that it would be better if they
    /// can be removed, we will change this so that these are added to the dynamic roles on startup.
    /// </summary>
    [Tooltip("The roles that this NPC starts with.")]
    [SerializeField] private List<NpcRoleSO> DefaultRoles = new();

    /// <summary>
    /// Add other components/ScriptableObjects that implement IRoleProvider here.
    /// References PlayerIdentity automatically if available via the singleton.
    /// </summary>
    [Tooltip("Add other components/ScriptableObjects that implement IRoleProvider here.")]
    [SerializeField] private List<UnityEngine.Object> AdditionalRoleProviderObjects = new();

    // ******** END Unity Inspector Variables ********

    // ******** Internal Variables ********
    /// <summary>
    /// The roles that have been added to this NPC dynamically (Not from providers).
    /// </summary>
    private HashSet<NpcRoleSO> dynamicRoles = new();

    /// <summary>
    /// Internal collections for efficient lookup, rebuilt on any relevant change.
    /// </summary>
    private Dictionary<RoleType, HashSet<NpcRoleSO>> rolesByType = new();
    private HashSet<NpcRoleSO> currentRolesInternal = new();

    /// <summary>
    /// Cached list representation of currentRolesInternal for efficient read access by external systems.
    /// </summary>
    private List<NpcRoleSO> cachedCurrentRoles = new();

    /// <summary>
    /// List of resolved IRoleProvider interfaces from various sources (self, player, additional).
    /// </summary>
    private List<IRoleProvider> internalRoleProviders = new();

    /// <summary>
    /// Roles are assigned weights. The primary role is the role with the highest weight.
    /// This is used to determine what role to show the user in the UI.
    /// </summary>
    private NpcRoleSO primaryRole;
    public NpcRoleSO PrimaryRole => primaryRole;

    private NpcContext npcContext;

    // ******** Events ********
    /// <summary>
    /// Fired when a specific role is added to the final aggregated set for this NPC.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleAdded;
    /// <summary>
    /// Fired when a specific role is removed from the final aggregated set for this NPC.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleRemoved;
    // ******** END Events ********


    // ******** END Internal Variables ********

    /// <summary>
    /// Gets the save data for this object.
    /// </summary>
    /// <returns>The save data.</returns>
    public override SaveableData GetSaveData()
    {
        return new NpcIdentitySaveableData()
        {
            DynamicRoles = dynamicRoles.ToList() // Convert HashSet to List for serialization
        };
    }

    /// <summary>
    /// Sets the save data for this object.
    /// </summary>
    /// <param name="data">The save data to set.</param>
    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            if (data is not NpcIdentitySaveableData npcData)
            {
                Debug.LogWarning($"Invalid save data type for {gameObject.name}. Expected NpcIdentitySaveableData.", this);
                return;
            }

            dynamicRoles.Clear();
            if (data != null && npcData.DynamicRoles != null)
            {
                foreach (NpcRoleSO role in npcData.DynamicRoles)
                {
                    if (role != null)
                    {
                        dynamicRoles.Add(role);
                    }
                    else
                    {
                        Debug.LogWarning($"Null role found in saved data for {gameObject.name}. Skipping.", this);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"No valid dynamic roles found in saved data for {gameObject.name}.", this);
            }
        }
        // Clear list in case Awake runs multiple times (e.g. editor domain reload)
        internalRoleProviders.Clear();

        // Add self as a provider for dynamic roles
        internalRoleProviders.Add(this);
        
        // Add the PlayerIdentityManager from the singleton instance if available.
        if (PlayerIdentityManager.Instance != null)
        {
            internalRoleProviders.Add(PlayerIdentityManager.Instance);
        }
        else
        {
            Debug.LogWarning($"PlayerIdentityManager not found on {gameObject.name}. Player roles will be missing.", this);
        }
        
        NpcContext npcContext = GetComponent<NpcContext>();
        if (npcContext.Inventory != null)
        {
            internalRoleProviders.Add(npcContext.Inventory);
        }
        else
        {
            Debug.LogWarning($"NpcInventory not found on {gameObject.name}. Inventory roles will be missing.", this);
        }

        // Add the additional role providers specified in the inspector.
        foreach (UnityEngine.Object providerObj in AdditionalRoleProviderObjects)
        {
            if (providerObj is IRoleProvider provider && provider != null)
            {
                // Prevent adding self again if accidentally added to the list
                if (!ReferenceEquals(provider, this))
                {
                    internalRoleProviders.Add(provider);
                }
            }
            else if (providerObj != null)
            {
                Debug.LogWarning($"Object '{providerObj.name}' in AdditionalRoleProviders on {gameObject.name} does not implement IRoleProvider.", this);
            }
        }

        // Hook up the events for the role providers.
        foreach (IRoleProvider provider in internalRoleProviders)
        {
            // Don't subscribe to self's IRoleProvider events
            if (ReferenceEquals(provider, this)) continue;

            provider.OnRoleAdded += HandleProviderRoleAdded;
            provider.OnRoleRemoved += HandleProviderRoleRemoved;
        }
        
        // Recalculate roles after setting the save data
        RecalculateAllRoles();
    }

    // ******** Lifecycle Methods ********
    private void Awake()
    {
        npcContext = gameObject.GetComponent<NpcContext>();
    }

    /// <summary>
    /// Unsubscribe from role change events to prevent memory leaks.
    /// </summary>
    void OnDestroy()
    {
        // Unhook the events for the role providers.
        foreach (IRoleProvider provider in internalRoleProviders)
        {
             if (ReferenceEquals(provider, this)) continue;

             provider.OnRoleAdded -= HandleProviderRoleAdded;
             provider.OnRoleRemoved -= HandleProviderRoleRemoved;
        }
        internalRoleProviders.Clear();
    }

    // ******** Public Methods ********

    // --- IRoleProvider Implementation ---
    /// <summary>
    /// Gets the roles provided *directly* by this NPCIdentity component (dynamic roles).
    /// </summary>
    IReadOnlyCollection<NpcRoleSO> IRoleProvider.GetCurrentRoles()
    {
        return dynamicRoles;
    }
    // --- End IRoleProvider Implementation ---


    /// <summary>
    /// Gets the current aggregated roles of the NPC from all sources.
    /// </summary>
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles()
    {
        return cachedCurrentRoles;
    }

    /// <summary>
    /// Alias for GetCurrentRoles.
    /// </summary>
    public IReadOnlyCollection<NpcRoleSO> GetAllRoles()
    {
        return GetCurrentRoles();
    }

    /// <summary>
    /// Gets roles of a specific type present on this NPC.
    /// </summary>
    public IEnumerable<NpcRoleSO> GetRolesByType(RoleType type)
    {
        if (rolesByType.TryGetValue(type, out HashSet<NpcRoleSO> roles))
        {
            return roles;
        }
        return Enumerable.Empty<NpcRoleSO>();
    }

    /// <summary>
    /// Checks if the NPC currently possesses a specific role.
    /// </summary>
    public bool HasRole(NpcRoleSO role)
    {
        return currentRolesInternal.Contains(role);
    }

    /// <summary>
    /// Checks if the NPC currently possesses at least one of the specified roles.
    /// </summary>
    public bool HasAnyRole(IEnumerable<NpcRoleSO> rolesToCheck)
    {
        if (rolesToCheck == null || !rolesToCheck.Any()) return false;
        return currentRolesInternal.Overlaps(rolesToCheck);
    }

    /// <summary>
    /// Checks if the NPC currently possesses all of the specified roles.
    /// </summary>
    public bool HasAllRoles(IEnumerable<NpcRoleSO> rolesToCheck)
    {
        if (rolesToCheck == null || !rolesToCheck.Any()) return true;
        return currentRolesInternal.IsSupersetOf(rolesToCheck);
    }

    /// <summary>
    /// Adds a role directly to this NPC's dynamic set.
    /// </summary>
    public bool AddDynamicRole(NpcRoleSO role)
    {
        if (role != null && dynamicRoles.Add(role))
        {
            HandleRoleChange(role, true); // Triggers recalculation and events
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a role from this NPC's dynamic set.
    /// </summary>
    public bool RemoveDynamicRole(NpcRoleSO role)
    {
        if (role != null && dynamicRoles.Remove(role))
        {
            HandleRoleChange(role, false); // Triggers recalculation and events
            return true;
        }
        return false;
    }

    // ******** END Public Methods ********

    // ******** Editor Helper Methods ********

#if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Gets the current roles grouped by type for debugging purposes.
    /// </summary>
    public IReadOnlyDictionary<RoleType, HashSet<NpcRoleSO>> GetDebugRolesByType()
    {
        if (!Application.isPlaying) return null;
        return new ReadOnlyDictionary<RoleType, HashSet<NpcRoleSO>>(rolesByType);
    }

    /// <summary>
    /// [EDITOR ONLY] Called by the custom editor when DefaultRoles are changed during runtime.
    /// Triggers a recalculation of aggregated roles.
    /// </summary>
    public void EditorNotifyDefaultRolesChanged()
    {
        if (!Application.isPlaying) return; // Should only be used in play mode
        // Debug.Log($"[{gameObject.name}] DefaultRoles changed via Inspector during runtime."); // Optional debug log
        RecalculateAllRoles();
    }
#endif

    // ******** END Editor Helper Methods ********


    // ******** Helper Methods ********

    /// <summary>
    /// Helper to add a role to internal collections during rebuild.
    /// </summary>
    private void AddRoleToInternalCollections(NpcRoleSO role)
    {
        if (role == null) return;

        RoleType roleType = role.RoleType;
        currentRolesInternal.Add(role);

        if (!rolesByType.ContainsKey(roleType))
        {
            rolesByType[roleType] = new HashSet<NpcRoleSO>();
        }
        rolesByType[roleType].Add(role);

        // Check if we should overwrite the primary role
        // Debug.Log($"[{gameObject.name}] Adding role: {role.RoleName} (Weight: {role.RoleWeight}). Old Primary: {primaryRole?.RoleName} (Weight: {primaryRole?.RoleWeight})"); // Optional debug log
        if (primaryRole == null || role.RoleWeight > primaryRole.RoleWeight)
        {
            primaryRole = role;
        }
    }

    /// <summary>
    /// Central method to recalculate all aggregated roles from all providers.
    /// Updates internal collections, cache, and raises OnRoleAdded/OnRoleRemoved events for actual changes.
    /// </summary>
    private void RecalculateAllRoles()
    {
        primaryRole = null; // Reset primary role
        HashSet<NpcRoleSO> previousRoles = new HashSet<NpcRoleSO>(currentRolesInternal);

        currentRolesInternal.Clear();
        rolesByType.Clear();

        // 1. Add default roles from this component
        foreach (NpcRoleSO role in DefaultRoles)
        {
            AddRoleToInternalCollections(role);
        }

        // 2. Add roles from all registered providers (includes dynamic roles via 'this')
        foreach (IRoleProvider provider in internalRoleProviders)
        {
            if (provider == null) continue; // Skip null providers if any slipped through

            if (!provider.ShouldProvideRoles(npcContext)) continue;

            // Add dynamic roles provided by this component itself
            if (ReferenceEquals(provider, this))
            {
                foreach (NpcRoleSO role in dynamicRoles) // Directly iterate dynamic roles
                {
                    AddRoleToInternalCollections(role);
                }
            }
            // Add roles from external providers
            else
            {
                IReadOnlyCollection<NpcRoleSO> providerRoles = provider.GetCurrentRoles();
                if (providerRoles != null)
                {
                    foreach (NpcRoleSO role in providerRoles)
                    {
                        AddRoleToInternalCollections(role);
                    }
                }
            }
        }

        // 3. Update the cached list
        cachedCurrentRoles = currentRolesInternal.ToList();
        // Debug.Log($"[{gameObject.name}] Roles recalculated. Count: {cachedCurrentRoles.Count}"); // Optional debug log

        // 4. Determine changes and raise events
        foreach (var role in currentRolesInternal.Except(previousRoles).ToList()) // Use ToList to prevent modification during iteration issues
        {
            OnRoleAdded?.Invoke(role);

            if (
                PlayerIdentityManager.Instance != null &&
                PlayerIdentityManager.Instance.ShouldProvideRoles(npcContext) &&
                role.Sticky
            )
            {
                // If the PlayerIdentityManager is providing roles, we also reciprocally it when we get a role.
                PlayerIdentityManager.Instance.AddRole(role);
            }
        }
        foreach (var role in previousRoles.Except(currentRolesInternal).ToList()) // Use ToList here too
        {
            OnRoleRemoved?.Invoke(role);
        }

        // Change the name to reflect the primary role if it exists
        if (primaryRole != null)
        {
            this.SetName($"NPC Identity ({primaryRole.RoleName})");
        }
    }


    /// <summary>
    /// Handles role changes initiated directly on this component (Add/RemoveDynamicRole).
    /// </summary>
    private void HandleRoleChange(NpcRoleSO role, bool wasAdded)
    {
        RecalculateAllRoles();
    }

    /// <summary>
    /// Handles the event when an external provider adds a role.
    /// </summary>
    private void HandleProviderRoleAdded(NpcRoleSO role)
    {
        RecalculateAllRoles();
    }

    /// <summary>
    /// Handles the event when an external provider removes a role.
    /// </summary>
    private void HandleProviderRoleRemoved(NpcRoleSO role)
    {
        RecalculateAllRoles();
    }

    // ******** END Helper Methods ********
}