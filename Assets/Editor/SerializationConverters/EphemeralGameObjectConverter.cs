#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A custom Newtonsoft.Json converter for serializing ephemeral, editor-only references
/// to any GameObject in a scene. This is intended for short-term operations like
/// copy-paste within the editor.
/// It serializes the GameObject to its InstanceID and deserializes it back using EditorUtility.
/// </summary>
public class EphemeralGameObjectConverter : JsonConverter
{
    /// <summary>
    /// A private helper class to hold the serialized reference data.
    /// </summary>
    private class GameObjectReference
    {
        public int InstanceID { get; set; }
        // The fields below are for debugging/readability in the JSON file.
        public string GameObjectName { get; set; }
    }
    
    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to GameObject.
        return typeof(GameObject).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        
        if (reader.TokenType != JsonToken.StartObject)
        {
            throw new JsonSerializationException($"Unexpected token when deserializing GameObject reference. Expected StartObject, but got {reader.TokenType}.");
        }

        var reference = serializer.Deserialize<GameObjectReference>(reader);
        if (reference == null || reference.InstanceID == 0)
        {
            Debug.LogWarning("GameObject reference had a null or zero InstanceID. The reference will be null.");
            return null;
        }
        
        // Find the object using its instance ID. This is only valid in the editor and for the current session.
        var foundObject = EditorUtility.InstanceIDToObject(reference.InstanceID) as GameObject;

        if (foundObject == null)
        {
            Debug.LogWarning($"Could not find GameObject with InstanceID '{reference.InstanceID}'. It may have been deleted or the scene changed.");
        }

        return foundObject;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var go = (GameObject)value;

        writer.WriteStartObject();

        writer.WritePropertyName("ReferenceType");
        writer.WriteValue("EphemeralGameObject");

        writer.WritePropertyName("InstanceID");
        writer.WriteValue(go.GetInstanceID()); // The essential piece of data for deserialization.

        writer.WritePropertyName("GameObjectName");
        writer.WriteValue(go.name); // For debugging.

        writer.WriteEndObject();
    }
}
#endif