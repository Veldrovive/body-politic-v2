#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A custom Newtonsoft.Json converter for serializing ephemeral, editor-only references
/// to any ScriptableObject asset. This is intended for short-term operations like
/// copy-paste within the editor.
/// It serializes the asset to its GUID and deserializes it back using the AssetDatabase.
/// </summary>
public class EphemeralSOConverter : JsonConverter
{
    /// <summary>
    /// A private helper class to hold the serialized reference data.
    /// </summary>
    private class SOReference
    {
        public string AssetGUID { get; set; }
        // The fields below are for debugging/readability in the JSON file.
        public string AssetName { get; set; }
        public string ConcreteType { get; set; }
    }
    
    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to ScriptableObject and all of its subclasses.
        return typeof(ScriptableObject).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            throw new JsonSerializationException($"Unexpected token when deserializing ScriptableObject reference. Expected StartObject, but got {reader.TokenType}.");
        }

        var reference = serializer.Deserialize<SOReference>(reader);
        if (string.IsNullOrEmpty(reference?.AssetGUID))
        {
            Debug.LogWarning("ScriptableObject reference had a null or empty GUID. The reference will be null.");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"Could not find an asset with GUID '{reference.AssetGUID}'. It may have been deleted. The reference will be null.");
            return null;
        }

        // Load the asset from the path. We use LoadAssetAtPath<Object> to be generic.
        return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var so = (ScriptableObject)value;
        
        // This system can only reference assets that are saved to disk (i.e., have a GUID).
        if (!EditorUtility.IsPersistent(so))
        {
            throw new JsonSerializationException($"Attempted to serialize a non-persistent ScriptableObject: '{so.name}'. This converter only works for assets saved in the project.");
        }

        string path = AssetDatabase.GetAssetPath(so);
        string guid = AssetDatabase.AssetPathToGUID(path);
        
        writer.WriteStartObject();

        writer.WritePropertyName("ReferenceType");
        writer.WriteValue("EphemeralSO");

        writer.WritePropertyName("AssetGUID");
        writer.WriteValue(guid); // The essential piece of data for deserialization.

        writer.WritePropertyName("AssetName");
        writer.WriteValue(so.name); // For debugging.

        writer.WritePropertyName("ConcreteType");
        writer.WriteValue(so.GetType().Name); // For debugging.

        writer.WriteEndObject();
    }
}
#endif