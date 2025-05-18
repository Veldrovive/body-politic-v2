using System;
using UnityEditor;
using UnityEngine;

namespace Sisus.ComponentNames.Editor
{
	public static class RenameableObjectField
	{
		private static Color ObjectFieldBackgroundColor => EditorGUIUtility.isProSkin ? new Color32(42, 42, 42, 255) : new Color32(237, 237, 237, 255);
		
		public static void Draw(Rect position, SerializedProperty property, GUIContent label, Action<Rect, SerializedProperty, GUIContent> drawDefaultObjectField)
		{
			var objectFieldValue = property.objectReferenceValue as Component;
			if(!objectFieldValue || property.serializedObject.isEditingMultipleObjects)
			{
				drawDefaultObjectField(position, property, label);
				return;
			}
            
			var componentWithField = property.serializedObject.targetObject as Component;
			var textInsideField = GetTextInsideField(objectFieldValue, componentWithField);
			var fieldRect = EditorGUI.PrefixLabel(position, label);
			drawDefaultObjectField(fieldRect, property, GUIContent.none);
			fieldRect.x += 16f;
			fieldRect.width -= 35f;
			fieldRect.y += 2f;
			fieldRect.height -= 3f;
			EditorGUI.DrawRect(fieldRect, ObjectFieldBackgroundColor);
			GUI.Label(fieldRect, textInsideField);
		}
		
		public static string GetTextInsideField(Component objectFieldValue, Component componentWithField)
		{
			if(ReferenceEquals(objectFieldValue, componentWithField))
			{
				return "This Component";
			}

			string componentName = ComponentName.Get(objectFieldValue);
			if(componentWithField && objectFieldValue.gameObject == componentWithField.gameObject)
			{
				return string.Concat(componentName, " (this GameObject)");
			}

			var gameObjectName = objectFieldValue.gameObject.name;
			return string.Concat(componentName, " (", gameObjectName, ")");
		}
	}
}