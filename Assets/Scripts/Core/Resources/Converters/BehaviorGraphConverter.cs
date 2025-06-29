using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Behavior;
using UnityEngine;

public class BehaviorGraphConverter : JsonConverter
{
    private Dictionary<string, BehaviorGraph> keyToBehaviorGraph;
    private Dictionary<BehaviorGraph, string> behaviorGraphToKey;
    
    public BehaviorGraphConverter(Dictionary<string, BehaviorGraph> keyToBehaviorGraph,
        Dictionary<BehaviorGraph, string> behaviorGraphToKey)
    {
        this.keyToBehaviorGraph = keyToBehaviorGraph;
        this.behaviorGraphToKey = behaviorGraphToKey;
    }
    
    /// <summary>
    /// Determines if this converter can be used for the given object type.
    /// It returns true if the type is BehaviorGraph or any class that inherits from it.
    /// </summary>
    public override bool CanConvert(Type objectType)
    {
        // This converter is applicable to BehaviorGraph and all of its subclasses.
        return typeof(BehaviorGraph).IsAssignableFrom(objectType);
    }

    private class BehaviorGraphReference
    {
        public string Key { get; set; }
        
        public string TrueName { get; set; }
    }

    /// <summary>
    /// Deserializes data from JSON into an BehaviorGraph object reference.
    /// Handles both the new object format and the old raw string format for backwards compatibility.
    /// </summary>
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        BehaviorGraphReference graphData;
        if (reader.TokenType == JsonToken.StartObject)
        {
            graphData = serializer.Deserialize<BehaviorGraphReference>(reader);
        }
        else
        {
            throw new JsonSerializationException($"Unexpected token when deserializing BehaviorGraph. Expected String or StartObject, but got {reader.TokenType}.");
        }

        if (string.IsNullOrEmpty(graphData.Key))
        {
            Debug.LogWarning("BehaviorGraph reference had a null or empty ID during deserialization. The reference will be null.");
            return null;
        }
        
        if (keyToBehaviorGraph.TryGetValue(graphData.Key, out BehaviorGraph g))
        {
            return g;
        }

        Debug.LogWarning($"Could not find an BehaviorGraph with ID '{graphData.Key}' during deserialization. The reference will be null.");
        return null;
    }
    
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        BehaviorGraph g = (BehaviorGraph)value;
        string key = g.name;
        // Internally behavior graphs get cloned and so the name will end in " (Clone)". We don't care about that.
        // The key is the original name. I wish it could be a nice persistent ID, but Unity Behavior doesn't support that.
        // So instead we have to do string surgery to get the original name.
        if (key.EndsWith("(Clone)"))
        {
            key = key.Substring(0, key.Length - "(Clone)".Length);
        }
        // Now we can check if the key is registered
        if (!keyToBehaviorGraph.ContainsKey(key))
        {
            throw new JsonSerializationException($"Attempted to serialize an unregistered BehaviorGraph: '{g.name}'. Make sure it is located in a 'Resources' folder.");
        }
        // Otherwise we are free to write the key 
        writer.WriteStartObject();
        writer.WritePropertyName("Key");
        writer.WriteValue(key);
        writer.WritePropertyName("TrueName");
        writer.WriteValue(g.name);
        writer.WriteEndObject();
    }
}