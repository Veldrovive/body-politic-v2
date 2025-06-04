using System;
using UnityEditor;
using UnityEngine;

public abstract class SaveableMonobehavior : MonoBehaviour, ISaveable
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
            // Check if we are a prefab
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                // We are not a real instance, so the SaveableId should not be generated.
            }
            else
            {
                // This is either not a prefab or is an instance of a prefab. In either case, we need to generate a SaveableId.
                saveableConfig.SaveableId = Guid.NewGuid().ToString();
            }
        }
    }

    protected virtual void Awake()
    {
        InitializeSaveable();
    }

    protected virtual void Reset()
    {
        InitializeSaveable();
    }

    protected virtual void OnValidate()
    {
        InitializeSaveable();
    }

    protected virtual void Start()
    {
        InitializeSaveable();  // Ensure the SaveableId is initialized before registering
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

    protected virtual void OnDestroy()
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