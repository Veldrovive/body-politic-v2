using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class LevelManagerSaveableData : SaveableData
{
    public List<GameObject> InfectedNpcs;
    public int LastControlledNpcIndex = -1; // Index of the currently controlled NPC in the InfectedNpcs list.
}

[DefaultExecutionOrder(-85)]
class LevelManager : SaveableGOConsumer
{
    [FormerlySerializedAs("cameraManager")] [SerializeField] private CameraManager_v2 controlledCameraManager;
    [SerializeField] private PlayerManagerV2 controlledPlayerManager;
    
    [Tooltip("View position to use as a backup when confused about what to look at.")]
    [SerializeField] private Transform fallbackView;
    
    [Tooltip("List of NPCs that are infected at the start of the level.")]
    [SerializeField] private List<NpcContext> initiallyInfectedNpcs;
    
    public static LevelManager Instance { get; private set; }

    /// <summary>
    /// We use a temporary object to position the camera to dynamically position the camera.
    /// </summary>
    private GameObject _tempObject;

    private bool _isLevelPaused = false;
    public bool IsLevelPaused => _isLevelPaused;
    
    private HashSet<NpcContext> _loadedNpcs = new HashSet<NpcContext>();
    public IReadOnlyCollection<NpcContext> LoadedNpcs => _loadedNpcs;
    
    private List<NpcContext> _infectedNpcs = new List<NpcContext>();
    public IReadOnlyCollection<NpcContext> InfectedNpcs => _infectedNpcs;
    
    private NpcContext _currentlyControlledNpc = null;

