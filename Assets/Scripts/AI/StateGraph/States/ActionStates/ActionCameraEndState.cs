using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ActionCameraEndStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(ActionCameraEndState);

    public string SourceKey;
    
    public ActionCameraEndStateConfiguration()
    {
        SourceKey = "";
    }
    
    public ActionCameraEndStateConfiguration(string sourceKey)
    {
        SourceKey = sourceKey;
    }
}

public enum ActionCameraEndStateOutcome
{
    SourceRemoved
}

public class ActionCameraEndState : GenericAbstractState<ActionCameraEndStateOutcome, ActionCameraEndStateConfiguration>
{
    [SerializeField]
    private string sourceKey = null;
        
    public override void ConfigureState(ActionCameraEndStateConfiguration configuration)
    {
        sourceKey = configuration.SourceKey;
    }

    public override bool InterruptState()
    {
        return true;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(sourceKey))
        {
            sourceKey = $"NPC_CAM: {gameObject.name} ({gameObject.GetInstanceID()})";
        }

        ActionCameraManager.Instance?.RemoveActionCamSource(sourceKey);
        TriggerExit(ActionCameraEndStateOutcome.SourceRemoved);
    }
}