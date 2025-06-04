using System;
using UnityEngine;

public abstract class SaveableScriptableObject : ScriptableObject, ISaveable
{
    [SerializeField] private SaveableConfig saveableConfig;
    public SaveableConfig SaveableConfig => saveableConfig;

    public virtual SaveableData GetSaveData()
    {
        throw new NotImplementedException();
    }
    
    public virtual void LoadSaveData(SaveableData data)
    {
        throw new NotImplementedException();
    }
    
    private void InitializeSaveable()
    {
        if (string.IsNullOrEmpty(saveableConfig.SaveableId))
        {
            saveableConfig.SaveableId = Guid.NewGuid().ToString();
        }
    }

    private void Awake()
    {
        InitializeSaveable();
        // On start we need to register with the ResourceDataManager
        if (ResourceDataManager.Instance == null)
        {
            Debug.LogError("ResourceDataManager is null");
        }
        else
        {
            ResourceDataManager.Instance.RegisterSaveable(this);
        }
    }

    private void OnDestroy()
    {
        // On destroy we need to unregister with the ResourceDataManager
        if (ResourceDataManager.Instance == null)
        {
            Debug.LogError("ResourceDataManager is null");
        }
        else
        {
            ResourceDataManager.Instance.UnregisterSaveable(this);
        }
    }
}