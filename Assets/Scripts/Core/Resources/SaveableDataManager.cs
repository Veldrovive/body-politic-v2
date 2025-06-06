using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Used to generate prefabs dynamically at runtime
/// </summary>
public enum HoldableType {
    None,               // Wasn't a dynamically generated prefab.
    GenericConsumable,  // A generic consumable item.
}

[Serializable]
public class HoldablePrefabMapping {
    public HoldableType holdableType;
    public GameObject Prefab;
}

public class ProducerGOContext {
    public SaveableGOProducer Producer;
    public HoldableType HoldableType = HoldableType.None;  // Used to instantiate the correct prefab type.
    public bool IsDestroyed = false;                       // If true, we might need to destroy the saveable on load. This might happen if
    // a consumable was eaten.
}

public class ProducerSaveableDataContext
{
    public ProducerGOSaveableData Data;
    public HoldableType HoldableType = HoldableType.None;
    public bool IsDestroyed = false;  // If true, this producer was destroyed and should not be instantiated on load.
}

public class SaveData {
    public DateTime SaveTime;      // The time at which the save was made.
    public SceneId ActiveSceneId;  // The scene in which the save was made.

    // Saved Data:
    public Dictionary<string, ProducerSaveableDataContext> ProducerData = new Dictionary<string, ProducerSaveableDataContext>();
    public Dictionary<string, SaveableData> SaveableSOData = new Dictionary<string, SaveableData>();
}

[DefaultExecutionOrder(-50)]
public class SaveableDataManager : MonoBehaviour {
    [SerializeReference]
    private List<SaveableGOProducer> saveableGOProducers = new List<SaveableGOProducer>();

    [SerializeField]
    private List<HoldablePrefabMapping> holdablePrefabMappings = new List<HoldablePrefabMapping>();

    private Dictionary<string, ProducerGOContext> producers = new Dictionary<string, ProducerGOContext>();
    // TODO: Is this necessary? These are set at runtime and we can look them up using a resource query at awake
    // like we do with IdentifiableSOs. In face they are a subset of IdentifiableSOs so we could use the same loop.
    private Dictionary<string, SaveableSO> saveableSOs = new Dictionary<string, SaveableSO>();
    
    // Used for serialization of IdentifiableSOs.
    private Dictionary<IdentifiableSO, string> identifiableSoToId = new Dictionary<IdentifiableSO, string>();
    private Dictionary<string, IdentifiableSO> identifiableSOs = new Dictionary<string, IdentifiableSO>();
    private Dictionary<string, System.Type> identifiableSOTypes = new Dictionary<string, System.Type>();

    public static SaveableDataManager Instance;
    private void Awake() {
        if (Instance == null) {
            Instance = this;
            ConstructIdentifiableSODatabase();
        } else {
            Destroy(gameObject);
        }
    }

    public GameObject GetProducerObject(string producerId) {
        if (producers.TryGetValue(producerId, out ProducerGOContext context)) {
            if (context.IsDestroyed) {
                Debug.LogWarning($"Producer {producerId} is marked as destroyed. Returning null.");
                return null;
            }

            return context.Producer.gameObject;
        }

        Debug.LogError($"Producer with id {producerId} not found.");
        return null;
    }

    public void RegisterProducer(SaveableGOProducer producer, HoldableType holdableType = HoldableType.None, bool isDestroyed = false) {
        string producerId = producer.Config.ProducerId;
        if (producers.ContainsKey(producerId)) {
            Debug.LogError("There is already a producer with the id " + producerId);
            return;
        }

        ProducerGOContext context = new ProducerGOContext { Producer = producer, HoldableType = holdableType, IsDestroyed = isDestroyed };

        producers[producerId] = context;

        saveableGOProducers.Add(producer);
    }

    public void HandleProducerDestroyed(SaveableGOProducer producer) {
        string producerId = producer.Config.ProducerId;
        if (producers.ContainsKey(producerId)) {
            producers[producerId].IsDestroyed = true;
            Debug.Log($"Producer {producerId} marked as destroyed.");
        } else {
            Debug.LogWarning($"Producer {producerId} not found in the registry.");
        }
    }

