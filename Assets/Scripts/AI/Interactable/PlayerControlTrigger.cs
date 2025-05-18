using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

public enum PlayerControlTriggerActionType
{
    MoveAndUse,  // Standard "MoveTo->Interact" sequence
    CustomStateGraphFactory,  // Calls an external function that returns an AbstractGraphFactory
}

/// <summary>
/// Component placed on GameObjects to define how the player can interact with them via player control.
/// It acts as a data source, specifying either parameters for a standard "MoveTo->Interact" sequence
/// (targeting a specific Interactable and InteractionDefinition) or linking to a CustomPlayerActionSO
/// for more complex sequences. It also provides references for highlighting.
/// </summary>
[RequireComponent(typeof(PlayerControlTriggerVisualDefinition))]
public class PlayerControlTrigger : MonoBehaviour
{
    private string playerControlTriggerLayerName = "PlayerControlTrigger";

    [Header("Action Type")]
    [Tooltip("Check this box to use a Custom Action ScriptableObject instead of defining parameters for a standard 'MoveTo->Interact' sequence.")]
    [SerializeField] private bool useCustomAction = false;
    
    [Tooltip("A unique identifier for the graph that allows for checking if the graph is in a NPCs action queue.")]
    [SerializeField] private string StateGraphGUID = System.Guid.NewGuid().ToString();
    [Tooltip("If true, attempting to add this graph to the NPCs queue twice will result in failure.")]
    [SerializeField] private bool PreventDuplicates = false;
    
    // --- Custom Action ---
    [Header("Custom Action (If Use Custom Action is true)")]
    [Tooltip("Where this action should be available from")]
    [SerializeField] List<InteractionAvailableFrom> customAvailableFroms = new List<InteractionAvailableFrom>{InteractionAvailableFrom.World};
    
    [Tooltip("The Custom Action Scriptable Object defining the sequence.")]
    [SerializeField] private AbstractCustomPlayerAction customAction;

    // --- Standard Action Parameters (If Use Custom Action is false) ---
    [Header("Standard Action Parameters")]
    [Tooltip("The Interactable component that will receive the interaction after the MoveTo state (if any). Defaults to Interactable on this GameObject if null.")]
    [SerializeField] private Interactable targetInteractableObject;

    [Tooltip("The specific Interaction Definition SO (which must be configured on the Target Interactable Object) to execute as the standard action.")]
    [SerializeField] private InteractionDefinitionSO targetInteractionDefinition;

    [Tooltip("Optional destination transform for the initial MoveTo state. If null, the system generating the states will likely move near the Target Interactable Object.")]
    [SerializeField] private Transform moveToTargetTransform; // Where to stand before interaction
    
    [Tooltip("Whether to require that position is exact when moving to the target.")]
    [SerializeField] private bool requireExactPosition = true;
    [Tooltip("Whether to require that rotation is exact when moving to the target.")]
    [SerializeField] private bool requireFinalAlignment = true;
    
    // --- Public Accessors ---

    /// <summary>
    /// Gets the Interactable component targeted by this trigger for standard actions.
    /// </summary>
    public Interactable TargetInteractable => targetInteractableObject;

    /// <summary>
    /// Gets the specific Interaction Definition targeted by this trigger for standard actions.
    /// </summary>
    public InteractionDefinitionSO TargetInteractionDefinition => targetInteractionDefinition;

    /// <summary>
    /// Gets the optional override transform for the MoveTo destination for standard actions.
    /// Returns null if no specific destination is set (allowing the state generation logic to default).
    /// </summary>
    public Transform MoveToTargetTransform => moveToTargetTransform;

    /// <summary>
    /// Gets whether this trigger is configured to use a Custom Action SO.
    /// </summary>
    public bool IsCustomAction => useCustomAction;

    /// <summary>
    /// Gets the Custom Action SO assigned to this trigger. Returns null if IsCustomAction is false or if none is assigned.
    /// </summary>
    public AbstractCustomPlayerAction CustomAction => useCustomAction ? customAction : null;

    public List<InteractionAvailableFrom> InteractionAvailableFroms =>
        useCustomAction ? customAvailableFroms : targetInteractionDefinition.InteractionAvailableFroms;

    public string Title => useCustomAction ? customAction?.DisplayName : targetInteractionDefinition?.DisplayName;
    public string Description => useCustomAction ? customAction?.Description : targetInteractionDefinition?.Description;
    
