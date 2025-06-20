using System;
using UnityEngine;
using UnityEngine.Serialization;

#if (UNITY_EDITOR)
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public class ConsumerGOSaveableConfig
{
    [SerializeField] public string ConsumerId = null;  // Should get set by initialization code.
    public float LoadOrder = 0f;  // Used to determine the order in which saveables are loaded. Higher is later.
}

public abstract class SaveableGOConsumer : MonoBehaviour
{
    [SerializeField] protected SaveableGOProducer linkedSaveableProducer;
    [SerializeField] private ConsumerGOSaveableConfig saveableConfig;
    public ConsumerGOSaveableConfig SaveableConfig => saveableConfig;
    
    public abstract SaveableData GetSaveData();
    public abstract void LoadSaveData(SaveableData data, bool blankLoad);
    public virtual void HandleDestroyOnLoad(){ }  // By default, component just let themselves be destroyed with no side effects.
    
    public string GetProducerId()
    {
        if (linkedSaveableProducer == null)
        {
            Debug.LogWarning($"No linked producer set for {name}. Returning null.");
            return null;
        }
        return linkedSaveableProducer.Config.ProducerId;
    }

    #region Autoset

    private void AutosetConsumerId()
    {
        if (saveableConfig == null)
        {
            // If the saveableConfig is not set, we need to create it.
            saveableConfig = new ConsumerGOSaveableConfig();
        }
        
        if (string.IsNullOrEmpty(saveableConfig.ConsumerId))
        {
            // Then we are not playing and this is not a prefab so we are designing the level. It must have an ID.
            Debug.Log($"Initializing ConsumerId for {name}");
            saveableConfig.ConsumerId = Guid.NewGuid().ToString();
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                // Required to actually save the changes to the prefab instance. If we don't do this, changes will
                // visually appear in the editor, but are not "real". That was an annoying bug to track down.
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }
    }
    
    private void AutosetLinkedProducer()
    {
        if (linkedSaveableProducer == null)
        {
            // Try to find a linked producer on this object
            linkedSaveableProducer = GetComponent<SaveableGOProducer>();
            if (linkedSaveableProducer == null)
            {
                Debug.LogWarning($"No linked producer found for {name}. Please assign one in the Inspector.");
            }
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                // Required to actually save the changes to the prefab instance. If we don't do this, changes will
                // visually appear in the editor, but are not "real". That was an annoying bug to track down.
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }
    }
    
    private void RegisterConsumer()
    {
        if (linkedSaveableProducer != null)
        {
            linkedSaveableProducer.RegisterConsumer(this);
        }
        else
        {
            Debug.LogWarning($"Cannot register consumer {name} because no linked producer is set.");
        }
    }
    
    protected virtual void OnValidate()
    { 
        AutosetConsumerId();
        AutosetLinkedProducer();
        RegisterConsumer();
    }

    protected virtual void OnEnable()
    {
        if (saveableConfig == null || string.IsNullOrEmpty(saveableConfig.ConsumerId))
        {
            Debug.LogError($"Consumer {name} is enabled without a valid ConsumerId. This is likely due to OnValidate not being called before OnEnable. Please ensure that AutosetConsumerId is called in OnValidate.", this);
        }
        AutosetLinkedProducer();
        RegisterConsumer();
    }

    #endregion
}