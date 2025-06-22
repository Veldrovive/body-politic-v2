using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

#region Runtime Instantiation

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

/// <summary>
/// Used to generate SaveableSOs dynamically at runtime.
/// </summary>
public enum InstantiableSOType {
    None,           // Was not a dynamically generated SO (i.e., it's a project asset).
    Vector3Variable
}

#endregion


public class ProducerGOContext
{
    public string ProducerId;
    public string ProducerGOName;
    public SaveableGOProducer Producer;
    public HoldableType HoldableType = HoldableType.None;  // Used to instantiate the correct prefab type.
    public bool IsDestroyed = false;                       // If true, we might need to destroy the saveable on load. This might happen if
    // a consumable was eaten.
}

public class ProducerGODataContext
{
    public string ProducerId;  // Used to assign the correct id to the instantiated game object if it needs to be instantiated.
    public string ProducerGOName;
    public HoldableType HoldableType = HoldableType.None;
    public bool IsDestroyed = false;  // If true, this producer was destroyed and should not be instantiated on load.
}

public class SOGeneratorDataContext
{
    public string SOId;
    public string SOName;
    public InstantiableSOType SOType = InstantiableSOType.None;
}

public class SaveableSODataContext
{
    public SaveableData Data;
    public string SOName;
    public string SOId;
    public InstantiableSOType SOType;
}

public class SaveableSOContext
{
    public string SOId;
    public string SOName;
    public SaveableSO Instance;
    public InstantiableSOType SOType = InstantiableSOType.None;
}

public class SaveDataMeta
{
    public DateTime SaveTime;      // The time at which the save was made.
    public SceneId ActiveSceneId;  // The scene in which the save was made.
    public float GameTime;      // The game time at which the save was made.
}

/// <summary>
/// Holds the portion of save data necessary to instantiate the game object, but not the internal data.
/// This is used so that we can first instantiate any missing game objects so that if they are referenced in the
/// later save data, they can be found.
/// </summary>
public class SaveDataGOGenerator
{
    public List<ProducerGODataContext> ProducersGODefs = new List<ProducerGODataContext>();
}

/// <summary>
/// Holds the portion of save data necessary to instantiate the SaveableSO, but not its internal data.
/// </summary>
public class SaveDataSOGenerator
{
    public List<SOGeneratorDataContext> SOGeneratorDefs = new List<SOGeneratorDataContext>();
}

/// <summary>
/// Holds the portion of the data necessary to fill in the data for game objects. This is the stuff we got from
/// calling GetSaveData on the SaveableGOProducer.
/// </summary>
public class SaveDataGOProducer
{
    public List<ProducerGOSaveableData> ProducerData = new List<ProducerGOSaveableData>();
}

/// <summary>
/// Holds the portion of the data necessary to fill in the data for SaveableSOs.
/// </summary>
public class SaveDataSOSaveable
{
    public List<SaveableSODataContext> SaveableSOData = new List<SaveableSODataContext>();
}

/// <summary>
/// So that we can deserialize each section independently, the actual SaveData class maps to strings that contain the
/// serialized data for each section.
/// </summary>
public class SaveData
{
    public SaveDataMeta meta = new SaveDataMeta(); // Metadata about the save, such as time and active scene.
    public string SaveDataGOGenerator;   // Serialized data for creating the game object producers.
    public string SaveDataSOGenerator;  // Serialized data for creating the saveable scriptable objects.
    public string SaveDataGOProducer;     // Serialized data for the game object producers.
    public string SaveDataSOSaveable;    // Serialized data for the saveable scriptable objects.
}

public class DeserializedSaveData
{
    public SaveDataMeta meta;
    public SaveDataGOGenerator SaveDataGOGenerator;   // Deserialized data for creating the game object producers.
    public SaveDataSOGenerator SaveDataSOGenerator;   // Deserialized data for creating the saveable scriptable objects.
    public SaveDataGOProducer SaveDataGOProducer;     // Deserialized data for the game object producers.
    public SaveDataSOSaveable SaveDataSOSaveable;     // Deserialized data for the saveable scriptable objects.
}

[DefaultExecutionOrder(-50)]
public class SaveableDataManager : MonoBehaviour
{
    [SerializeField] private bool isTesting = false;
    
    [SerializeReference]
    private List<SaveableGOProducer> saveableGOProducers = new List<SaveableGOProducer>();
    [SerializeReference]
    private List<SaveableSO> saveableSOsList = new List<SaveableSO>();

