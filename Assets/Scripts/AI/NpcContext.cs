using System;
using UnityEngine;
using UnityEngine.AI; // For NavMeshAgent

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
public class NpcContext : MonoBehaviour
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

    private static string NPC_LAYER_NAME = "NPC";

    /// <summary>
    /// Gets references to all essential NPC components. Logs errors if components are missing.
    /// </summary>
    void Awake()
    {
        Identity = GetComponent<NPCIdentity>();
        Inventory = GetComponent<NpcInventory>();
        StateGraphController = GetComponent<StateGraphController>();
        MovementManager = GetComponent<NpcMovementManager>();
        SuspicionTracker = GetComponent<NpcSuspicionTracker>();
        SpeechBubbleManager = GetComponent<SpeechBubbleManager>();
        AnimationManager = GetComponent<NpcAnimationManager>();
        NavMeshAgent = GetComponent<NavMeshAgent>();
        DetectorReactor = GetComponent<NpcDetectorReactor>();
        
        
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

    private void OnValidate()
    {
#if UNITY_EDITOR
        EnsureLayerIsSet();
#endif
    }
}