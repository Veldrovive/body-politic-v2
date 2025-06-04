using System;
using System.Collections.Generic;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization; // For NavMeshAgent

public class NpcSaveData : SaveableData
{
    // Transform
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    
    // Identity
    public List<NpcRoleSO> DynamicRoles;
    
    // Inventory
    public string HeldItemSaveableId;  // References the saveable ID of the held item, if any.
    public List<string> InventorySlotsSaveableIds;  // References the saveable IDs of the items in the inventory.
    
    // Suspicion
    public List<NpcSuspicionTracker.SuspicionSourceState> SuspicionSources; // List of active suspicion sources with their states.
    
    // Interactable Npc component manages its own save data, so we don't need to store anything here.
}

// Add required components for the new controllers
[RequireComponent(typeof(NPCIdentity))] // [cite: 765]
[RequireComponent(typeof(NpcInventory))]
[RequireComponent(typeof(StateGraphController))]
[RequireComponent(typeof(NpcMovementManager))]
[RequireComponent(typeof(NpcSuspicionTracker))]
[RequireComponent(typeof(SpeechBubbleManager))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcAnimationManager))]
[RequireComponent(typeof(NpcDetectorReactor))]
[RequireComponent(typeof(InteractableNpc))]
[RequireComponent(typeof(NpcSoundHandler))]
public class NpcContext : SaveableMonobehavior
{
    public NPCIdentity Identity { get; private set; }
    public NpcInventory Inventory { get; private set; }
    public StateGraphController StateGraphController { get; private set; }
    public NpcMovementManager MovementManager { get; private set; }
    public NpcSuspicionTracker SuspicionTracker { get; private set; }
    public SpeechBubbleManager SpeechBubbleManager { get; private set; }
    public NpcAnimationManager AnimationManager { get; private set; }
    public NavMeshAgent NavMeshAgent { get; private set; }
    public NpcDetectorReactor DetectorReactor { get; private set; }
    public InteractableNpc InteractableNpc { get; private set; }
    
    public NpcSoundHandler SoundHandler { get; private set; }
    
    private Dictionary<string, object> arbitraryAccessData { get; set; }

    private static string NPC_LAYER_NAME = "NPC";

    public Transform InfectPoint;

    /// <summary>
    /// Gets references to all essential NPC components. Logs errors if components are missing.
    /// </summary>
    protected override void Awake()
    {
        Debug.Log("Awake called for NPC " + gameObject.name);
        base.Awake();
        
        arbitraryAccessData = new Dictionary<string, object>();
        
        Identity = GetComponent<NPCIdentity>();
        Inventory = GetComponent<NpcInventory>();
        StateGraphController = GetComponent<StateGraphController>();
        MovementManager = GetComponent<NpcMovementManager>();
        SuspicionTracker = GetComponent<NpcSuspicionTracker>();
        SpeechBubbleManager = GetComponent<SpeechBubbleManager>();
        AnimationManager = GetComponent<NpcAnimationManager>();
        NavMeshAgent = GetComponent<NavMeshAgent>();
        DetectorReactor = GetComponent<NpcDetectorReactor>();
        InteractableNpc = GetComponent<InteractableNpc>();
        SoundHandler = GetComponent<NpcSoundHandler>();
        
        
        // Register with the InfectionManager
        if (InfectionManager.Instance != null)
        {
            InfectionManager.Instance.RegisterNpc(this);
        }
        else
        {
            Debug.LogError("InfectionManager instance not found. NPC will not be registered.", this);
        }
    }
    
    /// <summary>
    /// Sets the GameObject's layer based on NPC_LAYER_NAME using LayerMask.NameToLayer.
    /// Logs an error if the layer is not defined in the Tag Manager.
    /// </summary>
    private void EnsureLayerIsSet()
    {
        int targetLayer = LayerMask.NameToLayer(NPC_LAYER_NAME);
        if (targetLayer == -1) // LayerMask.NameToLayer returns -1 if the layer name doesn't exist
        {
#if UNITY_EDITOR
            // Provide a more helpful error message in the editor
            Debug.LogError($"PlayerControlTrigger on '{gameObject.name}': The layer '{NPC_LAYER_NAME}' is not defined in the Tag Manager (Project Settings > Tags and Layers). Please define it.", this);
#else
            // Runtime error
            Debug.LogError($"PlayerControlTrigger on '{gameObject.name}': Layer '{NPC_LAYER_NAME}' not defined.", this);
#endif
        }
        else if (gameObject.layer != targetLayer)
        {
            // Only set the layer if it's not already correct
            gameObject.layer = targetLayer;
        }
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
#if UNITY_EDITOR
        EnsureLayerIsSet();
#endif
    }

