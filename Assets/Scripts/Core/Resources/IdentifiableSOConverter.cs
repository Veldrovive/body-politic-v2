using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// A custom Newtonsoft.Json converter for types inheriting from IdentifiableSO.
/// This converter handles serializing an IdentifiableSO instance to its unique string ID
/// and deserializing the ID back into a reference to the corresponding pre-loaded asset.
/// This avoids serializing the entire ScriptableObject's data and ensures that only
/// a single instance of each asset is referenced in the game.
/// </summary>
public class IdentifiableSOConverter : JsonConverter
{
    private Dictionary<string, IdentifiableSO> idToIdentifiableSO = new Dictionary<string, IdentifiableSO>();
    private Dictionary<IdentifiableSO, string> identifiableSoToId = new Dictionary<IdentifiableSO, string>();
    
    public IdentifiableSOConverter(Dictionary<string, IdentifiableSO> idToIdentifiableSO,
                                   Dictionary<IdentifiableSO, string> identifiableSoToId)
    {
        this.idToIdentifiableSO = idToIdentifiableSO;
        this.identifiableSoToId = identifiableSoToId;
    }
    
    /// <summary>
    /// A private helper class used to temporarily hold the structured SO reference during deserialization.
    /// </summary>
    private class SOReference
    {
        public string AssetID { get; set; }
        // The fields below are for debugging/readability in the JSON file and are not used by the deserializer.
        public string AssetName { get; set; }
        public string ConcreteType { get; set; }
    }
    
    /// <summary>
    /// Determines if this converter can be used for the given object type.
    /// It returns true if the type is IdentifiableSO or any class that inherits from it.
    /// </summary>
    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to IdentifiableSO and all of its subclasses.
        return typeof(IdentifiableSO).IsAssignableFrom(objectType);
    }

    /// <summary>
    /// Deserializes data from JSON into an IdentifiableSO object reference.
    /// Handles both the new object format and the old raw string format for backwards compatibility.
    /// </summary>
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        string id = null;

        if (reader.TokenType == JsonToken.String)
        {
            // --- Backwards Compatibility ---
            // Handles old save files where the value was just the string ID.
            id = (string)reader.Value;
        }
        else if (reader.TokenType == JsonToken.StartObject)
        {
            // --- New Format ---
            // Handles the new, more descriptive object format.
            // We let Newtonsoft deserialize the small object into our helper class.
            SOReference reference = serializer.Deserialize<SOReference>(reader);
            id = reference?.AssetID;
        }
        else
        {
            throw new JsonSerializationException($"Unexpected token when deserializing IdentifiableSO. Expected String or StartObject, but got {reader.TokenType}.");
        }

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("IdentifiableSO reference had a null or empty ID during deserialization. The reference will be null.");
            return null;
        }

        // The core logic remains the same: look up the object by its ID.
        if (idToIdentifiableSO.TryGetValue(id, out IdentifiableSO soInstance))
        {
            return soInstance;
        }

        Debug.LogWarning($"Could not find an IdentifiableSO with ID '{id}' during deserialization. The reference will be null.");
        return null;
    }

    /// <summary>
    /// Serializes an IdentifiableSO object reference into a descriptive JSON object.
    /// </summary>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        IdentifiableSO so = (IdentifiableSO)value;

        if (identifiableSoToId.TryGetValue(so, out string id))
        {
            // Instead of writing a simple string, write a structured object.
            writer.WriteStartObject();

            writer.WritePropertyName("ReferenceType");
            writer.WriteValue("IdentifiableSO"); // A clear marker for what this object represents.

            writer.WritePropertyName("AssetID");
            writer.WriteValue(id); // The essential piece of data for deserialization.

            writer.WritePropertyName("AssetName");
            writer.WriteValue(so.name); // The human-readable asset name for debugging.

            writer.WritePropertyName("ConcreteType");
            writer.WriteValue(so.GetType().Name); // The specific scriptable object type.

            writer.WriteEndObject();
        }
        else
        {
            throw new JsonSerializationException($"Attempted to serialize an unregistered IdentifiableSO: '{so.name}'. Make sure it is located in a 'Resources' folder and has a unique ID.");
        }
    }
}