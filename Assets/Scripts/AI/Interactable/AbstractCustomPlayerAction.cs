using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Data structure holding contextual information needed for generating the steps of a custom player action.
/// Passed to CustomPlayerActionHandler.GenerateStep.
/// </summary>
[System.Serializable]
public struct PlayerActionContext
{
    /// <summary>The NPC initiating the action.</summary>
    public NpcContext InitiatorNpc;
    /// <summary>The primary GameObject that was interacted with (the one with the PlayerControlTrigger).</summary>
    public Interactable InteractedObject;
    /// <summary>The specific PlayerControlTrigger component instance that was activated.</summary>
    public PlayerControlTrigger TriggerComponent;
    // --- Potentially add a direct reference to the handler that was activated ---
    // public CustomPlayerActionHandler ActivatedHandler;

    /// <summary>
    /// Constructor for PlayerActionContext.
    /// </summary>
    /// <param name="initiator">The context of the NPC initiating the action.</param>
    /// <param name="interactedObject">The primary Interactable that was triggered.</param>
    /// <param name="trigger">The specific trigger component activated.</param>
    public PlayerActionContext(NpcContext initiator, Interactable interactedObject, PlayerControlTrigger trigger)
    {
        InitiatorNpc = initiator;
        InteractedObject = interactedObject;
        TriggerComponent = trigger;
        // ActivatedHandler = handler; // If adding handler reference
    }
}

/// <summary>
/// Abstract MonoBehaviour base class for defining and handling custom, multi-state action sequences
/// initiated by the player via a PlayerControlTrigger.
/// Lives directly in the scene, allowing direct references to scene objects.
/// Implements IActionDefinition to provide status checks for UI and player feedback.
/// </summary>
public abstract class AbstractCustomPlayerAction : MonoBehaviour, IActionDefinition
{
    [Header("Base Action Info")]
    [Tooltip("The user-facing name displayed in UI menus.")]
    [SerializeField] private string uiTitle;
    [Tooltip("The user-facing description displayed in UI tooltips.")]
    [SerializeField] [TextArea] private string uiDescription;

    // --- IActionDefinition Implementation ---
    public string DisplayName => uiTitle;
    public string Description => uiDescription;

    /// <summary>
    /// Checks the possibility and visibility of this specific custom action instance.
    /// Subclasses MUST override this to implement the logic using their direct scene references
    /// and potentially aggregating checks from standard InteractionDefinitionSOs if needed.
    /// </summary>
    /// <param name="initiator">The GameObject of the NPC attempting the action.</param>
    /// <param name="targetInteractable">The primary Interactable component associated with the PlayerControlTrigger initiating this custom action (often this component's own Interactable, but passed for clarity).</param>
    /// <returns>An InteractionStatus object indicating ability, visibility, reasons, etc.</returns>
    public abstract InteractionStatus GetStatus(GameObject initiator, Interactable targetInteractable);

    /// <summary>
    /// Generates the sequence of states (StepDefinition) required to execute this custom action.
    /// This is called by the player control system when the action is chosen for execution.
    /// Subclasses implement this using their direct scene references.
    /// </summary>
    /// <param name="context">Contextual information needed to generate the steps (e.g., initiator, trigger, interaction point).</param>
    /// <returns>A StateGraph representing the sequence of states.</returns>
    public abstract AbstractGraphFactory GenerateGraph(PlayerActionContext context);


    // --- Optional Helper for Aggregating Status (if needed) ---

    /// <summary>
    /// Optional helper method for subclasses to aggregate multiple InteractionStatus results
    /// if their custom action involves checking several standard InteractionDefinitionSOs against their scene targets.
    /// </summary>
    /// <param name="individualStatuses">An enumerable collection of InteractionStatus objects resulting from individual checks.</param>
    /// <returns>The aggregated InteractionStatus object for the entire sequence.</returns>
    protected InteractionStatus AggregateStatusFromResults(IEnumerable<InteractionStatus> individualStatuses)
    {
        // Initialize the final status assuming success/visibility unless proven otherwise
        InteractionStatus aggregatedStatus = new InteractionStatus
        {
            IsVisible = true,
            IsSuspicious = false
            // FailureReasons initialized as empty HashSet by constructor
        };

        if (individualStatuses == null || !individualStatuses.Any())
        {
            Debug.LogWarning($"CustomPlayerActionHandler on {gameObject.name} called AggregateStatusFromResults with no status results. Returning default status.", this);
            return aggregatedStatus;
        }

        // Aggregate status from each underlying check result
        foreach (var currentCheckStatus in individualStatuses)
        {
            if (currentCheckStatus == null) continue;
            
            // Add all failure reasons to the aggregated status
            aggregatedStatus.FailureReasons.UnionWith(currentCheckStatus.FailureReasons);
            // This implicitly sets CanInteract
            
            aggregatedStatus.IsVisible &= currentCheckStatus.IsVisible;
            aggregatedStatus.IsSuspicious |= currentCheckStatus.IsSuspicious;
            if (currentCheckStatus.FailureReasons != null)
            {
                aggregatedStatus.FailureReasons.UnionWith(currentCheckStatus.FailureReasons);
            }
        }
        return aggregatedStatus;
    }
}