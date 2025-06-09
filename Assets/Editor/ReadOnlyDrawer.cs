using UnityEditor;
using UnityEngine;

/// <summary>
/// This class tells Unity how to draw any field with the [ReadOnly] attribute.
/// </summary>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Disable the GUI so the field isn't editable
        GUI.enabled = false;

        // Draw the property field as normal, but it will be grayed out
        EditorGUI.PropertyField(position, property, label);

        // Re-enable the GUI for other fields
        GUI.enabled = true;
    }
}