using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class SaveableScriptableObject : ScriptableObject, ISaveable
{
    [SerializeField] private SaveableConfig saveableConfig;
    public SaveableConfig SaveableConfig => saveableConfig;

    public abstract SaveableData GetSaveData();

    public abstract void LoadSaveData(SaveableData data);
    
    private void InitializeSaveable()
    {
        if (string.IsNullOrEmpty(saveableConfig.SaveableId))
        {
            saveableConfig.SaveableId = Guid.NewGuid().ToString();
        }
    }
    
    private void RegisterSaveable()
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
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // We need to register with the ResourceDataManager for the scene every time a scene is loaded since that
        // component is scene-specific to avoid saving data across scenes.
        RegisterSaveable();
    }

    private void Awake()
    {
        RegisterSaveable();
        
        SceneManager.sceneLoaded += OnSceneLoaded;
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
            ResourceDataManager.Instance.HandleSaveableDestroyed(this);
        }
    }
}