using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Required for role checks

public enum InteractionAvailableFrom
{
    World,
    Hand,
    Inventory,
}

// TODO: Make it so that IsVisible is dependent on if the item is in th correct "Available From" location. Also
// convert that into a list so it can be available from multiple locations

/// <summary>
/// Defines the properties, requirements, and outcomes of a specific type of interaction.
/// Implements IActionDefinition to check status based on initiator and target.
/// </summary>
[CreateAssetMenu(fileName = "InteractionDefinitionSO", menuName = "Body Politic/Interaction Definition SO")]
public class InteractionDefinitionSO : ScriptableObject, IActionDefinition
{
    [Header("Identification & UI")] [Tooltip("The user-facing name of the action (e.g., for menus).")] [SerializeField]
    private string uiTitle;

    [Tooltip("A description of the action (e.g., for tooltips).")] [SerializeField] [TextArea]
    private string uiDescription;

    // Interface Properties
    public string DisplayName => uiTitle;
    public string Description => uiDescription;

    [Header("Access Roles")] [Tooltip("Roles that can perform this action without raising suspicion.")] [SerializeField]
    private List<NpcRoleSO> rolesCanExecuteNoSuspicion = new List<NpcRoleSO>();
    public IEnumerable<NpcRoleSO> RolesCanExecuteNoSuspicion => rolesCanExecuteNoSuspicion;

    [Tooltip("Roles that can perform this action, but doing so is inherently suspicious.")] [SerializeField]
    private List<NpcRoleSO> rolesCanExecuteWithSuspicion = new List<NpcRoleSO>();
    public IEnumerable<NpcRoleSO> RolesCanExecuteWithSuspicion => rolesCanExecuteWithSuspicion;

    [Tooltip("Roles that can see this action is available (even if they can't perform it).")] [SerializeField]
    private List<NpcRoleSO> rolesCanView = new List<NpcRoleSO>();
    public IEnumerable<NpcRoleSO> RolesCanView => rolesCanView;

    [Tooltip(
        "If true, the action appears 'greyed out' for those who can't perform it but can see it. If false, it's hidden unless the viewer has a specific 'CanView' role.")]
    [SerializeField]
    private bool generallyVisible = true;

    [Header("Execution Requirements & Effects")]
    [Tooltip("Maximum distance the initiator can be from the target to start the interaction.")]
    [SerializeField]
    private float requiredProximity = 5f;

    // [Tooltip("Whether the action should only be visible in the inventory menu.")]
    // [SerializeField] private bool onlyAvailableInInventory = false;
    // public bool OnlyAvailableInInventory => onlyAvailableInInventory;
    [Tooltip("Where the interaction can be used from.")] [SerializeField]
    private List<InteractionAvailableFrom> interactionAvailableFroms = new List<InteractionAvailableFrom>{ InteractionAvailableFrom.World };

    public List<InteractionAvailableFrom> InteractionAvailableFroms => interactionAvailableFroms;
    [Tooltip("Time in seconds the interaction takes to complete.")]
    [SerializeField] private float interactionDuration = 1.0f;
    [Tooltip("Animation trigger to play on the initiator when the interaction starts.")]
    [SerializeField] private string initiatorAnimationTrigger;
    [Tooltip("Animation trigger to play on the target object when the interaction starts.")]
    [SerializeField] private string targetAnimationTrigger;
    [Tooltip("The level of suspicion generated if this interaction is witnessed.")]
    [SerializeField] private int witnessSuspicionLevel = 0;
    
    [Header("Human Readable Failure Reasons")]
    [Tooltip("List of human-readable failure reasons for this action.")]
    [SerializeField] private List<HumanReadableFailureReason> humanReadableFailureReasons = new List<HumanReadableFailureReason>();

    [Header("Debugging")]
    [Tooltip("Optional prompt text for debugging purposes.")]
    [SerializeField] private string debugPrompt;
    
    private static Dictionary<InteractionFailureReason, HumanReadableFailureReason> defaultFailureMessages = new Dictionary<InteractionFailureReason, HumanReadableFailureReason>
    {
        { InteractionFailureReason.RoleFailed, new HumanReadableFailureReason(InteractionFailureReason.RoleFailed,3, "It looks like this thrall doesn't have the ability to do that.") },
        { InteractionFailureReason.ProximityFailed, new HumanReadableFailureReason(InteractionFailureReason.ProximityFailed,0, "I'm too far away to do that.") },
        { InteractionFailureReason.InteractionDisabled, new HumanReadableFailureReason(InteractionFailureReason.InteractionDisabled, 1, "It looks like I can't do that right now.") },
        { InteractionFailureReason.AlreadyExecuting, new HumanReadableFailureReason(InteractionFailureReason.AlreadyExecuting, 5, "I don't need to do that twice.") },
        { InteractionFailureReason.MustBeInWorld, new HumanReadableFailureReason(InteractionFailureReason.MustBeInWorld, 2, "I can't use this unless it is on the ground.") },
        { InteractionFailureReason.MustBeInHand, new HumanReadableFailureReason(InteractionFailureReason.MustBeInHand, 2, "I can't use this unless it is in my hand.") },
        { InteractionFailureReason.MustBeInInventory, new HumanReadableFailureReason(InteractionFailureReason.MustBeInInventory, 2, "I can't use this unless it is in my inventory.") },
        { InteractionFailureReason.InternalError, new HumanReadableFailureReason(InteractionFailureReason.InternalError, 4, "INTERNAL ERROR. PLEASE TELL AIDAN HOW TO REPRODUCE.") }
    };


