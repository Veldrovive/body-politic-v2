using System.Collections.Generic;
using UnityEngine;

[NodeInfo("Say Role Missing", "Event Listener/Say Role Missing")]
public class SayRoleMissingListenerNode : EventListenerNode
{
    [EventInputPort("Say Role Missing")]
    public void HandleRoleMising(List<NpcRoleSO> roles)
    {
        Debug.Log($"SayRoleMissingListenerNode: {roles.Count} roles are missing.");
    }
}