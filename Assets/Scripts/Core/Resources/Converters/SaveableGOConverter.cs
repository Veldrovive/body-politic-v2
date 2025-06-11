using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

public class GOReference
{
    public string ProducerId { get; set; }  // Points to the parent object. Directly found in producerIdToGO.
    [CanBeNull] public string ConsumerId { get; set; }  // If not null, used to point to a SaveableGOConsumer component that lives on the GameObject.
    // The fields below are for debugging/readability in the JSON file and are not used by the deserializer.
    public string GameObjectName { get; set; }
    public string GameObjectScenePath { get; set; }
}

public class GameObjectSerializer
{
    private static string GetInScenePath(Transform transform)
    {
        var current = transform;
        var inScenePath = new List<string> { current.name };
        while (current != transform.root)
        {
            current = current.parent;
            inScenePath.Add(current.name);
        }
        var sb = new StringBuilder();
        foreach (var item in Enumerable.Reverse(inScenePath)) sb.Append($"\\{item}");
        return sb.ToString().TrimStart('\\');
    }
    
    public static bool WriteGameObject(JsonWriter writer, GameObject go, Dictionary<GameObject, string> gameObjectToProducerId)
    {
        if (go == null)
        {
            writer.WriteNull();
            return false;
        }

        void WriteGoReference(string trueProducerId, [CanBeNull] string trueConsumerId)
        {
            
            writer.WriteStartObject();
            writer.WritePropertyName("ProducerId");
            writer.WriteValue(trueProducerId); // The essential piece of data for deserialization.

            writer.WritePropertyName("ConsumerId");
            if (trueConsumerId != null)
            {
                writer.WriteValue(trueConsumerId);
            }
            else
            {
                writer.WriteNull();
            }
        
            writer.WritePropertyName("GameObjectName");
            writer.WriteValue(go.name); // The human-readable asset name for debugging.
            
            writer.WritePropertyName("GameObjectScenePath");
            writer.WriteValue(GetInScenePath(go.transform));
        
            writer.WriteEndObject();
        }
    
        // If the go has a SaveableGOProducer component, we can directly serialize it without a ConsumerId.
        if (go.TryGetComponent<SaveableGOProducer>(out var producer))
        {
            string trueProducerId = producer.Config.ProducerId;
            WriteGoReference(trueProducerId, null);
            return true;
        }
        else if (go.TryGetComponent<SaveableGOPointer>(out var pointerComponent))
        {
            // Then we need to serialize this GO with reference to the parent SaveableGOProducer.
            SaveableGOProducer parentProducer = pointerComponent.ParentProducer;
            if (parentProducer == null)
            {
                throw new JsonSerializationException($"SaveableGOPointer on GameObject '{go.name}' does not have a linked SaveableGOProducer. Please assign one in the Inspector.");
            }
            
            // We can serialize the GameObject with reference to the parent producer.
            string trueProducerId = parentProducer.Config.ProducerId;
            string trueConsumerId = pointerComponent.SaveableConfig.ConsumerId;
            WriteGoReference(trueProducerId, trueConsumerId);
            return true;
        }
        else
        {
            throw new JsonSerializationException($"Attempted to serialize a GameObject '{go.name}' that does not have a SaveableGOProducer or SaveableGOPointer component. Make sure it is properly configured.");
        }
    }

    public static GameObject ReadGOReference(GOReference reference, Dictionary<string, GameObject> producerIdToGO)
    {
        if (reference == null)
        {
            Debug.LogWarning("GOReference is null. Returning null GameObject.");
            return null;
        }
        
        string producerId = reference?.ProducerId;
        string consumerId = reference?.ConsumerId;

        if (!producerIdToGO.TryGetValue(producerId, out var producer))
        {
            // We couldn't find the producer. This is an error and means that the level probably had a breaking change.
            Debug.LogWarning($"Could not find a GameObject with ProducerId '{producerId}'. Returning null GameObject.");
            return null;
        }

        if (consumerId == null)
        {
            // We were looking for the producer itself, so we are good to return it.
            return producer;
        }
        
        // Otherwise we are looking for a specific consumer on the producer.
        SaveableGOProducer producerComponent = producer.GetComponent<SaveableGOProducer>();
        if (producerComponent == null)
        {
            // The producerGO had the component removed? This is an error and means that the level probably had a breaking change.
            Debug.LogWarning($"GameObject '{producer.name}' with ProducerId '{producerId}' does not have a SaveableGOProducer component. Returning null GameObject.");
            return null;
        }

        GameObject consumerGO = producerComponent.GetConsumerObject(consumerId);
        if (consumerGO == null)
        {
            // The consumerGO had the component removed? This is an error and means that the level probably had a breaking change.
            Debug.LogWarning($"GameObject '{producer.name}' with ProducerId '{producerId}' does not have a SaveableGOConsumer with ConsumerId '{consumerId}'. Returning null GameObject.");
            return null;
        }
        return consumerGO;
    }
}

/// <summary>
/// A custom Newtonsoft.Json converter for types inheriting from GameObject.
/// To properly serialize GameObjects, they must have a SaveableGOProducer component attached to
/// act as a unique identifier that is preserved across sessions.
/// If one does not, we error out to prevent serialization issues.
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

        if (reader.TokenType == JsonToken.StartObject)
        {
            GOReference reference = serializer.Deserialize<GOReference>(reader);
            return GameObjectSerializer.ReadGOReference(reference, producerIdToGO);
        }
        else
        {
            throw new JsonSerializationException($"Unexpected token when deserializing GameObject. Expected String or StartObject, but got {reader.TokenType}.");
        }
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
        GameObjectSerializer.WriteGameObject(writer, go, gameObjectToProducerId);
    }
}