    /// <summary>
    /// Checks the fundamental possibility and visibility of this action for a given initiator and target Interactable,
    /// incorporating role requirements, proximity, and the enabled state of the instance on the target.
    /// </summary>
    /// <param name="initiator">The GameObject of the NPC attempting the action.</param>
    /// <param name="targetInteractable">The Interactable component being targeted.</param>
    /// <returns>An InteractionStatus object detailing the outcome.</returns>
    public virtual InteractionStatus GetStatus(GameObject initiator, Interactable targetInteractable)
    {
        InteractionStatus status = new InteractionStatus();
        // status.InteractionDefinition = this; // Optional context
        // status.TargetInteractable = targetInteractable; // Optional context
        NpcContext initiatorContext = initiator.GetComponent<NpcContext>();

        HumanReadableFailureReason getHrFailureReason(InteractionFailureReason reason)
        {
            // Gets the human readable failure reason for the generic reason. If the failure reason is in 
            // humanReadableFailureReasons then we use that one. Otherwise, we try to use defaultFailureMessages
            // If it is also not in that, we create log a warning and return a default one.
            var hrFailureReason = humanReadableFailureReasons.FirstOrDefault(h => h.Reason == reason);
            if (hrFailureReason != null)
            {
                return hrFailureReason;
            }
            else if (defaultFailureMessages.TryGetValue(reason, out var defaultReason))
            {
                return defaultReason;
            }
            else
            {
                Debug.LogWarning($"No human-readable failure reason found for {reason}. Using default.", this);
                return new HumanReadableFailureReason(reason, 0, "Unknown reason");
            }
        }

        // --- Basic Validation ---
        if (initiator == null || targetInteractable == null)
        {
            status.IsVisible = false; // Cannot determine visibility or interactability
            status.AddFailureReason(getHrFailureReason(InteractionFailureReason.RoleFailed)); // Treat null inputs as failure
            return status;
        }
        NPCIdentity initiatorIdentity = initiatorContext.Identity;
        if (initiatorIdentity == null)
        {
            // If no identity, cannot check roles. Assume failure if any roles are required.
            bool anyRolesDefined = rolesCanExecuteNoSuspicion.Any() || rolesCanExecuteWithSuspicion.Any() || rolesCanView.Any();
            if (anyRolesDefined) {
                status.IsVisible = false; // Hide if roles matter but identity is missing
                status.AddFailureReason(getHrFailureReason(InteractionFailureReason.RoleFailed));
                return status;
            }
            // If NO roles are defined at all, maybe proceed? Let's assume roles are usually needed.
            // For now, treat missing identity as RoleFailed if roles *could* be relevant.
            status.IsVisible = false; // Safer default
            status.AddFailureReason(getHrFailureReason(InteractionFailureReason.RoleFailed));
            return status;
        }

        NpcInventory initiatorInventory = initiatorContext.Inventory;
        bool itemInInventory = initiatorInventory.HasItemInInventory(targetInteractable);
        bool itemInHand = initiatorInventory.HasItemInHand(targetInteractable);

        bool failedInWorldCheck = (itemInHand || itemInInventory) && interactionAvailableFroms.Contains(InteractionAvailableFrom.World);
        bool failedInInventoryCheck = !itemInInventory && interactionAvailableFroms.Contains(InteractionAvailableFrom.Inventory);
        bool failedInHandCheck = !itemInHand && interactionAvailableFroms.Contains(InteractionAvailableFrom.Hand);

        // --- Check 1: Interaction Instance Enabled State (from Interactable) ---
        // Get the enabled info struct we discussed adding to Interactable
        // InteractionEnableInfo enableInfo = targetInteractable.GetInteractionEnableInfo(this);
        InteractionInstance interactionInstance = targetInteractable.FindInteractionInstance(this);
        bool isInstanceEnabled = interactionInstance.IsEnabled;
        bool isVisibleWhenDisabled = !interactionInstance.DisabledImpliesHidden;

        // --- Check 2: Role Requirements ---
        bool canExecuteNoSuspicion = initiatorIdentity.HasAnyRole(rolesCanExecuteNoSuspicion);
        bool canExecuteWithSuspicion = initiatorIdentity.HasAnyRole(rolesCanExecuteWithSuspicion);
        bool hasExecutionRole = canExecuteNoSuspicion || canExecuteWithSuspicion;
        bool canViewAction = generallyVisible || initiatorIdentity.HasAnyRole(rolesCanView);

        // Determine inherent suspicion based *only* on roles (if allowed)
        status.IsSuspicious = hasExecutionRole && !canExecuteNoSuspicion; // Suspicious if allowed, but only via the 'WithSuspicion' list

        // --- Check 3: Proximity ---
        bool proximityCheckPassed = true; // Assume pass if not required
        if (requiredProximity > 0 && !itemInHand && !itemInInventory)  // If it is in the inventory then it is close enough
        {
            Vector3 initiatorPosition = new Vector3(initiator.transform.position.x, 0, initiator.transform.position.z);
            Vector3 targetPosition = new Vector3(targetInteractable.transform.position.x, 0, targetInteractable.transform.position.z);
            float distanceSqr = (initiatorPosition - targetPosition).sqrMagnitude;
            if (distanceSqr > (requiredProximity * requiredProximity))
            {
                proximityCheckPassed = false;
            }
        }

        // --- Determine Final Status Flags ---

        // 1. Determine Failure Reasons (if any)
        if (!hasExecutionRole)      status.AddFailureReason(getHrFailureReason(InteractionFailureReason.RoleFailed));
        if (!proximityCheckPassed)  status.AddFailureReason(getHrFailureReason(InteractionFailureReason.ProximityFailed));
        if (!isInstanceEnabled)     status.AddFailureReason(getHrFailureReason(InteractionFailureReason.InteractionDisabled));
        if (failedInWorldCheck)     status.AddFailureReason(getHrFailureReason(InteractionFailureReason.MustBeInWorld));
        if (failedInInventoryCheck) status.AddFailureReason(getHrFailureReason(InteractionFailureReason.MustBeInInventory));
        if (failedInHandCheck)      status.AddFailureReason(getHrFailureReason(InteractionFailureReason.MustBeInHand));

        // 3. Determine IsVisible
        if (!hasExecutionRole && !canViewAction) {
            status.IsVisible = false; // Cannot execute roles AND cannot view roles = Hidden
        } else if (!isInstanceEnabled && !isVisibleWhenDisabled) {
            status.IsVisible = false; // Instance disabled AND configured to be hidden when disabled = Hidden
        } else {
            status.IsVisible = true; // Otherwise, it should be visible (either interactable or greyed out)
        }
        
        return status;
    }

