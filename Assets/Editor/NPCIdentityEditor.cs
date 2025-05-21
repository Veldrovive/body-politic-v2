// Update Type: Full File
// File: Editor/NPCIdentityEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq; // Required for Enum.GetValues

[CustomEditor(typeof(NPCIdentity))]
public class NPCIdentityEditor : Editor
{
    // Store foldout states
    private Dictionary<RoleType, bool> roleTypeFoldouts = new Dictionary<RoleType, bool>();

    // Store SerializedProperties
    private SerializedProperty defaultRolesProp;
    private SerializedProperty additionalProvidersProp;

    /// <summary>
    /// Cache SerializedProperty references when the editor is enabled.
    /// </summary>
    private void OnEnable()
    {
        defaultRolesProp = serializedObject.FindProperty("DefaultRoles");
        additionalProvidersProp = serializedObject.FindProperty("AdditionalRoleProviderObjects"); // Use the updated name if you changed it
    }

    /// <summary>
    /// Draws the custom inspector GUI for the NPCIdentity component.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Update the serializedObject representation. Always do this at the beginning.
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        // Get the target NPCIdentity component
        NPCIdentity npcIdentity = (NPCIdentity)target;

        // --- Draw DefaultRoles with Change Check ---
        EditorGUI.BeginChangeCheck();
        // Draw the DefaultRoles list field. The 'true' argument enables child property drawing (elements).
        EditorGUILayout.PropertyField(defaultRolesProp, true);
        // Check if any changes were made within the BeginChangeCheck/EndChangeCheck block
        if (EditorGUI.EndChangeCheck())
        {
            // If changes occurred, apply them to the serialized object
            serializedObject.ApplyModifiedProperties();
            // If we are in Play Mode, notify the component to recalculate roles
            if (Application.isPlaying)
            {
                npcIdentity.EditorNotifyDefaultRolesChanged();
            }
        }
        // --- End Draw DefaultRoles ---

        // Draw the other serialized fields (AdditionalRoleProviders)
        EditorGUILayout.PropertyField(additionalProvidersProp, true);

        // Apply any changes made to other fields like AdditionalRoleProviders
        serializedObject.ApplyModifiedProperties();


        // --- Draw Runtime Role Information ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Role Information", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view the NPC's current aggregated roles.", MessageType.Info);
            return;
        }

        var rolesByType = npcIdentity.GetDebugRolesByType();

        if (rolesByType == null)
        {
             EditorGUILayout.HelpBox("Role data not available (possibly called before Start or component disabled).", MessageType.Warning);
             return;
        }

        if (rolesByType.Count == 0)
        {
             EditorGUILayout.HelpBox("No roles currently assigned or calculated.", MessageType.Info);
             // Still draw the headers even if empty
             // return; // Don't return, show empty categories
        }

        // Iterate through all defined RoleTypes
        foreach (RoleType type in Enum.GetValues(typeof(RoleType)))
        {
            if (!roleTypeFoldouts.ContainsKey(type)) { roleTypeFoldouts[type] = false; }

            bool hasRolesOfType = rolesByType.TryGetValue(type, out HashSet<NpcRoleSO> roles) && roles != null && roles.Count > 0;

            EditorGUI.BeginDisabledGroup(!hasRolesOfType);
            string foldoutLabel = $"{type} ({(hasRolesOfType ? roles.Count.ToString() : "0")})";
            // Use foldoutHeader style for better visual separation
            roleTypeFoldouts[type] = EditorGUILayout.Foldout(roleTypeFoldouts[type], foldoutLabel, true, EditorStyles.foldoutHeader); // Pass true for toggleOnLabelClick

            if (roleTypeFoldouts[type] && hasRolesOfType)
            {
                EditorGUI.indentLevel++;
                // Sort roles alphabetically for consistent display
                var sortedRoles = roles.ToList().OrderBy(r => r.name).ToList();
                foreach (NpcRoleSO role in sortedRoles)
                {
                    if (role == null) continue; // Skip if a null role somehow got added
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(role.RoleName ?? "Unnamed Role", role, typeof(NpcRoleSO), false);
                    EditorGUI.EndDisabledGroup();
                }
                 EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(2); // Add a little vertical space between types
        }
        // --- End Draw Runtime Role Information ---
    }
}