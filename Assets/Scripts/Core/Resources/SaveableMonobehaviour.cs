using System;
using UnityEditor;
using UnityEngine;

#if (UNITY_EDITOR)
using UnityEditor.SceneManagement;
#endif

public abstract class SaveableMonoBehaviour : MonoBehaviour, ISaveable
{
    [SerializeField] private SaveableConfig saveableConfig;
    public SaveableConfig SaveableConfig => saveableConfig;

    public abstract SaveableData GetSaveData();

    public abstract void LoadSaveData(SaveableData data);

    private void InitializeSaveable()
    {
        if (string.IsNullOrEmpty(saveableConfig.SaveableId))
        {
#if UNITY_EDITOR
            // Check if we are a prefab
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject) || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                // We are not a real instance, so the SaveableId should not be generated.
            }
            else
            {
                // This is either not a prefab or is an instance of a prefab. In either case, we need to generate a SaveableId.
                Debug.Log($"Initializing SaveableId for {gameObject.name}");
                saveableConfig.SaveableId = Guid.NewGuid().ToString();
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    // Required to actually save the changes to the prefab instance. If we don't do this, changes will
                    // visually appear in the editor, but are not "real". That was an annoying bug to track down.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }
            }
#else
            throw new InvalidOperationException("SaveableMonoBehaviour must have a SaveableId during runtime.");
#endif
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
            // The resource manager was already destroyed. This means the game has exited or the scene has been unloaded.
            // In any case we don't need to worry about cleanup as the ResourceDataManager has been entirely cleaned up.
        }
        else
        {
            ResourceDataManager.Instance.HandleSaveableDestroyed(this);
        }
    }
}