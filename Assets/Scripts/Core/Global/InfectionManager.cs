using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used in an infection event to specify who was infected.
/// </summary>
public class InfectionData
{
    public NpcContext infectedNpc;
}

[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(PlayerManager))]
public class InfectionManager : GameEventListenerBase<InfectionData, InfectionDataEventSO>
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

    void Awake()
    {
        if (Instance != null) {
            Debug.LogError("There is more than one instance!");
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        playerManager = GetComponent<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("PlayerManager component not found on this GameObject.", this);
            return;
        }
    }

    protected override void Start()
    {
        // Filter out Npcs that are not enabled
        allNpcs.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy || !npc.gameObject.activeSelf);
        infectedNpcs.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy);
        
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

        // Call the base Start method AFTER potentially assigning gameEvent.
        // This ensures registration happens correctly.
        base.Start();

        // Initialize the player manager with the infected NPCs.
        if (playerManager != null)
        {
            playerManager.SetControllableNpcs(infectedNpcs, true);
        }
    }

    protected override void HandleEventRaised(InfectionData data)
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
        
        if (!allNpcs.Contains(npc))
        {
            allNpcs.Add(npc);
        }
        else
        {
            Debug.LogWarning($"NPC {npc.name} is already registered in the InfectionManager.", this);
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

        if (ActionCameraManager.Instance != null && triggerActionCameraOnInfection)
        {
            ActionCameraManager.Instance.AddActionCamSource(
                new ActionCamSource("infection", 1, infectedNpc.transform, ActionCameraMode.ThirdPerson, infectionActionCameraDuration)
            );
        }
    }
}
