using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
//
// /// <summary>
// /// Used to generate prefabs dynamically at runtime
// /// </summary>
// public enum HoldableType
// {
//     None, // Wasn't a dynamically generated prefab.
//     GenericConsumable,  // A generic consumable item.
// }
//
// [Serializable]
// public class SaveableConfig
// {
//     [SerializeField] public string SaveableId = null;  // Should get set by initialization code.
//     [SerializeField] public float LoadOrder = 0f;  // Used to determine the order in which saveables are loaded. Higher is later.
// }
//
[Serializable]
public class SaveableData
{
    
}
//
// public class SaveableMetadata
// {
//     public HoldableType HoldableType = HoldableType.None;
//     public bool IsDestroyed = false;  // If true, we might need to destroy the saveable on load. This might happen if
//     // a consumable was eaten.
// }
//
// public class SaveContext
// {
//     public SaveableData Data;
//     public SaveableMetadata Metadata;
// }
//
// [Serializable]
// public class HoldablePrefabMapping
// {
//     public HoldableType holdableType;
//     public GameObject Prefab;
// }
//
// [DefaultExecutionOrder(-50)]
// public class ResourceDataManager : MonoBehaviour
// {
//     public int NumSaveables;
//     
//     [SerializeField] private List<HoldablePrefabMapping> holdablePrefabMappings = new List<HoldablePrefabMapping>();
//     
//     private Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();
//     private Dictionary<string, SaveableMetadata> saveablesMetadata = new Dictionary<string, SaveableMetadata>();
//     
//     public static ResourceDataManager Instance;
//     private void Awake()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }
//     
//     public TSavedObjectType GetSaveable<TSavedObjectType>(string saveableId) where TSavedObjectType : class, ISaveable
//     {
//         if (saveables.TryGetValue(saveableId, out ISaveable saveable))
//         {
//             return saveable as TSavedObjectType;
//         }
//         
//         Debug.LogWarning($"Saveable with ID {saveableId} not found.", this);
//         return default;
//     }
//     
//     public void RegisterSaveable(ISaveable saveable)
//     {
//         SaveableConfig config = saveable.SaveableConfig;
//         if (config == null || string.IsNullOrEmpty(config.SaveableId))
//         {
//             Debug.LogError("SaveableConfig is null or SaveableId is empty. Cannot register saveable.");
//             return;
//         }
//         
//         if (saveables.ContainsKey(config.SaveableId))
//         {
//             // If the attached saveable is different, log a warning
//             if (saveables[config.SaveableId] != saveable)
//             {
//                 if (saveable is Component component)
//                 {
//                     // Then we can log the component's GameObject name
//                     Debug.LogWarning($"Saveable with ID {config.SaveableId} ({component.gameObject.name}: {component.GetType()}) is already registered. Overwriting the existing one.", this);
//                 }
//                 else if (saveable is ScriptableObject scriptableObject)
//                 {
//                     // Then we can log the scriptable object's name
//                     Debug.LogWarning($"Saveable with ID {config.SaveableId} ({scriptableObject.name}: {scriptableObject.GetType()}) is already registered. Overwriting the existing one.", this);
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"Saveable with ID {config.SaveableId} is already registered. Overwriting the existing one.", this);
//                 }
//             }
//         }
//         else
//         {
//             // Then we also need to generate metadata for it
//             saveablesMetadata[config.SaveableId] = new SaveableMetadata();
//         }
//         saveables[config.SaveableId] = saveable;
//         
//         NumSaveables = saveables.Count;
//     }
//     
//     public void HandleSaveableDestroyed(ISaveable saveable)
//     {
//         SaveableConfig config = saveable.SaveableConfig;
//         if (config == null || string.IsNullOrEmpty(config.SaveableId))
//         {
//             Debug.LogError("SaveableConfig is null or SaveableId is empty. Cannot unregister saveable.");
//             return;
//         }
//         
//         // mark the saveable as destroyed
//         if (saveablesMetadata.TryGetValue(config.SaveableId, out SaveableMetadata metadata))
//         {
//             metadata.IsDestroyed = true;
//         }
//         else
//         {
//             Debug.LogWarning($"Saveable with ID {config.SaveableId} not found in metadata.", this);
//         }
//         
//         NumSaveables = saveables.Count;
//     }
//
//     public GameObject InstantiateHoldable(HoldableType holdableType, Vector3 position, Quaternion rotation)
//     {
//         HoldablePrefabMapping mapping = holdablePrefabMappings.Find(p => p.holdableType == holdableType);
//         if (mapping == null)
//         {
//             // The prefab requested does not exist in the mappings.
//             Debug.LogError($"Prefab mapping for type {holdableType} not found.", this);
//             return null;
//         }
//         GameObject prefab = mapping.Prefab;
//         if (prefab == null)
//         {
//             Debug.LogError($"Prefab for type {holdableType} is null.", this);
//             return null;
//         }
//         
//         GameObject instance = Instantiate(prefab, position, rotation);
//         if (instance == null)
//         {
//             Debug.LogError($"Failed to instantiate prefab of type {holdableType}.", this);
//             return null;
//         }
//         
//         // Now that we know the prefab was instantiated, we can set the metadata for it
//         Holdable holdable = instance.GetComponent<Holdable>();
//         string saveableId = holdable?.SaveableConfig?.SaveableId;
//         if (string.IsNullOrEmpty(saveableId))
//         {
//             Debug.LogError($"Holdable {holdableType} instantiated, but it does not have a valid SaveableId.", this);
//             return instance;
//         }
//         
//         // Instantiating the prefab should have registered it with the ResourceDataManager
//         if (!saveablesMetadata.TryGetValue(saveableId, out SaveableMetadata metadata))
//         {
//             Debug.LogError($"Saveable with ID {saveableId} not found in metadata.", this);
//             return instance;
//         }
//         
//         metadata.HoldableType = holdableType;
//         return instance;
//     }
//
//     public Dictionary<string, SaveContext> GetSaveObjects()
//     {
//         // Gathers all saveable data and metadata into a dictionary that will be serialized
//         Dictionary<string, SaveContext> saveObjects = new Dictionary<string, SaveContext>();
//         foreach (var kvp in saveables)
//         {
//             ISaveable saveable = kvp.Value;
//             SaveableConfig config = saveable.SaveableConfig;
//             if (config == null || string.IsNullOrEmpty(config.SaveableId))
//             {
//                 Debug.LogError($"Saveable {saveable} does not have a valid SaveableId. Skipping.", this);
//                 continue;
//             }
//             
//             SaveContext context = new SaveContext
//             {
//                 Data = saveable.GetSaveData(),
//                 Metadata = saveablesMetadata.TryGetValue(config.SaveableId, out SaveableMetadata metadata) ? metadata : new SaveableMetadata()
//             };
//             
//             saveObjects[config.SaveableId] = context;
//         }
//         return saveObjects;
//     }
// }