    /// <summary>
    /// Checks the status of the action defined by this trigger for a given initiator.
    /// This considers roles, proximity, and the enabled state of the interaction instance.
    /// </summary>
    /// <param name="initiator">The GameObject attempting the action.</param>
    /// <returns>An InteractionStatus object detailing the action's possibility and visibility.</returns>
    public InteractionStatus GetActionStatus(GameObject initiator)
    {
        // Determine the effective interactable target. For custom actions, it might still
        // be relevant for the custom SO's GetStatus, so we use the assigned targetInteractableObject.
        // Ensure the targetInteractableObject is up-to-date (e.g., via Awake or manual assignment).

        if (useCustomAction)
        {
            // If using a custom action, delegate the status check to the CustomPlayerActionSO.
            // Ensure the custom action SO is assigned.
            if (customAction == null)
            {
                Debug.LogError($"PlayerControlTrigger on {gameObject.name}: Trying to GetStatus for a custom action, but no CustomPlayerActionSO is assigned.", this);
                // Return a default failure status if the custom action is missing.
                // return new InteractionStatus { CanInteract = false, IsVisible = false, FailureReasons = { InteractionFailureReason.InternalError } }; // Or a more specific reason
                InteractionStatus failedStatus = new InteractionStatus();
                failedStatus.IsVisible = true;
                failedStatus.AddFailureReason(new HumanReadableFailureReason(
                    InteractionFailureReason.InternalError,
                    0,
                    "Custom action is missing."
                ));
                return failedStatus;
            }
            // Pass the initiator. There is no target interactable in this case as custom actions define their own.
            InteractionStatus status = customAction.GetStatus(initiator, null);
            
            // Check if this graph is already in the interaction queue of the initiator
            NpcContext initiatorContext = initiator.GetComponent<NpcContext>();
            if (initiatorContext != null && PreventDuplicates && initiatorContext.StateGraphController.HasGraphInQueue(StateGraphGUID))
            {
                // Then we should not allow this graph to be added again
                status.AddFailureReason(new HumanReadableFailureReason(
                    InteractionFailureReason.AlreadyExecuting,
                    5,
                    "I don't need to do that twice."
                ));
            }
            return status;
        }
        else
        {
            Interactable effectiveTarget = targetInteractableObject; // Use the cached/assigned interactable

            // If using a standard action, delegate the status check to the InteractionDefinitionSO.
            // Ensure the interaction definition SO is assigned.
            if (targetInteractionDefinition == null)
            {
                Debug.LogError($"PlayerControlTrigger on {gameObject.name}: Trying to GetStatus for a standard action, but no TargetInteractionDefinition is assigned.", this);
                // Return a default failure status if the definition is missing.
                InteractionStatus failedStatus = new InteractionStatus();
                failedStatus.IsVisible = true;
                failedStatus.AddFailureReason(new HumanReadableFailureReason(
                    InteractionFailureReason.InternalError,
                    0,
                    "Interaction definition is missing."
                ));
                return failedStatus;
            }
            // Ensure the target interactable object is assigned for the standard action check.
            if (effectiveTarget == null)
            {
                 Debug.LogError($"PlayerControlTrigger on {gameObject.name}: Trying to GetStatus for a standard action, but TargetInteractableObject is null.", this);
                // Return a default failure status if the target is missing.
                InteractionStatus failedStatus = new InteractionStatus();
                failedStatus.IsVisible = true;
                failedStatus.AddFailureReason(new HumanReadableFailureReason(
                    InteractionFailureReason.InternalError,
                    0,
                    "Target interactable object is missing."
                ));
                return failedStatus;
            }
            // Perform the status check using the standard interaction definition and target.
            InteractionStatus interactionStatus = targetInteractionDefinition.GetStatus(initiator, effectiveTarget);
            
            // Check if this graph is already in the interaction queue of the initiator
            NpcContext initiatorContext = initiator.GetComponent<NpcContext>();
            if (initiatorContext != null && PreventDuplicates && initiatorContext.StateGraphController.HasGraphInQueue(StateGraphGUID))
            {
                // Then we should not allow this graph to be added again
                interactionStatus.AddFailureReason(new HumanReadableFailureReason(
                    InteractionFailureReason.AlreadyExecuting,
                    5,
                    "I don't need to do that twice."
                ));
            }
            // Return the status object.
            return interactionStatus;
        }
    }
    
    /// <summary>
    /// Checks whether this trigger is available from a certain layer.
    /// </summary>
    /// <param name="queryAvailableFrom"></param>
    /// <returns></returns>
    public bool IsInteractionAvailableFrom(InteractionAvailableFrom queryAvailableFrom)
    {
        return InteractionAvailableFroms.Contains(queryAvailableFrom);
    }

    /// <summary>
    /// Generates the sequence of states (StateGraph) required to execute the action defined by this trigger.
    /// </summary>
    /// <param name="context">Contextual information needed to generate the graph (especially for custom actions).</param>
    /// <returns>A StateGraph representing the sequence of states, or null if generation fails.</returns>
    public AbstractGraphFactory GetGraphDefinition(NpcContext initiator)
    {
        PlayerActionContext context = new PlayerActionContext(initiator, targetInteractableObject, this);
        if (useCustomAction)
        {
            // If using a custom action, delegate graph generation to the CustomPlayerActionSO.
            if (customAction == null)
            {
                Debug.LogError($"PlayerControlTrigger on {gameObject.name}: Trying to GetGraphDefinition for a custom action, but no CustomPlayerActionSO is assigned.", this);
                return null; // Cannot generate graphs without the definition.
            }
            // Generate the graph using the custom action's logic, passing the provided context.
            AbstractGraphFactory customGraph = customAction.GenerateGraph(context);
            if (!string.IsNullOrEmpty(StateGraphGUID))
            {
                customGraph.SetGraphId(StateGraphGUID);
            }
            return customGraph;
        }
        else
        {
            MoveAndUseGraphFactory graphFactory = new MoveAndUseGraphFactory(new MoveAndUseGraphConfiguration()
            {
                MoveToTargetTransform = moveToTargetTransform,
                RequireExactPosition = requireExactPosition,
                RequireFinalAlignment = requireFinalAlignment,
                TargetInteractable = targetInteractableObject,
                TargetInteractionDefinition = targetInteractionDefinition,
            }, StateGraphGUID);
            return graphFactory;
        }
    }
    
