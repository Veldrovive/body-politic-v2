using System;
using UnityEngine;

class PlayerManagerV2 : MonoBehaviour
{
    public static PlayerManagerV2 Instance;
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There is more than one instance of PlayerManagerV2!");
            return;
        }

        Instance = this;
    }
    
    
    private NpcContext _controlledNpc = null;
    public NpcContext ControlledNpc => _controlledNpc;
    public bool CanControlNpc => _controlledNpc != null;
    
    public void SetControlledNpc(NpcContext npc)
    {
        _controlledNpc = npc;
        OnControlledNpcChange?.Invoke(npc);
    }
    public event Action<NpcContext> OnControlledNpcChange;


    /// <summary>
    /// Handles interaction logic when a PlayerControlTrigger is clicked.
    /// Tells the focused NPC's CharacterModeController to switch to Player mode and execute the trigger's step.
    /// </summary>
    public void HandleTriggerInteraction(PlayerControlTrigger clickedTrigger)
    {
        Debug.Log($"PlayerManager: Triggered interaction {clickedTrigger.name}");
    }

    public void HandleWorldInteraction(Vector3 worldPosition)
    {
        Debug.Log($"PlayerManager: World interaction {worldPosition}");
    }
}