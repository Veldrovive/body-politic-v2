using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class SaveableTransformReference : GOReference
{
}

/// <summary>
/// A custom Newtonsoft.Json converter for Transform objects.
/// Similar to the SaveableGOConverter, this gameObject that the Transform is attached to
/// must have a SaveableGOProducer component attached to it so that we can identify the
/// game object on load. In effect, we just serialize the GameObject with the understanding
/// that we are interested in the Transform component of that GameObject.
/// </summary>

public class TransformConverter : SaveableGOConverter
{
    public TransformConverter(Dictionary<string, GameObject> producerIdToGO,
        Dictionary<GameObject, string> gameObjectToProducerId) : base(producerIdToGO, gameObjectToProducerId)
    {
    }

    public override bool CanConvert(Type objectType)
    {
        // Instead of GOs, we are interested in Transform objects.
        return typeof(Transform).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        GameObject go = (GameObject) base.ReadJson(reader, objectType, existingValue, serializer);
        if (go == null)
        {
            // This happens if the game object field was null.
            return null;
        }
        
        // The game object existed and all game object have a transform so we are free to just return it.
        Transform transform = go.transform;
        return transform;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        Transform transform = (Transform) value;
        if (transform == null)
        {
            writer.WriteNull();
            return;
        }
        // We just serialize a transform as the game object it is attached to.
        GameObject go = transform.gameObject;
        base.WriteJson(writer, go, serializer);
    }
}