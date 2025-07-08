using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for AbstractConditionalSO.
/// This editor draws the default inspector for all fields EXCEPT the base 'value' field.
/// In its place, it draws a disabled toggle that shows the real-time calculated result
/// of the condition's EvaluateCondition() method.
/// </summary>
[CustomEditor(typeof(AbstractConditionalSO), true)] // 'true' makes this editor apply to child classes as well.
public class AbstractConditionalSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Get the target scriptable object instance.
        AbstractConditionalSO conditionalSO = (AbstractConditionalSO)target;

        // The base AbstractVariableSO<T> has a serialized field named 'value'.
        // We want to hide this field from the inspector, as it's irrelevant for a calculated condition.
        // We do this by drawing all other properties manually.
        
        // Update the serialized object's representation.
        serializedObject.Update();

        // Draw all serialized properties EXCEPT for the 'value' field from the base class.
        // The "m_Script" field is also a default property we can safely exclude.
        DrawPropertiesExcluding(serializedObject, "value", "m_Script");

        // Apply any changes made to the other properties.
        serializedObject.ApplyModifiedProperties();

        // Now, draw our custom, calculated, read-only "Value" field.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Calculated Value", EditorStyles.boldLabel);

        // We use a disabled group to make the field non-interactive (read-only).
        EditorGUI.BeginDisabledGroup(true);
        {
            // By accessing the 'Value' property, we trigger the EvaluateCondition() method.
            // The result is displayed in a Toggle field.
            EditorGUILayout.Toggle("Value", conditionalSO.Value);
        }
        EditorGUI.EndDisabledGroup();

        // Repaint the inspector if the application is playing to see real-time changes.
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}