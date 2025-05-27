using System;
using System.Collections.Generic;
using UnityEngine;

public enum ActionCameraMode
{
    FirstPerson,
    ThirdPerson,
    TopDown
}

[Serializable]
public class ActionCamSource
{
    public string SourceKey;
    public int Priority;
    public Transform Target;
    public ActionCameraMode Mode;
    public float MaxDuration;

    [Tooltip("Used in ThirdPerson mode to position the camera")]
    public Vector3 ThirdPersonOffset = new Vector3(0, 5, -1.5f);
    [Tooltip("Used in TopDown mode to position the camera")]
    public Vector3 TopDownOffset = new Vector3(0, 5, 0);
    
    public float FOV = 60f;

    private float startTime;
        
    public ActionCamSource(string sourceKey, int priority, Transform target, ActionCameraMode mode, float maxDuration)
    {
        SourceKey = sourceKey;
        Priority = priority;
        Target = target;
        Mode = mode;
        MaxDuration = maxDuration;
    }

    public ActionCamSource()
    {
        SourceKey = "";
        Priority = 0;
        Target = null;
        Mode = ActionCameraMode.ThirdPerson;
        MaxDuration = -1f;
    }
    
    public void ResetStartTime()
    {
        startTime = Time.time;
    }
        
    public bool IsExpired()
    {
        return MaxDuration > 0 && Time.time - startTime > MaxDuration;
    }
}

public class ActionCameraManager : MonoBehaviour
{
    [SerializeField] private Camera actionCamera;
    [SerializeField] private bool actionCameraEnabled = false;
    
    [SerializeField] private Rect cameraViewRect = new Rect(0f, 0.75f, 0.25f, 0.25f);
    
    [Header("Testing")]
    [SerializeField] private bool testing = false;

    [SerializeField] private Transform testTarget;
    [SerializeField] private ActionCameraMode testMode = ActionCameraMode.FirstPerson;
    [SerializeField] private float testMaxDuration = 5f;
    
    private ActionCamSource currentActionCamSource;
    private Dictionary<string, ActionCamSource> actionCamSources = new Dictionary<string, ActionCamSource>();
    
    public static ActionCameraManager Instance { get; private set; }
    
    private void Awake()
    {
        if (actionCamera == null)
        {
            actionCamera = GetComponent<Camera>();
        }
        
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple instances of ActionCameraManager detected. Destroying this instance.");
            Destroy(gameObject);
        }
    }

    public void AddActionCamSource(ActionCamSource source)
    {
        source.ResetStartTime();
        if (!actionCamSources.ContainsKey(source.SourceKey))
        {
            actionCamSources.Add(source.SourceKey, source);
        }
        else
        {
            actionCamSources[source.SourceKey] = source;
        }
    }
    
    public void AddActionCamSource(string sourceKey, int priority, Transform target, ActionCameraMode mode, float maxDuration)
    {
        AddActionCamSource(new ActionCamSource(sourceKey, priority, target, mode, maxDuration));
    }
    
    public bool RemoveActionCamSource(string sourceKey)
    {
        return actionCamSources.Remove(sourceKey);
    }

    public void Start()
    {
        if (testing)
        {
            AddActionCamSource("TestSource", 1, testTarget, testMode, testMaxDuration);
        }
    }

    private void UpdateCameraState()
    {
        if (currentActionCamSource == null) return;
        
        switch (currentActionCamSource.Mode)
        {
            case ActionCameraMode.FirstPerson:
                actionCamera.transform.position = currentActionCamSource.Target.position;
                actionCamera.transform.rotation = currentActionCamSource.Target.rotation;
                break;
            case ActionCameraMode.ThirdPerson:
                actionCamera.transform.position = currentActionCamSource.Target.position + currentActionCamSource.ThirdPersonOffset;
                actionCamera.transform.LookAt(currentActionCamSource.Target);
                break;
            case ActionCameraMode.TopDown:
                actionCamera.transform.position = currentActionCamSource.Target.position + currentActionCamSource.TopDownOffset;
                actionCamera.transform.LookAt(currentActionCamSource.Target);
                break;
            default:
                Debug.LogError($"Unknown ActionCameraMode: {currentActionCamSource.Mode}");
                return;
        }
        actionCamera.fieldOfView = currentActionCamSource.FOV;
    }

    private void Update()
    {
        if (actionCamera != null)
        {
            if (actionCameraEnabled)
            {
                actionCamera.rect = cameraViewRect;
            }
            else
            {
                actionCamera.enabled = false;
                return;
            }
        }
        
        List<string> sourcesToRemove = new List<string>();
        currentActionCamSource = null;
        int highestPriority = int.MinValue;
        foreach (var source in actionCamSources.Values)
        {
            if (source.IsExpired())
            {
                sourcesToRemove.Add(source.SourceKey);
                continue;
            }

            if (source.Priority > highestPriority)
            {
                highestPriority = source.Priority;
                currentActionCamSource = source;
            }
        }
        
        foreach (var sourceKey in sourcesToRemove)
        {
            actionCamSources.Remove(sourceKey);
        }

        if (currentActionCamSource != null)
        {
            actionCamera.enabled = true;
            UpdateCameraState();
        }
        else
        {
            actionCamera.enabled = false;
        }
    }
}