    [SerializeField]
    private List<HoldablePrefabMapping> holdablePrefabMappings = new List<HoldablePrefabMapping>();
    
    private Dictionary<InstantiableSOType, System.Type> soTypeToSystemType = new Dictionary<InstantiableSOType, System.Type>();

    private float startTime = 0;
    public float time => startTime + Time.timeSinceLevelLoad;

    private Dictionary<string, ProducerGOContext> producers = new Dictionary<string, ProducerGOContext>();
    // TODO: Is this necessary? These are set at runtime and we can look them up using a resource query at awake
    // like we do with IdentifiableSOs. In face they are a subset of IdentifiableSOs so we could use the same loop.
    private Dictionary<string, SaveableSOContext> saveableSOs = new Dictionary<string, SaveableSOContext>();
    
    // Used for serialization of IdentifiableSOs.
    private Dictionary<IdentifiableSO, string> identifiableSoToId = new Dictionary<IdentifiableSO, string>();
    private Dictionary<string, IdentifiableSO> identifiableSOs = new Dictionary<string, IdentifiableSO>();
    
    // Used for the serialization of GameObjects that have SaveableGOProducers.
    private Dictionary<GameObject, string> gameObjectToProducerId = new Dictionary<GameObject, string>();
    private Dictionary<string, GameObject> producerIdToGO = new Dictionary<string, GameObject>();

