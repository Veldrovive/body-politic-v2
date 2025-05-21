using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[NodeInfo("Say Role Missing", "Event Listener/Say Role Missing")]
public class SayRoleMissingListenerNode : EventListenerNode
{
    [Header("Message Templates")]
    [SerializeField]
    [Tooltip("Template string for a single missing role. Use {role} as a placeholder for the role name.")]
    private string singularRoleTemplate = "Looks like I have to be a {role}";

    [SerializeField]
    [Tooltip("Template string for multiple missing roles. Use {roles} as a placeholder for the list of role names.")]
    private string pluralRolesTemplate = "Looks like I have to be a {roles}";
    
    public static string SAY_ROLE_MISSING_PORT_NAME = "Say Role Missing";
    [EventInputPort("Say Role Missing")]
    public void HandleRoleMising(List<NpcRoleSO> roles)
    {
        Debug.Log($"SayRoleMissingListenerNode: {roles.Count} roles are missing.");
        CreatePlayerVisibleSpeechBubble(roles);
    }
    
    public void CreatePlayerVisibleSpeechBubble(List<NpcRoleSO> missingRoles)
    {
        // Constructs a player-facing error statement based on the list of missing NPC roles.
        string message = constructPlayerErrorStatement(missingRoles);
        
        // If the message is not empty, create a speech bubble with the message.
        if (!string.IsNullOrEmpty(message) && npcContext != null)
        {
            npcContext.SpeechBubbleManager.ShowBubble(
                message, 3f, true    
            );
        }
    }

    /// <summary>
    /// Constructs a player-facing error statement based on the list of missing NPC roles.
    /// </summary>
    /// <param name="missingRoles">A list of NpcRoleSO representing the roles that are missing.</param>
    /// <returns>A formatted string indicating the missing role(s), or an empty string if no roles are provided or templates are not set.</returns>
    private string constructPlayerErrorStatement(List<NpcRoleSO> missingRoles)
    {
        // Guards against null or empty list to prevent errors and unnecessary processing.
        if (missingRoles == null || missingRoles.Count == 0)
        {
            return string.Empty; // No roles missing, or list not provided.
        }

        if (missingRoles.Count == 1)
        {
            // Ensures a valid role object and name before proceeding.
            if (missingRoles[0] == null || string.IsNullOrEmpty(missingRoles[0].RoleName))
            {
                Debug.LogWarning("A missing role or its name is null or empty.");
                return "This door won't open for me";
            }
            string roleName = missingRoles[0].RoleName;
            // Replaces the placeholder in the singular template with the actual role name.
            return singularRoleTemplate.Replace("{role}", roleName);
        }
        else
        {
            // Extracts the names of all missing roles. Filters out any null roles or roles with null/empty names.
            List<string> roleNames = missingRoles
                                        .Where(role => role != null && !string.IsNullOrEmpty(role.RoleName))
                                        .Select(role => role.RoleName)
                                        .ToList();

            // If, after filtering, there are no valid role names, return empty.
            if (roleNames.Count == 0)
            {
                Debug.LogWarning("No valid role names found in the list of missing roles.");
                return string.Empty;
            }

            // Handles the case where filtering leaves only one role.
            if (roleNames.Count == 1)
            {
                return singularRoleTemplate.Replace("{role}", roleNames[0]);
            }

            string rolesString;
            // Constructs a grammatically correct list of role names (e.g., "role1 or role2" or "role1, role2, or role3").
            if (roleNames.Count == 2)
            {
                // Simple case for two roles: "A or B".
                rolesString = $"{roleNames[0]} or {roleNames[1]}";
            }
            else
            {
                // For three or more roles, join all but the last with ", " and then add ", or " before the last role.
                // This creates a natural language list like "A, B, or C".
                string allButLast = string.Join(", ", roleNames.Take(roleNames.Count - 1));
                rolesString = $"{allButLast}, or {roleNames.Last()}";
            }
            // Replaces the placeholder in the plural template with the formatted list of role names.
            return pluralRolesTemplate.Replace("{roles}", rolesString);
        }
    }
}