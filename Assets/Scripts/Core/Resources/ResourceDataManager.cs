using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveableConfig
{
    public string SaveableId = null;  // Should get set by initialization code.
    public float LoadOrder = 0f;  // Used to determine the order in which saveables are loaded. Higher is later.
}

[Serializable]
public class SaveableData
{
    
}

public class SaveableMetadata
{
    public GameObject prefab = null;
}

[DefaultExecutionOrder(-50)]
public class ResourceDataManager : MonoBehaviour
{
    public int NumSaveables;
    
    private Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();
    private Dictionary<string, SaveableMetadata> saveablesMetadata = new Dictionary<string, SaveableMetadata>();
    
    public static ResourceDataManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void RegisterSaveable(ISaveable saveable)
    {
        SaveableConfig config = saveable.SaveableConfig;
        if (config == null || string.IsNullOrEmpty(config.SaveableId))
        {
            Debug.LogError("SaveableConfig is null or SaveableId is empty. Cannot register saveable.");
            return;
        }
        
        if (saveables.ContainsKey(config.SaveableId))
        {
            // If the attached saveable is different, log a warning
            if (saveables[config.SaveableId] != saveable)
            {
                Debug.LogWarning($"Saveable with ID {config.SaveableId} is already registered. Overwriting the existing one.", this);
            }
        }
        
        saveables[config.SaveableId] = saveable;
        
        NumSaveables = saveables.Count;
    }
    
    public void UnregisterSaveable(ISaveable saveable)
    {
        SaveableConfig config = saveable.SaveableConfig;
        if (config == null || string.IsNullOrEmpty(config.SaveableId))
        {
            Debug.LogError("SaveableConfig is null or SaveableId is empty. Cannot unregister saveable.");
            return;
        }
        
        if (!saveables.Remove(config.SaveableId))
        {
            Debug.LogWarning($"Saveable with ID {config.SaveableId} is not registered.", this);
        }
        
        saveablesMetadata.Remove(config.SaveableId);  // We don't care if it actually existed or not, just remove it.
        
        NumSaveables = saveables.Count;
    }
}