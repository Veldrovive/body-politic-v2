#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A custom Newtonsoft.Json converter for serializing ephemeral, editor-only references
/// to any Transform component in a scene. This is intended for short-term operations like
/// copy-paste within the editor.
/// It serializes the Transform's underlying GameObject to its InstanceID and deserializes it back.
/// </summary>
public class EphemeralTransformConverter : JsonConverter
{
    /// <summary>
    /// A private helper class to hold the serialized reference data.
    /// This can be the same structure as the GameObject reference.
    /// </summary>
    private class TransformReference
    {
        public int InstanceID { get; set; }
        // The fields below are for debugging/readability in the JSON file.
        public string GameObjectName { get; set; }
    }

    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to Transform components.
        return typeof(Transform).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            throw new JsonSerializationException($"Unexpected token when deserializing Transform reference. Expected StartObject, but got {reader.TokenType}.");
        }
        
        var reference = serializer.Deserialize<TransformReference>(reader);
        if (reference == null || reference.InstanceID == 0)
        {
            Debug.LogWarning("Transform reference had a null or zero InstanceID. The reference will be null.");
            return null;
        }
        
        var foundObject = EditorUtility.InstanceIDToObject(reference.InstanceID) as GameObject;

        if (foundObject == null)
        {
            Debug.LogWarning($"Could not find GameObject with InstanceID '{reference.InstanceID}' for Transform reference. It may have been deleted or the scene changed.");
            return null;
        }

        // We found the GameObject, now return its Transform component.
        return foundObject.transform;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var transform = (Transform)value;
        var go = transform.gameObject;

        writer.WriteStartObject();
        
        writer.WritePropertyName("ReferenceType");
        writer.WriteValue("EphemeralTransform");

        writer.WritePropertyName("InstanceID");
        writer.WriteValue(go.GetInstanceID()); // We serialize the ID of the GameObject.

        writer.WritePropertyName("GameObjectName");
        writer.WriteValue(go.name); // For debugging.

        writer.WriteEndObject();
    }
}
#endif