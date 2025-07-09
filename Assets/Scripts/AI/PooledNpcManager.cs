using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEngine;

public class PooledNpcInitContext
{
    public GameObject Npc;
    public Vector3 StartPosition;
    public Quaternion StartRotation;
}

public class PooledNpcManagerSaveableData : SaveableData
{
    public List<PooledNpcInitContext> NpcInitContexts = new List<PooledNpcInitContext>();
}

public class PooledNpcManager : SaveableGOConsumer
{
    // Read-only variable that tells us whether an NPC is allowed to reset at this time
    [SerializeField] private string ReadyForResetVarName = "ReadyForReset";
    // Write-only variable that, when set to true, resets the NPC
    [SerializeField] private string ShouldResetVarName = "ShouldReset";
    
    [SerializeField] private List<NpcContext> npcPool = new List<NpcContext>();
    public IReadOnlyList<NpcContext> NpcPool => npcPool.AsReadOnly();
    
    private Dictionary<NpcContext, PooledNpcInitContext> npcStartStates = new();

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (blankLoad)
        {
            // First, we remove any NPCs that are not enabled
            npcPool.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy);

            // Then we remove any that do not have the required variables. This time we also log a warning
            npcPool.RemoveAll(npc =>
            {
                if (!NpcRoutineHasRequiredVariables(npc))
                {
                    Debug.LogWarning($"NPC {npc.name} does not have the required variables for reset.", this);
                    return true;
                }

                return false;
            });

            // Record the start position and rotation of each NPC in the pool
            foreach (var npc in npcPool)
            {
                if (npc != null)
                {
                    npcStartStates[npc] = new PooledNpcInitContext
                    {
                        Npc = npc.gameObject,
                        StartPosition = npc.transform.position,
                        StartRotation = npc.transform.rotation
                    };
                }
            }
        }
        else
        {
            // Otherwise we recover the pool and start states from the save data
            if (data is not PooledNpcManagerSaveableData npcData)
            {
                Debug.LogError("Invalid save data for PooledNpcManager. Expected PooledNpcManagerSaveableData.", this);
                return;
            }
            
            npcPool.Clear();
            npcStartStates.Clear();
            foreach (var initContext in npcData.NpcInitContexts)
            {
                if (initContext.Npc != null)
                {
                    // Find the NpcContext for the GameObject
                    NpcContext npcContext = initContext.Npc.GetComponent<NpcContext>();
                    if (npcContext != null)
                    {
                        npcPool.Add(npcContext);
                        npcStartStates[npcContext] = initContext;
                    }
                    else
                    {
                        Debug.LogWarning($"GameObject {initContext.Npc.name} does not have an NpcContext component.", this);
                    }
                }
                else
                {
                    Debug.LogWarning("Found a null GameObject in the saved NPC contexts.", this);
                }
            }
        }
    }

    public override SaveableData GetSaveData()
    {
        // Create a new save data object
        PooledNpcManagerSaveableData saveData = new PooledNpcManagerSaveableData();
        
        // Populate it with the current NPC pool and their start states
        foreach (var npc in npcPool)
        {
            if (npc != null && npcStartStates.TryGetValue(npc, out var initContext))
            {
                saveData.NpcInitContexts.Add(new PooledNpcInitContext
                {
                    Npc = npc.gameObject,
                    StartPosition = initContext.StartPosition,
                    StartRotation = initContext.StartRotation
                });
            }
        }
        
        return saveData;
    }

    private bool NpcRoutineHasRequiredVariables(NpcContext context)
    {
        BehaviorController controller = context.BehaviorController;
        BehaviorGraphAgent routineAgent = controller.RoutineBehavior;
        
        if (routineAgent == null)
        {
            return false;
        }
        
        // We can check if a variable exists by checking if routineAgent.GetVariableID() returns true
        bool hasReadyForReset = routineAgent.GetVariableID(ReadyForResetVarName, out var id);
        bool hasShouldReset = routineAgent.GetVariableID(ShouldResetVarName, out var id2);
        if (!hasReadyForReset)
        {
            Debug.LogWarning($"NPC {context.name} does not have the variable '{ReadyForResetVarName}' in its routine.", this);
        }
        if (!hasShouldReset)
        {
            Debug.LogWarning($"NPC {context.name} does not have the variable '{ShouldResetVarName}' in its routine.", this);
        }
        return hasReadyForReset && hasShouldReset;
    }
    
    public bool IsNpcReadyForReset(NpcContext context)
    {
        if (context == null || context.BehaviorController == null)
        {
            return false;
        }
        
        BehaviorGraphAgent routineAgent = context.BehaviorController.RoutineBehavior;
        if (routineAgent == null)
        {
            return false;
        }
        
        // Check the variable
        routineAgent.GetVariable(ReadyForResetVarName, out var isReady);
        return isReady.ObjectValue as bool? == true;
    }
    
    public bool TryResetNpc(NpcContext context)
    {
        if (context == null || context.BehaviorController == null)
        {
            return false;
        }
        
        if (!IsNpcReadyForReset(context))
        {
            return false;
        }
        
        // Move the NPC back to its start position and rotation
        context.MovementManager.WarpToPosition(
            npcStartStates[context].StartPosition, 
            npcStartStates[context].StartRotation
        );
        
        // Set the ShouldReset variable to true
        BehaviorGraphAgent routineAgent = context.BehaviorController.RoutineBehavior;
        if (routineAgent == null)
        {
            return false;
        }
        routineAgent.SetVariableValue(ShouldResetVarName, true);
        
        BehaviorController controller = context.BehaviorController;
        controller.IdleOnExit = false;
        controller.ClearAll();  // Returns the NPC to its routine state
        routineAgent.Restart();

        return true;
    }

    public void ResetAllNpcs()
    {
        // Resets all NPCs in the pool. If any NPC is not ready for reset, it will be skipped.
        foreach (var npc in npcPool)
        {
            if (npc != null && IsNpcReadyForReset(npc))
            {
                TryResetNpc(npc);
            }
            else
            {
                Debug.LogWarning($"NPC {npc?.name} is not ready for reset or is null.", this);
            }
        }
    }
}