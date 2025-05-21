// Update Type: Full File
// File: Editor/PlayerIdentitySOEditor.cs
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlayerIdentitySO))]
public class PlayerIdentitySOEditor : Editor
{
    private SerializedProperty defaultRolesProp;

    /// <summary>
    /// Cache SerializedProperty references.
    /// </summary>
    private void OnEnable()
    {
        defaultRolesProp = serializedObject.FindProperty("DefaultRoles");
    }

    /// <summary>
    /// Draws the custom inspector GUI for the PlayerIdentitySO.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Update the serializedObject representation.
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        // Get the target PlayerIdentitySO
        PlayerIdentitySO playerIdentity = (PlayerIdentitySO)target;

        // Draw other fields if there were any (none in this case currently)
        // DrawDefaultInspector(); // Could use this if more fields are added later

        // --- Draw DefaultRoles with Change Check ---
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(defaultRolesProp, true);
        if (EditorGUI.EndChangeCheck())
        {
            // Apply changes to the SO asset
            serializedObject.ApplyModifiedProperties();

            // If in Play Mode, notify the SO instance to update its cache and notify listeners
            if (Application.isPlaying)
            {
                playerIdentity.EditorNotifyDefaultRolesChanged();
            }
            // Mark the asset dirty so changes are saved
            EditorUtility.SetDirty(playerIdentity);
        }
        // --- End Draw DefaultRoles ---

        // Apply changes at the end is good practice
        serializedObject.ApplyModifiedProperties();

        // Optional: Display dynamic roles at runtime (read-only)
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Dynamic Roles (Read-Only)", EditorStyles.boldLabel);
            // Note: Accessing dynamicRoles directly isn't ideal.
            // A better approach would be another GetDebug method on PlayerIdentitySO
            // similar to NPCIdentity, but for simplicity, we'll skip this for now.
            // If needed, add a GetDebugDynamicRoles() method to PlayerIdentitySO.
            EditorGUILayout.HelpBox("Dynamic roles are managed via API.", MessageType.Info);
        }
    }
}