    public static SaveableDataManager Instance;
    private void Awake() {
        if (Instance == null) {
            Instance = this;
            ConstructSOTypeDatabase();
            ConstructIdentifiableSODatabase();
        } else {
            // This shouldn't really happen, but the correct behavior is to overwrite the old instance.
            Debug.LogWarning($"There is already an instance of SaveableDataManager in the scene. Overwriting the old instance.");
            Destroy(Instance.gameObject);  // Destroy the old instance.
            Instance = this;  // Set the new instance.
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    /// <summary>
    /// Populates the dictionary that maps an enum type to a concrete C# System.Type.
    /// This is where you define all your instantiable SOs.
    /// </summary>
    private void ConstructSOTypeDatabase()
    {
        soTypeToSystemType[InstantiableSOType.Vector3Variable] = typeof(Vector3VariableSO);
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
            Debug.LogError("There is already a producer with the id " + producerId, producer);
            return;
        }

        ProducerGOContext context = new ProducerGOContext
        {
            ProducerId = producerId,
            ProducerGOName = producer.name,
            Producer = producer,
            HoldableType = holdableType,
            IsDestroyed = isDestroyed
        };

        producers[producerId] = context;
        
        gameObjectToProducerId[producer.gameObject] = producerId;
        producerIdToGO[producerId] = producer.gameObject;

        saveableGOProducers.Add(producer);
    }

    public void HandleProducerDestroyed(SaveableGOProducer producer) {
        string producerId = producer.Config.ProducerId;
        if (producers.ContainsKey(producerId)) {
            producers[producerId].IsDestroyed = true;
        } else {
            Debug.LogWarning($"Producer {producerId} not found in the registry. Failed to mark as destroyed.");
        }
    }

    public GameObject InstantiateHoldable(HoldableType holdableType, Vector3 position, Quaternion rotation, string id = null, bool doBlankLoad = true) {
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
        if (id == null)
        {
            producer.SetConfig(Guid.NewGuid().ToString(), -1);
        }
        else
        {
            producer.SetConfig(id, -1);
        }
        RegisterProducer(producer, holdableType);

        if (doBlankLoad)
        {
            // Then we are not going to call LoadSaveData during the load so we should call it here.
            producer.LoadSaveData(null, true);  // This will set up the producer without loading any data.
        }

        // Debug.Log($"Instantiated holdable of type {holdableType} at {position}. Producer registered with ID {producer.Config.ProducerId}.", this);
        return instance;
    }
    
    /// <summary>
    /// Similar to InstantiateHoldable, but this is used to create a new instance of a SaveableSO that will be
    /// re-constructed from scratch on load.
    /// </summary>
    /// <typeparam name="TSaveableSO"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public TSaveableSO CreateInstance<TSaveableSO>(InstantiableSOType soType) where TSaveableSO : SaveableSO
    {
        if (!soTypeToSystemType.TryGetValue(soType, out System.Type systemType))
        {
            Debug.LogError($"No C# Type mapping found for InstantiableSOType {soType}. Did you add it to ConstructSOTypeDatabase?");
            return null;
        }
        TSaveableSO instance = (TSaveableSO)ScriptableObject.CreateInstance(systemType);
    
        // Assign a new unique ID and name
        string newId = Guid.NewGuid().ToString();
        // The name no longer comes from a template, so we use the C# type name.
        instance.name = $"{systemType.Name}_Instance_{newId}";
        instance.SetID(newId);

        // Register the new instance with its context
        var context = new SaveableSOContext
        {
            SOId = newId,
            SOName = instance.name,
            Instance = instance,
            SOType = soType // Mark as dynamically created
        };
        saveableSOs[newId] = context;
        identifiableSOs[newId] = instance;
        identifiableSoToId[instance] = newId;
    
        // Initialize its default state
        instance.LoadSaveData(null, true);

        return instance;
    }
    
    #region Save

    private string ConstructSerializedSaveData() {
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            Formatting = Formatting.Indented,
        };
        jsonSettings.Converters.Add(new IdentifiableSOConverter(identifiableSOs, identifiableSoToId));
        jsonSettings.Converters.Add(new SaveableGOConverter(producerIdToGO, gameObjectToProducerId));
        jsonSettings.Converters.Add(new TransformConverter(producerIdToGO, gameObjectToProducerId));
        
        SaveData data = new();

        data.meta.SaveTime = DateTime.Now;
        data.meta.ActiveSceneId = SceneLoadManager.Instance.GetCurrentScenedId();

        SaveDataGOGenerator goSaveData = new();
        SaveDataGOProducer producerSaveData = new();
        SaveDataSOSaveable saveableSOData = new();
        SaveDataSOGenerator soGeneratorData = new();
        

        foreach (var producerContext in producers.Values)
        {
            ProducerGODataContext goData = new()
            {
                ProducerId = producerContext.ProducerId,
                ProducerGOName = producerContext.ProducerGOName,
                HoldableType = producerContext.HoldableType,
                IsDestroyed = producerContext.IsDestroyed
            };
            goSaveData.ProducersGODefs.Add(goData);
            
            if (producerContext.IsDestroyed)
            {
                continue; // Skip saving data for destroyed producers.
            }
            ProducerGOSaveableData saveData = producerContext.Producer.GetSaveData();
            if (saveData == null)
            {
                Debug.LogWarning($"Producer {producerContext.Producer.Config.ProducerId} returned null save data. Skipping.");
                continue; // Skip saving if no data is returned.
            }
            producerSaveData.ProducerData.Add(saveData);
        }

        foreach (var soContext in saveableSOs.Values) {
            // 1. Add definition to the SOGenerator
            soGeneratorData.SOGeneratorDefs.Add(new SOGeneratorDataContext()
            {
                SOId = soContext.SOId,
                SOName = soContext.SOName,
                SOType = soContext.SOType,
            });

            // 2. Add state to the SOSaveable
            SaveableData stateData = soContext.Instance.GetSaveData();
            if (stateData != null) {
                saveableSOData.SaveableSOData.Add(new SaveableSODataContext()
                {
                    Data = stateData,
                    SOName = soContext.SOName,
                    SOId = soContext.SOId,
                    SOType = soContext.SOType
                });
            }
        }

        data.SaveDataGOGenerator = JsonConvert.SerializeObject(goSaveData, jsonSettings);
        data.SaveDataSOGenerator = JsonConvert.SerializeObject(soGeneratorData, jsonSettings);
        data.SaveDataGOProducer = JsonConvert.SerializeObject(producerSaveData, jsonSettings);
        data.SaveDataSOSaveable = JsonConvert.SerializeObject(saveableSOData, jsonSettings);

        if (isTesting)
        {
            // Then we also write out the save data to a folder with one file for each of the sections.
            string folderPath = System.IO.Path.Combine(SaveFileInterface.GetRootSaveDir(), "TestSaves");
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            
            string metaDataPath = System.IO.Path.Combine(folderPath, "_SaveDataMeta.json");
            string goSaveDataPath = System.IO.Path.Combine(folderPath, "SaveDataGOGenerator.json");
            string producerSaveDataPath = System.IO.Path.Combine(folderPath, "SaveDataGOProducer.json");
            string saveableSODataPath = System.IO.Path.Combine(folderPath, "SaveDataSOSaveable.json");
            string soGeneratorDataPath = System.IO.Path.Combine(folderPath, "SaveDataSOGenerator.json");
            
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            string metaDataJson = JsonConvert.SerializeObject(data.meta, jsonSettings);
            System.IO.File.WriteAllText(metaDataPath, metaDataJson);
            System.IO.File.WriteAllText(goSaveDataPath, data.SaveDataGOGenerator);
            System.IO.File.WriteAllText(producerSaveDataPath, data.SaveDataGOProducer);
            System.IO.File.WriteAllText(saveableSODataPath, data.SaveDataSOSaveable);
            System.IO.File.WriteAllText(soGeneratorDataPath, data.SaveDataSOGenerator);
            Debug.Log($"Test save data written to {folderPath}");
        }
        
        string serializedJson = JsonConvert.SerializeObject(data, jsonSettings);
        return serializedJson;
    }

