// Update Type: Full File
// File: ZoneDefinitionSO.cs
using UnityEngine;
using System.Collections.Generic;
using System;

// Make sure SuspicionTier is accessible here.
// If it's in another file like ZoneTypes.cs, ensure that file exists.
// Alternatively, define SuspicionTier within this file if preferred.
// [System.Serializable] // Ensure this attribute is present for SuspicionTier
// public class SuspicionTier { ... } // As defined previously

/// <summary>
/// Defines a level of suspicion applied after an NPC spends a certain amount of time
/// in a zone without the required roles.
/// </summary>
[Serializable]
public class SuspicionTier
{
    [Tooltip("Time in seconds the NPC must be continuously unauthorized in the zone before this tier applies.")]
    public float Delay;
    [Tooltip("The level of suspicion to apply.")]
    public int SuspicionLevel;
    [Tooltip("How long the suspicion source persists after being applied. A short duration requires continuous application in Update to keep it active.")]
    public float RemovalDuration = 0.5f; // Default to short duration
}

/// <summary>
/// ScriptableObject defining the configuration for a type of gameplay zone.
/// Contains settings for role changes on enter/exit and suspicion rules.
/// </summary>
[CreateAssetMenu(fileName = "ZoneDefinitionSO", menuName = "Body Politic/Zone Definition SO")]
public class ZoneDefinitionSO : ScriptableObject
{
    [Header("Role Management")]
    [Tooltip("Roles to add to the NPC's dynamic roles when they enter the zone.")]
    public List<NpcRoleSO> RolesToAddOnEnter = new();

    [Tooltip("Roles to remove from the NPC's dynamic roles when they enter the zone.")]
    public List<NpcRoleSO> RolesToRemoveOnEnter = new();

    [Tooltip("Roles to add to the NPC's dynamic roles when they exit the zone.")]
    public List<NpcRoleSO> RolesToAddOnExit = new();

    [Tooltip("Roles to remove from the NPC's dynamic roles when they exit the zone.")]
    public List<NpcRoleSO> RolesToRemoveOnExit = new();

    [Header("Suspicion Management")]
    [Tooltip("NPCs MUST have at least one of these roles to be considered 'allowed' in this zone. If empty, all NPCs are allowed.")]
    public List<NpcRoleSO> AllowedRoles = new();

    [Tooltip("Defines suspicion levels applied over time to unauthorized NPCs. Tiers are applied cumulatively based on delay. Ensure this list is sorted by Delay if editing manually, otherwise ZoneDetector sorts it.")]
    public List<SuspicionTier> SuspicionTiers = new();
}