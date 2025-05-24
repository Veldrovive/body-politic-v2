using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ActionCameraStartStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(ActionCameraStartState);

    public ActionCamSource CamConfig;

    public ActionCameraStartStateConfiguration()
    {
        CamConfig = new ActionCamSource();
    }
    
    public ActionCameraStartStateConfiguration(ActionCamSource camConfig)
    {
        CamConfig = camConfig;
    }
}

public enum ActionCameraStartStateOutcome
{
    SourceAdded
}

public class ActionCameraStartState : GenericAbstractState<ActionCameraStartStateOutcome, ActionCameraStartStateConfiguration>
{
    [SerializeField]
    private ActionCamSource camConfig = null;
        
    public override void ConfigureState(ActionCameraStartStateConfiguration configuration)
    {
        camConfig = configuration.CamConfig;
    }

    public override bool InterruptState()
    {
        return true;
    }

    private void OnEnable()
    {
        if (camConfig == null)
        {
            Debug.LogError("Camera configuration is null. Cannot set action camera source.");
            return;
        }

        if (camConfig.Target == null)
        {
            camConfig.Target = gameObject.transform;
        }

        if (string.IsNullOrEmpty(camConfig.SourceKey))
        {
            camConfig.SourceKey = $"NPC_CAM: {gameObject.name} ({gameObject.GetInstanceID()})";
        }

        ActionCameraManager.Instance?.AddActionCamSource(camConfig);
        TriggerExit(ActionCameraStartStateOutcome.SourceAdded);
    }
}