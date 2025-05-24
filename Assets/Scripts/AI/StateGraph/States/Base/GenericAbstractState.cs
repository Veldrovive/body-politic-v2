using System;
using Sisus.ComponentNames;
using UnityEngine;
using Component = System.ComponentModel.Component;

[RequireComponent(typeof(NpcContext))]
public abstract class GenericAbstractState<TOutcomeEnum, TConfiguration> : AbstractState
    where TOutcomeEnum : Enum
    where TConfiguration : AbstractStateConfiguration
{
    public string StateName => configuration?.StateName;
    public string StateId => configuration?.StateId;
    
    protected NpcContext npcContext;
    protected TConfiguration configuration;

    private void Awake()
    {
        npcContext = GetComponent<NpcContext>();
        enabled = false;  // States start disabled so that they can be configured before OnEnable is called.
    }

    protected void TriggerExit(TOutcomeEnum outcomeEnum)
    {
        TriggerExit(outcomeEnum.ToString());
    }
    
    public abstract void ConfigureState(TConfiguration configuration);
    public override void Configure(AbstractStateConfiguration newConfiguration)
    {
        if (newConfiguration is TConfiguration typedConfiguration)
        {
            configuration = typedConfiguration;
            if (!string.IsNullOrEmpty(newConfiguration.StateName))
            {
                this.SetName(newConfiguration.StateName);
            }
            ConfigureState(typedConfiguration);
        }
        else
        {
            Debug.LogError($"Invalid configuration type: {newConfiguration.GetType()} for state: {GetType()}");
        }
    }

    public abstract bool InterruptState();
    public override bool Interrupt()
    {
        return InterruptState();
    }
    
    protected void SetGlobalData(string key, object value)
    {
        if (npcContext != null)
        {
            npcContext.SetArbitraryAccessData(key, value);
        }
        else
        {
            Debug.LogError("NpcContext is not set. Cannot set global data.");
        }
    }
    
    protected void SetStateData(string key, object value)
    {
        if (npcContext != null)
        {
            string stateKey = $"{StateId}_{key}";
            npcContext.SetArbitraryAccessData(stateKey, value);
        }
        else
        {
            Debug.LogError("NpcContext is not set. Cannot set state data.");
        }
    }
    
    protected TStoredDataType GetGlobalData<TStoredDataType>(string key, TStoredDataType defaultValue)
    {
        if (npcContext != null)
        {
            return npcContext.GetArbitraryAccessData<TStoredDataType>(key, defaultValue);
        }
        else
        {
            Debug.LogError("NpcContext is not set. Cannot get global data.");
            return default;
        }
    }
    
    protected TStoredDataType GetStateData<TStoredDataType>(string key, TStoredDataType defaultValue)
    {
        if (npcContext != null)
        {
            string stateKey = $"{StateId}_{key}";
            return npcContext.GetArbitraryAccessData<TStoredDataType>(stateKey, defaultValue);
        }
        else
        {
            Debug.LogError("NpcContext is not set. Cannot get state data.");
            return default;
        }
    }
}