    public void TriggerDeath()
    {
        AnimationManager.Play("Death");

        StateGraphController.enabled = false;
        MovementManager.enabled = false;
        DetectorReactor.enabled = false;
        InteractableNpc.enabled = false;
    }
    
    public void SetArbitraryAccessData(string key, object value)
    {
        arbitraryAccessData[key] = value;
    }
    
    public object GetArbitraryAccessData(string key, object defaultValue)
    {
        if (arbitraryAccessData.TryGetValue(key, out var value))
        {
            return value;
        }
        else
        {
            return defaultValue;
        }
    }
    
    public TStoredDataType GetArbitraryAccessData<TStoredDataType>(string key, TStoredDataType defaultValue)
    {
        if (arbitraryAccessData.TryGetValue(key, out var value))
        {
            if (value is TStoredDataType storedValue)
            {
                return storedValue;
            }
            else
            {
                Debug.LogWarning($"Key '{key}' found but value is not of type {typeof(TStoredDataType)}.", this);
                return defaultValue;
            }
        }
        else
        {
            return defaultValue;
        }
    }

    #region Sounds
    
    // Stroll 24 bpm - Velocity 1
    // Walk 99 bpm - Velocity 2
    // Run 146 bpm - Velocity 3
    // Sprint 173 - Velocity 5
    
    [Header("Sounds")]
    [SerializeField] private List<AudioClip> defaultFootStepSounds;

    [SerializeField] private float footstepZThreshold = 0f;
    [SerializeField] private Transform rightFootTransform;
    [SerializeField] private Transform leftFootTransform;

    private float lastRightFoodLocalZ = 0f;
    private float lastLeftFoodLocalZ = 0f;

    private AudioClip GetFootstepClip()
    {
        // Chooses a random footstep sound from the list of default footstep sounds.
        if (defaultFootStepSounds == null || defaultFootStepSounds.Count == 0)
        {
            Debug.LogWarning("No footstep sounds defined. Using a placeholder sound.", this);
            return null; // Placeholder or default sound
        }
        
        int randomIndex = UnityEngine.Random.Range(0, defaultFootStepSounds.Count);
        return defaultFootStepSounds[randomIndex];
    }
    
    private void HandleSoundCreation()
    {
        // *** Footsteps ***
        // If you have any suspicion, the footstep sound gains a suspiciousness of 1. Otherwise it has a suspiciousness of 0.
        bool didStep = false;
        if (rightFootTransform != null)
        {
            // float newRightFootLocalZ = rightFootTransform.position.z - transform.position.z;
            Vector3 rightFootTransformLocalPosition = rightFootTransform.position - transform.position;
            // Rotate this into the frame of the NPC
            rightFootTransformLocalPosition = Quaternion.Inverse(transform.rotation) * rightFootTransformLocalPosition;
            float newRightFootLocalZ = rightFootTransformLocalPosition.z;
            if (lastRightFoodLocalZ > footstepZThreshold && newRightFootLocalZ < footstepZThreshold)
            {
                didStep = true;
            }
            lastRightFoodLocalZ = newRightFootLocalZ;
        }
        if (leftFootTransform != null)
        {
            // float newLeftFootLocalZ = leftFootTransform.position.z - transform.position.z;
            Vector3 leftFootTransformLocalPosition = leftFootTransform.position - transform.position;
            // Rotate this into the frame of the NPC
            leftFootTransformLocalPosition = Quaternion.Inverse(transform.rotation) * leftFootTransformLocalPosition;
            float newLeftFootLocalZ = leftFootTransformLocalPosition.z;
            if (lastLeftFoodLocalZ > footstepZThreshold && newLeftFootLocalZ < footstepZThreshold)
            {
                didStep = true;
            }
            lastLeftFoodLocalZ = newLeftFootLocalZ;
        }
        

        if (didStep)
        {
            SoundData footstepSoundData = new SoundData
            {
                Clip = GetFootstepClip(),
                EmanationPoint = transform.position,
                Suspiciousness = SuspicionTracker.CurrentSuspicionLevel > 0 ? 1 : 0,
                CausesReactions = SuspicionTracker.CurrentSuspicionLevel > 0,
                Loudness = SoundLoudness.Quiet,
                CreatorObject = gameObject,
                SType = SoundType.Footstep
            };
            SoundHandler.RaiseSoundEvent(footstepSoundData);
        }
    }

    #endregion
    
    private void Update()
    {
        HandleSoundCreation();
    }
}