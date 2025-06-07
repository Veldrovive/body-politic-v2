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
public class SaveableGOConverter : JsonConverter
{
    private Dictionary<GameObject, string> gameObjectToProducerId;
    private Dictionary<string, GameObject> producerIdToGO;
    
    public SaveableGOConverter(Dictionary<string, GameObject> producerIdToGO,
                               Dictionary<GameObject, string> gameObjectToProducerId)
    {
        this.gameObjectToProducerId = gameObjectToProducerId;
        this.producerIdToGO = producerIdToGO;
    }
    
    private class GOReference
    {
        public string ProducerId { get; set; }
        // The fields below are for debugging/readability in the JSON file and are not used by the deserializer.
        public string GameObjectName { get; set; }
    }
    
    /// <summary>
    /// Determines if this converter can be used for the given object type.
    /// It returns true if the type is GameObject or any class that inherits from it.
    /// </summary>
    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to IdentifiableSO and all of its subclasses.
        return typeof(GameObject).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            // This just means that the GameObject reference is null.
            return null;
        }

        string producerId = null;

        if (reader.TokenType == JsonToken.StartObject)
        {
            GOReference reference = serializer.Deserialize<GOReference>(reader);
            producerId = reference?.ProducerId;
        }
        else
        {
            throw new JsonSerializationException($"Unexpected token when deserializing GameObject. Expected String or StartObject, but got {reader.TokenType}.");
        }

        if (string.IsNullOrEmpty(producerId))
        {
            Debug.LogWarning("GameObject reference had a null or empty ID during deserialization. The reference will be null.");
            return null;
        }
        
        if (producerIdToGO.TryGetValue(producerId, out GameObject soInstance))
        {
            return soInstance;
        }

        Debug.LogWarning($"Could not find an GameObject with ID '{producerId}' during deserialization. The reference will be null.");
        return null;
    }

    /// <summary>
    /// Serializes an GameObject object reference into a descriptive JSON object.
    /// </summary>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        GameObject go = (GameObject)value;
        if (go == null)
        {
            // Game object references can be null. This is perfectly fine and expected.
            writer.WriteNull();
            return;
        }
        // We can only serialize saveable GameObjects. This means that they must have a SaveableGOProducer component attached.
        SaveableGOProducer producer = go.GetComponent<SaveableGOProducer>();
        if (producer == null)
        {
            throw new JsonSerializationException($"Attempted to serialize a GameObject that does not have a SaveableGOProducer component: '{go.name}'. Make sure it is properly configured.");
        }

        string trueProducerId = producer.Config.ProducerId;
        if (gameObjectToProducerId.TryGetValue(go, out string producerId))
        {
            if (trueProducerId != producerId)
            {
                // What happened?
                throw new JsonSerializationException($"The GameObject '{go.name}' has a mismatched ProducerId. Expected '{trueProducerId}', but found '{producerId}'. This indicates a configuration error.");
            }
            
            writer.WriteStartObject();
            writer.WritePropertyName("ProducerId");
            writer.WriteValue(producerId); // The essential piece of data for deserialization.
            
            writer.WritePropertyName("GameObjectName");
            writer.WriteValue(go.name); // The human-readable asset name for debugging.
            
            writer.WriteEndObject();
        }
        else
        {
            throw new JsonSerializationException($"Attempted to serialize an unregistered GameObject: '{go.name}'. Make sure it is registered with a SaveableGOProducer and has a unique ID.");
        }
    }
}