    // Public getters for fields that might be needed by states
    public float InteractionDuration => interactionDuration;
    public string InitiatorAnimationTrigger => initiatorAnimationTrigger;
    public string TargetAnimationTrigger => targetAnimationTrigger;
    public int WitnessSuspicionLevel => witnessSuspicionLevel;

    /// <summary>
    /// humanReadableFailureReasons should always contain exactly one of each failure reason.
    /// If it is missing one, we should add it with a default value.
    /// If we have an extra one, we log an error, but don't remove it.
    /// </summary>
    private void ValidateHumanReadableFailureReasons()
    {
        // Get all the failure reasons
        var allFailureReasons = Enum.GetValues(typeof(InteractionFailureReason))
            .Cast<InteractionFailureReason>()
            .ToList();

        // Check for missing failure reasons
        foreach (var reason in allFailureReasons)
        {
            if (!humanReadableFailureReasons.Any(h => h.Reason == reason))
            {
                // Add a default human-readable reason
                if (defaultFailureMessages.TryGetValue(reason, out var defaultReason))
                {
                    humanReadableFailureReasons.Add(defaultReason);
                    Debug.LogWarning($"Added missing human-readable failure reason for {reason}: {defaultReason.HumanReadableReason}");
                }
                else
                {
                    Debug.LogError($"No default human-readable reason found for {reason}. Please add it manually.");
                }
            }
            
            // Check for duplicates
            var duplicates = humanReadableFailureReasons
                .Where(h => h.Reason == reason)
                .ToList();
            if (duplicates.Count > 1)
            {
                Debug.LogError($"Duplicate human-readable failure reason found on {uiTitle} for {reason}: {string.Join(", ", duplicates.Select(d => d.HumanReadableReason))}", this);
            }
        }
    }

    private void Reset()
    {
#if UNITY_EDITOR
        // ValidateHumanReadableFailureReasons();
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        ValidateHumanReadableFailureReasons();
#endif
    }
}