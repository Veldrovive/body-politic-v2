// Update Type: Full File
// File: Editor/NpcSuspicionTrackerEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // Required for List

/// <summary>
/// Custom Editor for the NpcSuspicionTracker component.
/// Displays the current maximum suspicion level and a list of active sources with remaining times during runtime.
/// </summary>
[CustomEditor(typeof(NpcSuspicionTracker))]
public class NpcSuspicionTrackerEditor : Editor
{
    private bool showSourcesFoldout = true; // Remember foldout state

    /// <summary>
    /// Draws the custom inspector GUI.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (if any are added later)
        DrawDefaultInspector();

        // Get the target component instance
        NpcSuspicionTracker tracker = (NpcSuspicionTracker)target;

        // Add a space for visual separation
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Debug Info", EditorStyles.boldLabel);

        // Only show runtime info when the application is playing
        if (Application.isPlaying)
        {
            // Display the current maximum suspicion level
            // Use BeginChangeCheck/EndChangeCheck to prevent marking scene dirty just for viewing runtime data
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Current Suspicion Level:", tracker.CurrentSuspicionLevel.ToString());

            // --- Display Active Sources ---
            showSourcesFoldout = EditorGUILayout.Foldout(showSourcesFoldout, "Active Suspicion Sources", true, EditorStyles.foldoutHeader);

            if (showSourcesFoldout)
            {
                EditorGUI.indentLevel++; // Indent the sources list slightly

                // Get the debug info list from the tracker component
                List<NpcSuspicionTracker.SuspicionSourceDebugInfo> sources = tracker.GetActiveSourcesDebugInfo();

                if (sources != null && sources.Count > 0)
                {
                    // Display each active source
                    foreach (var sourceInfo in sources)
                    {
                        // Display Source Name: Level (Expires in Xs)
                        EditorGUILayout.LabelField($" - {sourceInfo.SourceName}: Level {sourceInfo.Level} ({sourceInfo.RemainingTime:F1}s left)");
                    }
                }
                else
                {
                    // Show a message if no sources are active
                    EditorGUILayout.LabelField(" (No active sources)");
                }
                EditorGUI.indentLevel--; // Restore indent level
            }
            EditorGUI.EndChangeCheck(); // End change check for runtime data

            // Request the inspector repaint itself frequently during play mode
            // This ensures the remaining time updates visually without user interaction
            Repaint();
        }
        else
        {
            // Show a message indicating info is only available during play mode
            EditorGUILayout.HelpBox("Runtime suspicion info is only available during Play mode.", MessageType.Info);
        }
    }
}