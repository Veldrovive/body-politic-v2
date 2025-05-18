using UnityEditor;
using UnityEngine;

// This PropertyDrawer targets any field of type AbstractReference (and its derived classes).
[CustomPropertyDrawer(typeof(AbstractReference<,>), true)]
public class AbstractReferencePropertyDrawer : PropertyDrawer
{
    /// <summary>
    /// Draws the custom GUI for the AbstractReference property.
    /// </summary>
    /// <param name="position">Rectangle on the screen to use for the property GUI.</param>
    /// <param name="property">The SerializedProperty to make the GUI for.</param>
    /// <param name="label">The label of this property.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // BeginProperty used to properly handle prefabs and other system interactions.
        EditorGUI.BeginProperty(position, label, property);

        // Draw the label for the property.
        // If you want no main label for the entire line, you can comment this out or adjust.
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Don't indent child fields
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Get the serialized properties for UseConstant, ConstantValue, and Variable.
        // We use FindPropertyRelative to find them as children of the main property.
        SerializedProperty useConstantProperty = property.FindPropertyRelative("UseConstant");
        SerializedProperty constantValueProperty = property.FindPropertyRelative("ConstantValue");
        SerializedProperty variableProperty = property.FindPropertyRelative("Variable");

        // Define the rectangle for the checkbox.
        // It will be narrow and on the left.
        Rect checkboxRect = new Rect(position.x, position.y, 20, position.height);

        // Display the UseConstant checkbox without its own label.
        EditorGUI.PropertyField(checkboxRect, useConstantProperty, GUIContent.none);

        // Adjust the x position and width for the value field that will be next to the checkbox.
        Rect valueRect = new Rect(position.x + checkboxRect.width + 5, position.y, position.width - checkboxRect.width - 5, position.height);

        // Conditionally display either the ConstantValue or Variable property field
        // based on the current value of UseConstant.
        if (useConstantProperty.boolValue)
        {
            // If UseConstant is true, display the ConstantValue field.
            EditorGUI.PropertyField(valueRect, constantValueProperty, GUIContent.none);
        }
        else
        {
            // If UseConstant is false, display the Variable field.
            EditorGUI.PropertyField(valueRect, variableProperty, GUIContent.none);
        }

        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Overrides the GetPropertyHeight to ensure the property is drawn on a single line.
    /// </summary>
    /// <param name="property">The SerializedProperty to make the GUI for.</param>
    /// <param name="label">The label of this property.</param>
    /// <returns>The height required for the property GUI.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // We want our property to be drawn on a single standard line height.
        return EditorGUIUtility.singleLineHeight;
    }
}