    public void CreateSave()
    {
        string json = ConstructSerializedSaveData();
        
        string saveDir = SaveFileInterface.CreateSaveDir();
        string dataPath = System.IO.Path.Combine(saveDir, "SaveData.json");
        string screenshotPath = System.IO.Path.Combine(saveDir, "Screenshot.png");
        
        try
        {
            System.IO.File.WriteAllText(dataPath, json);
            Debug.Log($"Save created successfully at {dataPath}");
            
            // Take and write the screenshot
            ScreenCapture.CaptureScreenshot(screenshotPath);
            Debug.Log($"Screenshot saved at {screenshotPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create save file: {ex.Message}");
        }
    }
    
    #endregion


    #region Load
    
    private void RecreateGameObjects(SaveDataGOGenerator goDefinitions)
    {
        foreach (var goDefinition in goDefinitions.ProducersGODefs)
        {
            // Check if the producer is already registered. If it is, we are done. No need to recreate something that already exists.
            if (producers.ContainsKey(goDefinition.ProducerId))
            {
                // Debug.Log($"Producer {goDefinition.ProducerId} already exists. Skipping recreation.");
                continue;
            }
            
            // Similarly, if the producer is marked as destroyed, we should not recreate it.
            if (goDefinition.IsDestroyed)
            {
                Debug.Log($"Producer {goDefinition.ProducerId} is marked as destroyed. Skipping recreation.");
                continue;
            }
            
            // If we get here, we need to instantiate the game object. However, we still need to check if the prefab exists.
            // Also, if this object was not instantiated from a prefab, we can detect that with the HoldableType.
            // If it is None, something went wrong with the save data. An object was supposed to exist in the scene, but does not.
            if (goDefinition.HoldableType == HoldableType.None)
            {
                Debug.LogError($"Producer {goDefinition.ProducerId} has no HoldableType defined. Cannot instantiate.");
                continue;
            }
            
            // Find the prefab for this holdable type.
            HoldablePrefabMapping mapping = holdablePrefabMappings.Find(p => p.holdableType == goDefinition.HoldableType);
            if (mapping == null)
            {
                Debug.LogError($"No prefab mapping found for holdable type {goDefinition.HoldableType}. Cannot instantiate producer {goDefinition.ProducerId}.");
                continue;
            }
            
            // Instantiate the prefab at the origin (0, 0, 0) with no rotation. This will be set when we load the data.
            GameObject instance = InstantiateHoldable(mapping.holdableType, Vector3.zero, Quaternion.identity, id: goDefinition.ProducerId, doBlankLoad: false);
            if (instance == null)
            {
                Debug.LogError($"Failed to instantiate prefab for holdable type {goDefinition.HoldableType}. Cannot recreate producer {goDefinition.ProducerId}.");
                continue;
            }
            
            Debug.Log($"Instantiated prefab {goDefinition.ProducerId}.");
        }
    }

    private void RecreateScriptableObjects(SaveDataSOGenerator soDefinitions)
    {
        if (soDefinitions == null) return;

        foreach (var soDef in soDefinitions.SOGeneratorDefs)
        {
            // If it's a pre-existing asset or already registered, skip.
            if (soDef.SOType == InstantiableSOType.None || saveableSOs.ContainsKey(soDef.SOId))
            {
                continue;
            }

            if (!soTypeToSystemType.TryGetValue(soDef.SOType, out System.Type systemType))
            {
                Debug.LogError($"Cannot recreate SO {soDef.SOId}. No C# Type mapping found for InstantiableSOType {soDef.SOType}.");
                continue;
            }
            var instance = (SaveableSO)ScriptableObject.CreateInstance(systemType);
            instance.name = soDef.SOName;
            instance.SetID(soDef.SOId);

            var context = new SaveableSOContext
            {
                SOId = instance.ID,
                SOName = instance.name,
                Instance = instance,
                SOType = soDef.SOType
            };
            saveableSOs[instance.ID] = context;
            identifiableSOs[instance.ID] = instance;
            identifiableSoToId[instance] = instance.ID;
        }
    }
    