    public override SaveableData GetSaveData()
    {
        LevelManagerSaveableData data = new LevelManagerSaveableData();
        
        data.InfectedNpcs = _infectedNpcs.Select(npc => npc.gameObject).ToList();
        data.LastControlledNpcIndex = _currentlyControlledNpc != null ? 
            _infectedNpcs.ToList().IndexOf(_currentlyControlledNpc) : -1;

        return data;
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (blankLoad)
        {
            initiallyInfectedNpcs.RemoveAll(npc => npc == null || !npc.gameObject.activeInHierarchy);
            if (initiallyInfectedNpcs.Count > 0)
            {
                _infectedNpcs.AddRange(initiallyInfectedNpcs);
                SetCurrentlyControlledNpc(initiallyInfectedNpcs.FirstOrDefault(), immediate: true);
            }
            else
            {
                Debug.LogWarning($"No initially infected NPCs found. Fallback to null controlled NPC.", this);
                _currentlyControlledNpc = null;
                NullCurrentlyControlledNpc(immediate: true);
            }
        }
        else
        {
            if (data is not LevelManagerSaveableData levelData)
            {
                Debug.LogError("LevelManager received invalid save data.", this);
                return;
            }
            
            // Repopulate the infected NPCs list from the loaded data. We need to extract the NpcContext manually
            // because individual component are currently not serializable in my save system due to non-uniqueness.
            _infectedNpcs.Clear();
            foreach (var npcGameObject in levelData.InfectedNpcs)
            {
                if (npcGameObject.TryGetComponent<NpcContext>(out var npcContext))
                {
                    _infectedNpcs.Add(npcContext);
                }
                else
                {
                    Debug.LogWarning($"GameObject {npcGameObject.name} does not have NpcContext component. Skipping.", this);
                }
            }
            
            // If we have a controlled NPC index, set the currently controlled NPC.
            if (levelData.LastControlledNpcIndex >= 0 && 
                levelData.LastControlledNpcIndex < _infectedNpcs.Count)
            {
                SetCurrentlyControlledNpc(_infectedNpcs.ElementAt(levelData.LastControlledNpcIndex), immediate: true);
            }
            else
            {
                NullCurrentlyControlledNpc(immediate: true);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There is more than one instance of LevelManager!");
            return;
        }

        Instance = this;

        if (controlledCameraManager == null)
        {
            // Find the main camera manager in the scene.
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                controlledCameraManager = mainCamera.GetComponent<CameraManager_v2>();
                if (controlledCameraManager == null)
                {
                    Debug.LogError("Main camera does not have CameraManager_v2 component.");
                }
            }
            else
            {
                Debug.LogError("No main camera found in the scene. Please ensure there is a camera with CameraManager_v2 component.");
            }
        }

        if (controlledPlayerManager == null)
        {
            // It should be on this GameObject.
            controlledPlayerManager = GetComponent<PlayerManagerV2>();
            if (controlledPlayerManager == null)
            {
                Debug.LogError("No PlayerManagerV2 found on this GameObject. Please ensure it is attached.");
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        // Construct the temporary object
        _tempObject = new GameObject("LevelManager_TempObject");
        
        // Hook up input events
        GameManager.InputActions.PlayerControl.CycleCurrentThrall.performed += HandleCycleCurrentThrall;
    }

    private void OnDisable()
    {
        // Remove the temporary object
        if (_tempObject != null)
        {
            Destroy(_tempObject);
            _tempObject = null;
        }
        
        // Unhook input events
        GameManager.InputActions.PlayerControl.CycleCurrentThrall.performed -= HandleCycleCurrentThrall;
    }

    #region Public API

    public void RegisterNpc(NpcContext npcContext)
    {
        if (npcContext == null)
        {
            Debug.LogError("Cannot register a null NpcContext.");
            return;
        }
        
        if (_loadedNpcs.Add(npcContext))
        {
            // Debug.Log($"Registered NPC: {npcContext.name}");
        }
        else
        {
            Debug.LogWarning($"NPC {npcContext.name} is already registered.");
        }
    }
    
    #endregion

    #region Mid Level API

    private void SetViewToFallback(bool immediate = false)
    {
        if (fallbackView == null)
        {
            Debug.LogWarning("Fallback view is not set. Cannot set camera view to fallback.");
            return;
        }
        
        SetCameraViewTransform(fallbackView, immediate);
    }
    
    private void SetViewToTemporaryObject(bool immediate = false)
    {
        if (_tempObject == null)
        {
            Debug.LogError("Temporary object is not initialized. Cannot set camera view to temporary object.");
            SetViewToFallback(immediate);
            return;
        }
        
        SetCameraViewTransform(_tempObject.transform, immediate);
    }
    
    private void SetViewToNpc(NpcContext npcContext, bool immediate = false)
    {
        if (npcContext == null)
        {
            Debug.LogWarning("NpcContext is null. Cannot set camera view to NPC.");
            SetViewToFallback(immediate);
            return;
        }
        
        SetCameraViewTarget(npcContext.transform, immediate);
    }

    private bool SetCurrentlyControlledNpc(NpcContext npcContext, bool immediate = false)
    {
        Debug.Log($"Setting currently controlled NPC to {npcContext?.name ?? "null"}");
        if (npcContext == null)
        {
            Debug.LogWarning("NpcContext is null. Cannot set currently controlled NPC.");
            return false;
        }
        
        if (!_infectedNpcs.Contains(npcContext))
        {
            Debug.LogWarning($"NpcContext {npcContext.name} is not infected. Cannot set as currently controlled NPC.");
            return false;
        }
        
        if (_currentlyControlledNpc == npcContext)
        {
            // No change in currently controlled NPC, do nothing.
            return true;
        }
        
        // Otherwise we actually have to change the controlled NPC.
        _currentlyControlledNpc = npcContext;
        controlledPlayerManager.SetControlledNpc(_currentlyControlledNpc);
        SetViewToNpc(npcContext, immediate);

        return true;
    }

    private void NullCurrentlyControlledNpc(bool immediate = false)
    {
        if (_currentlyControlledNpc == null)
        {
            SetViewToFallback(immediate);
            return;
        }
        
        // We keep the camera in the same spot that it was before we nulled the controlled NPC by
        // positioning the temporary object at the current camera position and switching into fixed mode.
        // Set _tempObject.transform equal to controlledCameraManager.transform
        if (_tempObject == null)
        {
            Debug.LogError("Temporary object is not initialized. Save camera position.");
            // Instead, we move to the fallback view position.
            SetViewToFallback(immediate);
        }
        else
        {
            _tempObject.transform.position = controlledCameraManager.transform.position;
            _tempObject.transform.rotation = controlledCameraManager.transform.rotation;
            SetViewToTemporaryObject(immediate);
        }
        
        _currentlyControlledNpc = null;
        controlledPlayerManager.SetControlledNpc(null);
    }
    
    private void HandleCycleCurrentThrall(InputAction.CallbackContext context)
    {
        int nextIndex = -1;
        if (_currentlyControlledNpc != null)
        {
            int currentIndex = _infectedNpcs.IndexOf(_currentlyControlledNpc);
            if (currentIndex >= 0)
            {
                // Then we can just cycle to the next infected NPC.
                nextIndex = (currentIndex + 1) % _infectedNpcs.Count;
            }
            else if (_infectedNpcs.Count > 0)
            {
                Debug.LogWarning("Currently controlled NPC is not in the infected list. Cycling to the first infected NPC.");
                // If the controlled NPC is not in the infected list for some reason, default to the first infected NPC.
                nextIndex = 0;
            }
            else
            {
                Debug.LogError("Currently controlled NPC is not in the infected list and there are no infected NPCs to cycle to.");
            }
        }
        else
        {
            // Then we can just select the first infected NPC.
            if (_infectedNpcs.Count > 0)
            {
                nextIndex = 0;
            }
            else
            {
                Debug.LogWarning("No infected NPCs to cycle to. Nullifying currently controlled NPC.");
            }
        }

        if (nextIndex == -1)
        {
            // Then we couldn't find an NPC to transition to.
            NullCurrentlyControlledNpc();
        }
        else
        {
            SetCurrentlyControlledNpc(_infectedNpcs[nextIndex]);
        }
    }
    
    #endregion
    
    #region Low Level API

    private bool SetLevelPaused(bool paused)
    {
        if (_isLevelPaused == paused)
        {
            // No change in pause state, do nothing.
            return false;
        }
        _isLevelPaused = paused;
        
        Time.timeScale = paused ? 0f : 1f;
        return true;
    }
    
    private bool IsNpcInfected(NpcContext npcContext)
    {
        if (npcContext == null)
        {
            Debug.LogError("NpcContext is null. Cannot check if NPC is infected.");
            return false;
        }
        
        return _infectedNpcs.Contains(npcContext);
    }
    
    private string GetActionCamKey(NpcContext npcContext)
    {
        if (npcContext == null)
        {
            Debug.LogError("NpcContext is null. Cannot generate action camera key.");
            return string.Empty;
        }
        
        return $"NpcCamKey_{npcContext.name}";
    }

    private bool AddNpcActionCam(
        [NotNull] NpcContext npcContext,
        int priority = 10,
        ActionCameraMode mode = ActionCameraMode.ThirdPerson,
        float maxDuration = -1f
    )
    {
        if (ActionCameraManager.Instance == null)
        {
            Debug.LogWarning("ActionCameraManager is not initialized. Cannot add NPC action camera.");
            return false;
        }
        
        ActionCamSource source = new ActionCamSource(
            GetActionCamKey(npcContext),
            priority,
            npcContext.transform,
            mode,
            maxDuration
        );
        ActionCameraManager.Instance.AddActionCamSource(source);
        return true;
    }

    private bool RemoveNpcActionCam(NpcContext npcContext)
    {
        if (ActionCameraManager.Instance == null)
        {
            Debug.LogWarning("ActionCameraManager is not initialized. Cannot remove NPC action camera.");
            return false;
        }
        
        string actionCamKey = GetActionCamKey(npcContext);
        return ActionCameraManager.Instance.RemoveActionCamSource(actionCamKey);
    }

    /// <summary>
    /// Sets the camera to look at the target transform in an orbital mode, allowing the player
    /// to rotate around the target.
    /// </summary>
    /// <param name="targetTransform"></param>
    /// <param name="immediate"></param>
    private void SetCameraViewTarget(
        [NotNull] Transform targetTransform,
        bool immediate = false
    )
    {
        controlledCameraManager.SetCameraMode(
            CameraMode.Orbital,
            orbitalTarget: targetTransform,
            immediate: immediate
        );
    }

    /// <summary>
    /// Sets the camera to take the transform of the target.
    /// Effectively, we look from the target's perspective.
    /// </summary>
    /// <param name="targetTransform"></param>
    /// <param name="immediate"></param>
    private void SetCameraViewTransform(
        [NotNull] Transform targetTransform,
        bool immediate = false
    )
    {
        controlledCameraManager.SetCameraMode(
            CameraMode.FixedFollow,
            fixedTarget: targetTransform,
            immediate: immediate
        );
    }
    
    
    
    #endregion
}