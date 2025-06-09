using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdentitySaveableData : SaveableData
{
    public List<NpcRoleSO> DynamicRoles = new List<NpcRoleSO>();
}

[DefaultExecutionOrder(-91)]
public class PlayerIdentityManager : SaveableGOConsumer, IRoleProvider
{
    [SerializeField] private List<NpcRoleSO> DefaultRoles = new List<NpcRoleSO>();
    
    // Dynamic roles hold roles that have been added during gameplay
    private List<NpcRoleSO> dynamicRoles = new List<NpcRoleSO>();
    
    private HashSet<NpcRoleSO> allRolesCache = new HashSet<NpcRoleSO>();
    
    [SerializeField, ReadOnly]
    private List<NpcRoleSO> allRoles = new List<NpcRoleSO>();

    public event Action<NpcRoleSO> OnRoleAdded;
    public event Action<NpcRoleSO> OnRoleRemoved;
    
    public static PlayerIdentityManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            allRolesCache.UnionWith(DefaultRoles);
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Multiple instances of PlayerIdentityManager detected. Destroying duplicate instance.", this);
            Destroy(gameObject);
            return;
        }
    }

    public bool ShouldProvideRoles(NpcContext npcContext)
    {
        if (InfectionManager.Instance == null)
        {
            // Infection is not set up for some reason.
            Debug.LogError("InfectionManager is not initialized. Cannot determine if roles should be provided.");
            return false;
        }

        // Should provide roles if the npc is infected.
        return InfectionManager.Instance.IsNpcInfected(npcContext);
    }

    public override SaveableData GetSaveData()
    {
        PlayerIdentitySaveableData saveData = new PlayerIdentitySaveableData
        {
            DynamicRoles = new List<NpcRoleSO>(dynamicRoles)
        };
        return saveData;
    }

    public override void LoadSaveData(SaveableData data)
    {
        if (data is PlayerIdentitySaveableData identityData)
        {
            dynamicRoles.Clear();
            allRolesCache.Clear();
            allRolesCache.UnionWith(DefaultRoles);
            foreach (var role in identityData.DynamicRoles)
            {
                AddRole(role);
            }
        }
        else
        {
            Debug.LogError("Invalid save data type for PlayerIdentityManager.", this);
        }
    }

    public void AddRole(NpcRoleSO role)
    {
        if (allRolesCache.Add(role))
        {
            if (!role.Sticky)
            {
                Debug.LogWarning($"Added a non-sticky role {role.name} to PlayerIdentityManager.", this);
            }
            
            dynamicRoles.Add(role);
            OnRoleAdded?.Invoke(role);
        }
    }

    public void AddRoles(IEnumerable<NpcRoleSO> roles)
    {
        foreach (var role in roles)
        {
            AddRole(role);
        }
    }
    
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles()
    {
        // Return a read-only collection of all roles, combining default and dynamic roles.
        return new List<NpcRoleSO>(allRolesCache);
    }
    
#if UNITY_EDITOR
    private void Update()
    {
        allRoles.Clear();
        allRoles.AddRange(allRolesCache);
    }
#endif
}