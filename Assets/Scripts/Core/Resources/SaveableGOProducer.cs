using System;
using System.Collections.Generic;
using UnityEngine;

#if (UNITY_EDITOR)
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public class ProducerGOSaveableConfig
{
    [SerializeField] public string ProducerId = null;  // Should get set by initialization code.
    [SerializeField] public float LoadOrder = 0f;  // Used to determine the order in which saveables are loaded. Higher is later.
}

public class ProducerGOSaveableData : SaveableData
{
    public string ProducerId;
    public string GameObjectName; // Optional, can be used to store the name of the GameObject for debugging purposes.
    public float LoadOrder = 0f; // Used to determine the order in which saveables are loaded. Higher is later.
    public Dictionary<string, SaveableData> ChildConsumerData = new Dictionary<string, SaveableData>();
}

public class SaveableGOProducer : MonoBehaviour
{
    [SerializeField] private ProducerGOSaveableConfig config;
    public ProducerGOSaveableConfig Config => config;
    
    private List<SaveableGOConsumer> consumers = new List<SaveableGOConsumer>();

    public void RegisterConsumer(SaveableGOConsumer consumer)
    {
        // Register this consumer with the producer if it is not already registered.
        if (consumer == null)
        {
            Debug.LogWarning("Attempted to register a null consumer. Skipping registration.");
            return;
        }
        if (!consumers.Contains(consumer))
        {
            consumers.Add(consumer);
            // Debug.Log($"Registered consumer {consumer.name} with producer {gameObject.name}.");
        }
        else
        {
            // This is fine. It just means that we registered during OnValidate and Awake.
            // Debug.LogWarning($"Consumer {consumer.name} is already registered with producer {gameObject.name}. Skipping registration.");
        }
    }
    
    public ProducerGOSaveableData GetSaveData()
    {
        ProducerGOSaveableData data = new ProducerGOSaveableData();
        data.ProducerId = config.ProducerId;
        data.GameObjectName = gameObject.name;
        data.LoadOrder = config.LoadOrder;
        data.ChildConsumerData = new Dictionary<string, SaveableData>();
        foreach (var consumer in consumers)
        {
            string consumerId = consumer.SaveableConfig.ConsumerId;
            if (string.IsNullOrEmpty(consumerId))
            {
                Debug.LogWarning($"Consumer {consumer.name} does not have a valid ConsumerId. Skipping save data for this consumer.");
                continue;
            }
            
            SaveableData consumerData = consumer.GetSaveData();
            data.ChildConsumerData.Add(consumer.SaveableConfig.ConsumerId, consumerData);
        }
        return data;
    }

    public void LoadSaveData(ProducerGOSaveableData data)
    {
        // If we are a prefab we will not have an ID. If this is the case, we load the id and the data.
        // If we already have an ID, we need to make sure that the ID matches the one in the data.
        if (string.IsNullOrEmpty(config.ProducerId))
        {
            config.ProducerId = data.ProducerId;
            config.LoadOrder = data.LoadOrder;
        }
        else
        {
            if (config.ProducerId != data.ProducerId)
            {
                Debug.LogError($"Producer ID mismatch: {config.ProducerId} != {data.ProducerId}. Cannot load save data.");
                return;
            }
        }
        
        foreach (var consumerData in data.ChildConsumerData)
        {
            string consumerId = consumerData.Key;
            SaveableData consumerSaveData = consumerData.Value;
            SaveableGOConsumer consumer = consumers.Find(c => c.SaveableConfig.ConsumerId == consumerId);
            if (consumer != null)
            {
                consumer.LoadSaveData(consumerSaveData);
            }
            else
            {
                Debug.LogWarning($"Consumer with ID {consumerId} not found for producer {config.ProducerId}. Skipping load.");
            }
        }
    }

    public void HandleDestroyOnLoad()
    {
        // Tells each of the components to handle destroying themselves on load and then destroys the producer.
        foreach (var consumer in consumers)
        {
            consumer.HandleDestroyOnLoad();
        }
        Destroy(gameObject);
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(config?.ProducerId))
        {
            // This is a prefab instance that has been created at runtime. We do not automatically register these
            // as the SaveableDataManager will do it manually to make sure it is recorded.
            return;
        }
        SaveableDataManager.Instance.RegisterProducer(this);
    }

    private void OnDestroy()
    {
        if (SaveableDataManager.Instance != null)
        {
            SaveableDataManager.Instance.HandleProducerDestroyed(this);
        }
        // else: the data manager unloaded before this producer was destroyed, which is fine. It just means that the
        // scene was unloaded
    }

    #region Id Autoset

    private void AutosetProducerId()
    {
        // We only autoset the ID if it is not set, this is not a prefab asset, and we are not in a prefab stage.
        // If it is a prefab asset or in a prefab stage, we in fact want to null the ID as prefabs absolutely never have IDs.
        // We also do not autoset IDs if the application is running. We only autoset IDs when designing the level.
        if (string.IsNullOrEmpty(config.ProducerId))
        {
            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // If we are playing, we should not autoset the ID.
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject) || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                // Then this is a prefab. It must not have an ID.
                config.ProducerId = null;
            }
            else
            {
                // Then we are not playing and this is not a prefab so we are designing the level. It must have an ID.
                Debug.Log($"Initializing ProducerId for {gameObject.name}");
                config.ProducerId = Guid.NewGuid().ToString();
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    // Required to actually save the changes to the prefab instance. If we don't do this, changes will
                    // visually appear in the editor, but are not "real". That was an annoying bug to track down.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }
            }
            #else
            throw new InvalidOperationException("ProducerGOSaveable must have a ProducerId during runtime.");
            #endif
        }
    }
    
    protected void OnValidate()
    {
        AutosetProducerId();
    }

    #endregion

    public void SetConfig(string producerId, int loadOrder)
    {
        config ??= new ProducerGOSaveableConfig();
        
        if (string.IsNullOrEmpty(config.ProducerId))
        {
            config.ProducerId = producerId;
        }
        else if (config.ProducerId != producerId)
        {
            Debug.LogWarning($"Producer ID mismatch: {config.ProducerId} != {producerId}. Overwriting with new ID.");
            config.ProducerId = producerId;
        }
        
        config.LoadOrder = loadOrder;
    }
}