    public GameObject InstantiateHoldable(HoldableType holdableType, Vector3 position, Quaternion rotation) {
        HoldablePrefabMapping mapping = holdablePrefabMappings.Find(p => p.holdableType == holdableType);
        if (mapping == null) {
            // The prefab requested does not exist in the mappings.
            Debug.LogError($"Prefab mapping for type {holdableType} not found.", this);
            return null;
        }
        GameObject prefab = mapping.Prefab;
        if (prefab == null) {
            Debug.LogError($"Prefab for type {holdableType} is null.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        if (instance == null) {
            Debug.LogError($"Failed to instantiate prefab of type {holdableType}.", this);
            return null;
        }

        SaveableGOProducer producer = instance.GetComponent<SaveableGOProducer>();
        if (producer == null) {
            // This isn't something that can be handled by the saveable system.
            Debug.LogError($"Prefab of type {holdableType} does not have a SaveableGOProducer component.", this);
            Destroy(instance);
            return null;
        }

        // Otherwise we should set up the producer and register it.
        producer.SetConfig(Guid.NewGuid().ToString(), -1);
        RegisterProducer(producer, holdableType);

        Debug.Log($"Instantiated holdable of type {holdableType} at {position}. Producer registered with ID {producer.Config.ProducerId}.", this);
        return instance;
    }

    private SaveData ConstructSaveData() {
        SaveData data = new();

        data.SaveTime = DateTime.Now;
        data.ActiveSceneId = SceneLoadManager.Instance.GetCurrentScenedId();

        foreach (var producerContext in producers.Values)
        {
            ProducerSaveableDataContext saveData;
            if (producerContext.IsDestroyed)
            {
                saveData = new()
                {
                    Data = null,
                    HoldableType = producerContext.HoldableType,
                    IsDestroyed = true
                };
            }
            else
            {
                ProducerGOSaveableData producerData = producerContext.Producer.GetSaveData();
                saveData = new()
                {
                    Data = producerData,
                    HoldableType = producerContext.HoldableType,
                    IsDestroyed = false
                };
            }
            
            data.ProducerData[producerContext.Producer.Config.ProducerId] = saveData;
        }

        // foreach (var saveableSO in saveableSOs.Values) {
        //     SaveableData saveableData = saveableSO.GetSaveData();
        //     if (saveableData != null) {
        //         data.SaveableSOData[saveableSO.Config.SaveableId] = saveableData;
        //     }
        // }

        return data;
    }

    public string SaveFilePath(string fileName)
    {
        string path;
#if UNITY_EDITOR
        // For debugging in the editor, create a "Saves" folder in the project root.
        // This makes the save file easily accessible from the project window.
        // Application.dataPath is the /Assets folder. "../" goes up one level to the project root.
        path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Saves"));
#else
        // For a deployed build, use the platform-safe persistent data path.
        path = Application.persistentDataPath;
#endif

        // Ensure the directory exists before trying to create a file there.
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }

        // Combine the path with the desired filename.
        return System.IO.Path.Combine(path, fileName);
    }

    public void CreateSave()
    {
        SaveData saveData = ConstructSaveData();
        
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            Formatting = Formatting.Indented,
        };
        // TODO: Should I do a similar thing here with GameObjects that are saveable? Like if something puts a gameobject
        // in its save data, we assume it must be have a SaveableGOProducer component? Currently that would be problematic
        // because dynamically generated prefabs do not have a gameobject at load time. The current plan is to handle
        // the creation of such prefabs after deserialization which would not work with a converter. Although, that is
        // only relevant for destroyed objects, but if it was destroyed, another objects should not have a reference to it
        // as it is now null. So actually it should be fine.
        jsonSettings.Converters.Add(new IdentifiableSOConverter(identifiableSOs, identifiableSoToId));
        string json = JsonConvert.SerializeObject(saveData, jsonSettings);
        
        string filename = $"save_{DateTime.Now:yyyyMMdd_HHmmss}.json"; // Unique filename based on current time
        string path = SaveFilePath(filename);
        
        try
        {
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Save created successfully at {path}");
            
            SaveData testDeserializedData = JsonConvert.DeserializeObject<SaveData>(json, jsonSettings);
            Debug.Log($"Deserialized");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create save file: {ex.Message}");
        }
    }
    
    private void ConstructIdentifiableSODatabase()
    {
        foreach (IdentifiableSO so in Resources.LoadAll<IdentifiableSO>(""))
        {
            if (so == null || string.IsNullOrEmpty(so.ID))
            {
                Debug.LogWarning($"IdentifiableSO {so?.name} has no ID. Skipping.");
                continue;
            }

            if (identifiableSOs.ContainsKey(so.ID))
            {
                Debug.LogError($"Duplicate IdentifiableSO found with ID {so.ID}. Skipping {so.name}.");
                continue;
            }

            identifiableSoToId[so] = so.ID;
            identifiableSOs[so.ID] = so;
            identifiableSOTypes[so.ID] = so.GetType();
            Debug.Log($"IdentifiableSO {so?.name} (Type: {so.GetType().Name}) with ID {so.ID} added to the database.");
        }
    }
}