    void AutoSetInteractable()
    {
        if (!useCustomAction)
        {
            // Attempt to find the Interactable component on this GameObject or its parent
            if (targetInteractableObject == null){
                // We look for it on the parent first, as this is the most common case.
                targetInteractableObject = GetComponentInParent<Interactable>();
                // If not found, we look on this GameObject
                if (targetInteractableObject == null)
                {
                    targetInteractableObject = GetComponent<Interactable>();
                }
                // If still not found, we log a warning
#if UNITY_EDITOR
                if (targetInteractableObject == null)
                {
                    Debug.LogWarning($"PlayerControlTrigger on {gameObject.name}: No Interactable component found. Please assign one in the Inspector or ensure it's on this GameObject or its parent.", this);
                }
#endif
            }   
        }
    }
    
    /// <summary>
    /// Attempts to find the Interactable and Renderer components if not assigned in the Inspector.
    /// </summary>
    void Awake()
    {
        // Auto-assign target interactable if not set
        AutoSetInteractable();
    }
    
    /// <summary>
    /// Sets the GameObject's layer based on TARGET_LAYER_NAME using LayerMask.NameToLayer.
    /// Logs an error if the layer is not defined in the Tag Manager.
    /// </summary>
    private void EnsureLayerIsSet()
    {
        int targetLayer = LayerMask.NameToLayer(playerControlTriggerLayerName);
        if (targetLayer == -1) // LayerMask.NameToLayer returns -1 if the layer name doesn't exist
        {
            #if UNITY_EDITOR
            // Provide a more helpful error message in the editor
            Debug.LogError($"PlayerControlTrigger on '{gameObject.name}': The layer '{playerControlTriggerLayerName}' is not defined in the Tag Manager (Project Settings > Tags and Layers). Please define it.", this);
            #else
            // Runtime error
            Debug.LogError($"PlayerControlTrigger on '{gameObject.name}': Layer '{playerControlTriggerLayerName}' not defined.", this);
            #endif
        }
        else if (gameObject.layer != targetLayer)
        {
            // Only set the layer if it's not already correct
            gameObject.layer = targetLayer;
            // Debug.Log($"PlayerControlTrigger on '{gameObject.name}': Set layer to '{TARGET_LAYER_NAME}' ({targetLayer}).", this); // Optional log
        }
    }

    #if UNITY_EDITOR
    /// <summary>
    /// [EDITOR ONLY] Called when the component is first added or Reset is used.
    /// Sets the initial layer for the GameObject.
    /// </summary>
    void Reset()
    {
        // We call EnsureLayerIsSet here to apply the layer when the component is added in the editor.
        EnsureLayerIsSet();

        // Auto-assign the Interactable and Renderer components if not set
        AutoSetInteractable();
        
        // Optional: You could also auto-assign other components here if desired, similar to Awake.
        // Example:
        // if (targetInteractableObject == null) targetInteractableObject = GetComponent<Interactable>();
        // if (meshForHighlighting == null) { /* find renderer */ }
    }

    /// <summary>
    /// [EDITOR ONLY] Called when the script is loaded or a value changes in the Inspector.
    /// Ensures the layer remains correct and provides validation feedback.
    /// </summary>
    void OnValidate()
    {
        // Ensure the layer is correct whenever the component is validated in the editor
        EnsureLayerIsSet();

        // We don't want to auto-assign the Interactable or Renderer here, as it may not be desired.

        if (!PrefabUtility.IsPartOfPrefabAsset(this.gameObject))
        {
            // --- Original Validation Logic ---
            if (useCustomAction)
            {
                if (customAction == null)
                {
                    Debug.LogWarning($"PlayerControlTrigger on {gameObject.name}: 'Use Custom Action' is checked, but no Custom Action SO is assigned.", this);
                }
            }
            else // Standard Action Mode
            {
                if (targetInteractableObject == null)
                {
                    // Warning, but Awake might fix it at runtime. Reset might fix it at add time.
                    if (GetComponent<Interactable>() == null) { // Only warn if definitely missing now
                        Debug.LogWarning($"PlayerControlTrigger on {gameObject.name}: Standard Action mode selected, and no Target Interactable Object assigned or found on this GameObject.", this);
                    }
                }
                if (targetInteractionDefinition == null)
                {
                    Debug.LogWarning($"PlayerControlTrigger on {gameObject.name}: Standard Action mode selected, but no Target Interaction Definition is assigned.", this);
                }
            }
            // --- End Original Validation Logic ---   
        }
    }
    #endif
}