    private void LoadGameObjectData(SaveDataGOProducer producerData)
    {
        // Assumes all game object have been instantiated by this point. If one is missing, we warn, but do not fail.
        // We have one thing to do before we can load the data, sort by the load order.
        List<ProducerGOSaveableData> ProducerData = producerData.ProducerData;  // yea my naming sucks
        // Sort by increasing load order.
        ProducerData.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));
        foreach (ProducerGOSaveableData data in ProducerData)
        {
            if (!producers.TryGetValue(data.ProducerId, out ProducerGOContext context))
            {
                Debug.LogWarning($"Producer {data.ProducerId} not found in the registry. Cannot load data.");
                continue;
            }

            if (context.IsDestroyed)
            {
                Debug.LogWarning($"Producer {data.ProducerId} is marked as destroyed. Skipping loading data.");
                continue;
            }

            // Now we can set the data on the producer.
            context.Producer.LoadSaveData(data, false);
            // Debug.Log($"Loaded data for producer {data.ProducerId}.");
        }
    }
    
    private void LoadSaveableSOData(SaveDataSOSaveable saveableSOData)
    {
        foreach (SaveableSODataContext soData in saveableSOData.SaveableSOData)
        {
            // CHANGE: Use the context dictionary to find the instance
            if (!saveableSOs.TryGetValue(soData.SOId, out SaveableSOContext context))
            {
                Debug.LogWarning($"SaveableSO with ID {soData.SOId} not found. Cannot load data for {soData.SOName}.");
                continue;
            }

            context.Instance.LoadSaveData(soData.Data, false);
        }
    }

    private void DestroyDestroyedObjects(SaveDataGOGenerator goDefinitions)
    {
        // After we have the game fully loaded, we need to destroy any objects that were marked as destroyed in the save data.
        foreach (var goDefinition in goDefinitions.ProducersGODefs)
        {
            if (goDefinition.IsDestroyed)
            {
                // If the producer is marked as destroyed, we need to destroy the game object.
                if (producers.TryGetValue(goDefinition.ProducerId, out ProducerGOContext context))
                {
                    context.Producer.HandleDestroyOnLoad();  // This calls destroy on the game object.
                }
                else
                {
                    // This isn't actually a problem. We expect it to occur most of the time in fact. This just means
                    // that the producer was part of a prefab that was created and then destroyed before the save data was created.
                    // Debug.LogWarning($"Producer {goDefinition.ProducerId} not found in the registry. Cannot destroy.");
                }
            }
        }
    }
    
    /// <summary>
    /// Loads the save data from a file and deserializes it into game objects and saveable scriptable objects.
    /// Returns the deserialized save data for further processing if needed.
    /// </summary>
    /// <returns></returns>
    private DeserializedSaveData LoadSaveData(string saveDataJSON)
    {
        // First we deserialize into the SaveData class which has the sections as strings.
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            Formatting = Formatting.Indented,
        };
        jsonSettings.Converters.Add(new IdentifiableSOConverter(identifiableSOs, identifiableSoToId));
        jsonSettings.Converters.Add(new SaveableGOConverter(producerIdToGO, gameObjectToProducerId));
        jsonSettings.Converters.Add(new TransformConverter(producerIdToGO, gameObjectToProducerId));
        
        SaveData saveData = JsonConvert.DeserializeObject<SaveData>(saveDataJSON, jsonSettings);
        if (saveData == null)
        {
            Debug.LogError("Failed to deserialize save data. Aborting load.");
            return null;
        }
        startTime = saveData.meta.GameTime;  // Set the start time to the game time at which the save was made.
        
        // Santity check that we are in the right level
        if (SceneLoadManager.Instance.GetCurrentScenedId() != saveData.meta.ActiveSceneId)
        {
            // Whoops, we are loading data into the wrong scene.
            Debug.LogError($"Trying to load save data into scene {SceneLoadManager.Instance.GetCurrentScenedId()} but save data is for scene {saveData.meta.ActiveSceneId}. Aborting load.");
            return null;
        }
        
        // Section 1: Recreating Game Objects
        SaveDataGOGenerator goDefinitions = JsonConvert.DeserializeObject<SaveDataGOGenerator>(saveData.SaveDataGOGenerator, jsonSettings);
        if (goDefinitions == null)
        {
            Debug.LogError("Failed to deserialize SaveDataGOGenerator. Aborting load.");
            return null;
        }
        RecreateGameObjects(goDefinitions);
        // This automatically added the created game objects to the relevant dictionaries so no further action is needed here.
        
        // Section 2: Recreating Scriptable Objects
        SaveDataSOGenerator soDefinitions = JsonConvert.DeserializeObject<SaveDataSOGenerator>(saveData.SaveDataSOGenerator, jsonSettings);
        if (soDefinitions == null)
        {
            Debug.LogError("Failed to deserialize SaveDataSOGenerator. Aborting load.");
            return null;
        }
        RecreateScriptableObjects(soDefinitions);
        
        // Section 3: Loading Game Object Data
        SaveDataGOProducer producerData = JsonConvert.DeserializeObject<SaveDataGOProducer>(saveData.SaveDataGOProducer, jsonSettings);
        if (producerData == null)
        {
            Debug.LogError("Failed to deserialize SaveDataGOProducer. Aborting load.");
            return null;
        }
        LoadGameObjectData(producerData);
        
        // Section 4: Loading Saveable Scriptable Object Data
        SaveDataSOSaveable saveableSOData = JsonConvert.DeserializeObject<SaveDataSOSaveable>(saveData.SaveDataSOSaveable, jsonSettings);
        if (saveableSOData == null)
        {
            Debug.LogError("Failed to deserialize SaveDataSOSaveable. Aborting load.");
            return null;
        }
        LoadSaveableSOData(saveableSOData);
        
        // Section 5: Destroying any objects that were marked as destroyed in the save data.
        DestroyDestroyedObjects(goDefinitions);
        
        // If we get here, everything was loaded successfully.
        DeserializedSaveData deserializedData = new DeserializedSaveData
        {
            meta = saveData.meta,
            SaveDataGOGenerator = goDefinitions,
            SaveDataGOProducer = producerData,
            SaveDataSOSaveable = saveableSOData
        };
        Debug.Log("Save data loaded successfully.");
        return deserializedData;
    }

    public DeserializedSaveData LoadSaveFile(string saveFilePath)
    {
        if (string.IsNullOrEmpty(saveFilePath))
        {
            Debug.LogError("Save file path is null or empty.");
            return null;
        }

        if (!System.IO.File.Exists(saveFilePath))
        {
            Debug.LogError($"Save file not found at {saveFilePath}.");
            return null;
        }

        // try
        // {
            string json = System.IO.File.ReadAllText(saveFilePath);
            return LoadSaveData(json);
        // }
        // catch (Exception ex)
        // {
        //     Debug.LogError($"Failed to load save file: {ex.Message}");
        //     throw;
        //     // return null;
        // }
    }

    public void BlankLoad()
    {
        startTime = 0;
        // Used to start the level with no save data.
        // To do this, we only need to call the Load methods of each producer and saveable SO with blankLoad true.
        // Step 1: Iterate over all producers and call LoadSaveData with blankLoad true.
        List<ProducerGOContext> orderedProducers = producers.Values.ToList();
        orderedProducers.Sort((a, b) => a.Producer.Config.LoadOrder.CompareTo(b.Producer.Config.LoadOrder));
        
        foreach (var producerContext in orderedProducers)
        {
            if (producerContext.IsDestroyed)
            {
                Debug.LogWarning($"Producer {producerContext.ProducerId} is marked as destroyed. Skipping blank load.");
                continue;
            }
            producerContext.Producer.LoadSaveData(null, true);
        }
        
        // Step 2: Iterate over all saveable SOs and call LoadSaveData with blankLoad true.
        foreach (var saveableSOContext in saveableSOs.Values)
        {
            if (saveableSOContext.Instance == null)
            {
                Debug.LogWarning($"SaveableSO {saveableSOContext.SOId} is null. Skipping blank load.");
                continue;
            }
            saveableSOContext.Instance.LoadSaveData(null, true);
        }
    }
    
    #endregion
    
    private void ConstructIdentifiableSODatabase()
    {
        saveableSOsList.Clear();
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
            
            if (so is SaveableSO saveableSO)
            {
                // Create a context for pre-existing assets from Resources
                var context = new SaveableSOContext
                {
                    SOId = saveableSO.ID,
                    SOName = saveableSO.name,
                    Instance = saveableSO,
                    SOType = InstantiableSOType.None // Mark as a non-instantiable asset
                };
                saveableSOs[saveableSO.ID] = context;
        
                saveableSOsList.Add(saveableSO);
            }
        }
    }
}