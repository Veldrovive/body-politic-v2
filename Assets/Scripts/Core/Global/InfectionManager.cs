using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Used in an infection event to specify who was infected.
/// </summary>
public class InfectionData
{
    public NpcContext infectedNpc;
}

public class InfectionManagerSaveableData : SaveableData
{
    public List<GameObject> InfectedNpcs = new List<GameObject>();
}

[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(PlayerManager))]
// public class InfectionManager : GameEventListenerBase<InfectionData, InfectionDataEventSO>
public class InfectionManager : SaveableGOConsumer, IGameEventListener<InfectionData>
{
    [SerializeField] private List<NpcContext> allNpcs = new List<NpcContext>();
    
    [Tooltip("The list of NpcContexts that start infected. The first will be focused on spawn.")]
    [SerializeField] private List<NpcContext> infectedNpcs;

    [Tooltip("If true, infecting an NPC will cause the camera to focus on them.")]
    [SerializeField] private bool focusOnInfection;
    
    [Tooltip("If true, an action camera will be triggered on infection to follow the infected NPC.")]
    [SerializeField] private bool triggerActionCameraOnInfection;
    [SerializeField] private float infectionActionCameraDuration = 5f;
    
    private PlayerManager playerManager;

    public static InfectionManager Instance { get; private set; }
    
    public event Action<NpcContext> OnNpcInfected;
    
    [Tooltip("The event channel to register to.")]
    [SerializeField] protected InfectionDataEventSO gameEvent = default;

    /// <summary>
    /// Called when the component is disabled or destroyed.
    /// Unregisters the listener from the event channel.
    /// </summary>
    protected virtual void OnDisable()
    {
        // Only attempt to unregister if the gameEvent was assigned.
        gameEvent?.UnregisterListener(this);
    }

    void Awake()
    {
        if (Instance != null) {
            Debug.LogError("There is more than one instance!");
            return;
        }

        Instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        playerManager = GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("PlayerManager component not found on this GameObject.", this);
            return;
        }
    }

    public override SaveableData GetSaveData()
    {
        return new InfectionManagerSaveableData
        {
            InfectedNpcs = infectedNpcs.Select(npc => npc.gameObject).ToList()
        };
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            if (data is InfectionManagerSaveableData infectionData)
            {
                infectedNpcs = infectionData.InfectedNpcs.Select(go => go.GetComponent<NpcContext>()).Where(npc => npc != null).ToList();
            }
            else
            {
                Debug.LogError("Invalid save data type for InfectionManager.", this);
            }
        }
        
        // Filter out Npcs that are not enabled
        allNpcs.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy || !npc.gameObject.activeSelf);
        infectedNpcs.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy);
        foreach (var npc in allNpcs)
        {
            npc.OnDeath -= HandleNpcDeath;
            npc.OnDeath += HandleNpcDeath;
        }
        
        if (gameEvent == null)
        {
            InfectionDataEventSO infectionEvent = GlobalData.Instance?.InfectionEvent;
            if (infectionEvent != null)
            {
                gameEvent = infectionEvent;
                // Debug.Log($"Infection Event SO automatically assigned from GlobalData for {this.gameObject.name}.", this);
            }
            else
            {
                Debug.LogError("Infection Event SO not found in GlobalData. Please assign it in the inspector.", this);
            }
        }
        
        // Ensure the gameEvent is assigned before attempting to register.
        if (gameEvent == null)
        {
            Debug.LogError($"GameEvent is not assigned in the inspector for {this.GetType().Name} on GameObject {this.gameObject.name}. Cannot register listener.", this);
            return;
        }
        gameEvent.RegisterListener(this);
        
        // Initialize the player manager with the infected NPCs.
        if (playerManager != null)
        {
            playerManager.SetControllableNpcs(infectedNpcs, true);
        }

        UpdatePlayerIdentity();
    }

    public void UpdatePlayerIdentity()
    {
        // The player identity keeps track of any "Sticky" roles that exist on infected NPCs. Whenever we
        // change the infected NPCs, we iterate through and add all sticky roles to the player identity.
        foreach (var npc in infectedNpcs)
        {
            foreach (var role in npc.Identity.GetAllRoles())
            {
                if (role.Sticky)
                {
                    PlayerIdentityManager.Instance.AddRole(role);
                }
            }
        }
    }
    
    public void OnEventRaised(InfectionData data)
    {
        OnInfection(data.infectedNpc);
    }

    public List<NpcContext> GetNpcsWithAnyRoles(IEnumerable<NpcRoleSO> roles)
    {
        return allNpcs.FindAll(npc => npc.Identity.HasAnyRole(roles));
    }
    
    public bool IsNpcInfected(NpcContext npc)
    {
        // Check if the NPC is in the list of infected NPCs.
        return infectedNpcs.Contains(npc);
    }
    
    public void RegisterNpc(NpcContext npc)
    {
        // Register the NPC in the InfectionManager.
        if (npc == null)
        {
            Debug.LogError("Attempted to register a null NPC.", this);
            return;
        }
        
        npc.OnDeath -= HandleNpcDeath;
        npc.OnDeath += HandleNpcDeath;
        
        if (!allNpcs.Contains(npc))
        {
            allNpcs.Add(npc);
        }
        else
        {
            // Debug.LogWarning($"NPC {npc.name} is already registered in the InfectionManager.", this);
        }
    }
    
    public void NotifyInfection(NpcContext infectedNpc)
    {
        // Notify the event system about the infection.
        OnInfection(infectedNpc);
    }

    private void OnInfection(NpcContext infectedNpc)
    {
        playerManager?.AddControllableNpc(infectedNpc);
        if (focusOnInfection)
        {
            playerManager?.SetFocus(infectedNpc);   
        }
        
        OnNpcInfected?.Invoke(infectedNpc);
        infectedNpc.Identity.RecalculateAllRoles();  // Necessary to ensure the NPC gets the player roles.

        if (ActionCameraManager.Instance != null && triggerActionCameraOnInfection)
        {
            ActionCameraManager.Instance.AddActionCamSource(
                new ActionCamSource("infection", 1, infectedNpc.transform, ActionCameraMode.ThirdPerson, infectionActionCameraDuration)
            );
        }

        UpdatePlayerIdentity();
    }
    
    private void HandleNpcDeath(NpcContext npc)
    {
        // Remove the NPC from the infected list if they die.
        Debug.Log($"NPC {npc.name} has died and will be removed from the infected list.", this);
        if (infectedNpcs.Contains(npc))
        {
            playerManager?.RemoveControllableNpc(npc);
            infectedNpcs.Remove(npc);
            UpdatePlayerIdentity();
        }
    }
}
