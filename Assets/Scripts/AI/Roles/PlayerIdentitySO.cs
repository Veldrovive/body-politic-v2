using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

[CreateAssetMenu(fileName = "PlayerIdentitySO", menuName = "Body Politic/Player Roles")]
public class PlayerIdentitySO : ScriptableObject, IRoleProvider
{
    // ******** Unity Inspector Variables ********
    [Tooltip("The roles that the player starts with.")]
    [SerializeField] private List<NpcRoleSO> DefaultRoles = new();
    // ******** END Unity Inspector Variables ********

    // ******** Internal Variables ********
    /// <summary>
    /// The roles that have been added to this player dynamically.
    /// </summary>
    private HashSet<NpcRoleSO> dynamicRoles = new();
    /// <summary>
    /// Cached list of all current roles for efficient retrieval.
    /// </summary>
    private List<NpcRoleSO> cachedCurrentRoles = null;
    /// <summary>
    /// Flag indicating if the cache needs rebuilding.
    /// </summary>
    private bool isCacheDirty = true;

    // ******** Events ********
    /// <summary>
    /// Fired when a specific role is added dynamically.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleAdded;
    /// <summary>
    /// Fired when a specific role is removed dynamically.
    /// </summary>
    public event Action<NpcRoleSO> OnRoleRemoved;
    /// <summary>
    /// Fired when the provider's data changes in a way that requires consumers to refresh
    /// (e.g., DefaultRoles modified in editor during runtime).
    /// </summary>
    public event Action OnProviderDataChanged;
    // ******** END Events ********


    // ******** Public Methods ********
    /// <summary>
    /// Gets the current roles provided by the player identity.
    /// Combines default and dynamic roles. The returned collection is read-only.
    /// </summary>
    /// <returns>A read-only collection of the current NpcRoleSO.</returns>
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles()
    {
        // Rebuild cache if it's marked as dirty
        if (isCacheDirty || cachedCurrentRoles == null)
        {
            // Use HashSet temporarily to handle potential duplicates between default and dynamic
            HashSet<NpcRoleSO> allRolesSet = new HashSet<NpcRoleSO>(DefaultRoles);
            allRolesSet.UnionWith(dynamicRoles); // Add dynamic roles efficiently
            cachedCurrentRoles = allRolesSet.ToList();
            isCacheDirty = false;
            // Debug.Log($"[PlayerIdentitySO] Cache rebuilt. Count: {cachedCurrentRoles.Count}"); // Optional debug log
        }
        // Return the cached list (which is implicitly read-only via the interface)
        return cachedCurrentRoles;
    }

    /// <summary>
    /// Adds a role dynamically to the player identity.
    /// </summary>
    /// <param name="role">The role to add.</param>
    /// <returns>True if the role was added, false if it was already present.</returns>
    public bool AddPlayerRole(NpcRoleSO role)
    {
        // Check if the role is already in the list of added roles or default roles.
        if (role == null || dynamicRoles.Contains(role) || DefaultRoles.Contains(role))
        {
            return false;
        }

        // Add the role to the list of dynamic roles.
        if (dynamicRoles.Add(role))
        {
            isCacheDirty = true; // Mark cache for rebuild
            OnRoleAdded?.Invoke(role);
            // Also notify general change as the output of GetCurrentRoles() will change
            OnProviderDataChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a role dynamically added to the player identity.
    /// Note: Default roles cannot be removed this way.
    /// </summary>
    /// <param name="role">The role to remove.</param>
    /// <returns>True if the role was removed, false if it was not found in the dynamic roles.</returns>
    public bool RemovePlayerRole(NpcRoleSO role)
    {
         // Check if the role is in the list of added roles.
        if (role != null && dynamicRoles.Remove(role))
        {
            isCacheDirty = true; // Mark cache for rebuild
            OnRoleRemoved?.Invoke(role);
            // Also notify general change as the output of GetCurrentRoles() will change
            OnProviderDataChanged?.Invoke();
            return true;
        }

        // Role was null or not found in dynamicRoles
        return false;
    }

    // ******** Editor Helper Methods ********

#if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Called by the custom editor when DefaultRoles are changed during runtime.
    /// Invalidates the cache and notifies listeners that a refresh is needed.
    /// </summary>
    public void EditorNotifyDefaultRolesChanged()
    {
        if (!Application.isPlaying) return; // Should only be used in play mode

        // Debug.Log("[PlayerIdentitySO] DefaultRoles changed via Inspector during runtime."); // Optional debug log
        isCacheDirty = true; // Ensure cache rebuilds on next GetCurrentRoles()
        OnProviderDataChanged?.Invoke(); // Notify listeners (like NPCIdentity)
    }
#endif

    // ******** END Editor Helper Methods ********
}