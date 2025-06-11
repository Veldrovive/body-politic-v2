using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// InteractionReactor that checks if the player has a specific role. Conditionally adds new roles to the player,
/// sets their suspicion level, and exposes roleCheckSuccess and roleCheckFailure events.


public class RoleChecker : AbstractInteractionReactor
{
    [SerializeField] private InteractionLifecycleEvent interactionLifecycleTrigger;
    [SerializeField] private InteractionDefinitionSO targetInteractionDefinition;

    [SerializeField] private bool infectedDisallowed = false;
    [SerializeField] private List<NpcRoleSO> allowedRoles;
    [SerializeField] private List<NpcRoleSO> disallowedRoles;
    // If an NPC has both allowed and disallowed roles, they are taken as allowed.
    // If allowedRoles is empty, all roles except disallowedRoles are allowed.
    // If disallowedRoles is empty, no roles are allowed except those in allowedRoles.
    
    [SerializeField] private List<NpcRoleSO> rolesToAddOnSuccess;
    [SerializeField] private List<NpcRoleSO> rolesToAddOnFailure;
    
    [SerializeField] private List<NpcRoleSO> rolesToRemoveOnSuccess;
    [SerializeField] private List<NpcRoleSO> rolesToRemoveOnFailure;
    
    [SerializeField] private int disallowedSuspicionLevel = 0;
    [SerializeField] private float disallowedSuspicionTime = 0f;

    public UnityEvent<NpcContext> roleCheckSuccess;
    public UnityEvent<NpcContext> roleCheckFailure;

    private void Initialize()
    {
        if (targetInteractionDefinition == null)
        {
            Debug.LogWarning("RoleChecker requires a target interaction definition.", this);
            return;
        }

        if (!HasInteractionInstanceFor(targetInteractionDefinition))
        {
            Debug.LogWarning($"RoleChecker requires an interaction instance for {targetInteractionDefinition.name}.", this);
            return;
        }

        SafelyRegisterInteractionLifecycleCallback(
            interactionLifecycleTrigger, targetInteractionDefinition,
            HandleInteractionEnd
        );
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        base.LoadSaveData(data, blankLoad);
        Initialize();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        Initialize();
    }

    private void SignalAllowed(NpcContext context)
    {
        roleCheckSuccess?.Invoke(context);
        foreach (var role in rolesToAddOnSuccess)
        {
            context.Identity.AddDynamicRole(role);
        }
    }
    
    private void SignalDisallowed(NpcContext context)
    {
        roleCheckFailure?.Invoke(context);
        foreach (var role in rolesToAddOnFailure)
        {
            context.Identity.AddDynamicRole(role);
        }

        if (disallowedSuspicionLevel > 0)
        {
            context.SuspicionTracker.AddSuspicionSource(Guid.NewGuid().ToString(), disallowedSuspicionLevel, disallowedSuspicionTime);
        }
    }

    private void SignalResult(NpcContext context, bool roleResult)
    {
        if (infectedDisallowed && InfectionManager.Instance.IsNpcInfected(context))
        {
            roleResult = false;
        }
        
        if (roleResult)
        {
            SignalAllowed(context);
        }
        else
        {
            SignalDisallowed(context);
        }
    }

    private void HandleInteractionEnd(InteractionContext context)
    {
        if (!context.Initiator.TryGetComponent<NpcContext>(out NpcContext initiatorContext))
        {
            Debug.LogWarning($"Initiator {context.Initiator.name} does not have a NpcContext component.");
            return;
        }

        bool hasAllowedRole = allowedRoles.Count == 0 || initiatorContext.Identity.HasAnyRole(allowedRoles);
        bool hasDisallowedRole = disallowedRoles.Count == 0 || initiatorContext.Identity.HasAnyRole(disallowedRoles);
        
        if (allowedRoles.Count == 0 && disallowedRoles.Count == 0)
        {
            // A great success! In this case I guess everyone is allowed.
            SignalResult(initiatorContext, true);
        }
        else if (allowedRoles.Count == 0 && disallowedRoles.Count > 0)
        {
            // Then you are only disallowed if hasDisallowedRole is true.
            if (hasDisallowedRole)
            {
                SignalResult(initiatorContext, false);
            }
            else
            {
                SignalResult(initiatorContext, true);
            }
        }
        else if (allowedRoles.Count > 0 && disallowedRoles.Count == 0)
        {
            // Then you are only allowed if hasAllowedRole is true.
            if (hasAllowedRole)
            {
                SignalResult(initiatorContext, true);
            }
            else
            {
                SignalResult(initiatorContext, false);
            }
        }
        else
        {
            // Then you are allowed if hasAllowedRole is true and not disallowed if hasDisallowedRole is false.
            if (hasAllowedRole && !hasDisallowedRole)
            {
                SignalResult(initiatorContext, true);
            }
            else
            {
                SignalResult(initiatorContext, false);
            }
        }
    }
}