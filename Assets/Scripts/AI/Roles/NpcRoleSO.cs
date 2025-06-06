using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcRoleSO", menuName = "Body Politic/NPC Role SO")]
public class NpcRoleSO : IdentifiableSO
{
    [Tooltip("The visual name shown to the player when they select individuals with this role.")]
    public string RoleName;
    [Tooltip("The description shown the the player when they select individuals with this role.")]
    [TextArea] public string RoleDescription;

    [Tooltip("Defines the visual preference for the role. Higher weight roles will override lower weight ones when shown to the player.")]
    public float RoleWeight;

    [Tooltip("The type of role this is. This is used to determine how the role interacts with the game world.")]
    public RoleType RoleType = RoleType.Default;

    public override string ToString()
    {
        return RoleName;
    }
}