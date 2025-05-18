using UnityEditor;
using UnityEngine;

namespace Sisus.ComponentNames.Editor
{
#if !COMPONENT_NAMES_DISABLE_PROPERTY_DRAWER && !INSPECTOR_GADGETS 
#if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Editor.DrawerPriority(0, 0, 0.26)] // make Odin prioritize other custom property drawers over this one 
#endif
    [CustomPropertyDrawer(typeof(Component), true)]
    [CustomPropertyDrawer(typeof(Object), false)] // needed for UnityEvent property drawer support
#endif
    public class RenameableObjectPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => RenameableObjectField.Draw(position, property, label, DrawDefaultObjectField);

        protected virtual void DrawDefaultObjectField(Rect position, SerializedProperty property, GUIContent label) => EditorGUI.ObjectField(position, property, label);
    }
}