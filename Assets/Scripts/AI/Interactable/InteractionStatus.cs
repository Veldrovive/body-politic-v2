using System;
using System.Collections.Generic;
using System.Linq;

// TODO: Should be more self-contained.
// Should compute CanInteract and IsVisible based on values stored inside the InteractionStatus.
// Maybe CanInteract is true if there are no failure reasons and IsVisible is true if it is not failed or if a
// generally visible flag is set.
// Then the AddFailureReason definition is changed to (InteractionFailureReason reason, float priority, string humanReadableReason)
// and we store those together.
// When we call GetHumanReadableFailure it looks for the highest priority one and returns the string.

/// <summary>
/// Specific reasons why an interaction might not be possible at the current moment.
/// </summary>
public enum InteractionFailureReason
{
    /// <summary>The initiator lacks the required roles/permissions defined in InteractionDefinitionSO.</summary>
    RoleFailed,
    /// <summary>The initiator is too far away from the target Interactable.</summary>
    ProximityFailed,
    /// <summary>The specific InteractionInstance on the Interactable is currently disabled (e.g., broken panel).</summary>
    InteractionDisabled,
    /// <summary>Used if the interaction is already in the NPCs action queue and duplicates are disallowed.</summary>
    AlreadyExecuting,
    /// <summary>The target is a holdable and needed to be outside the inventory. </summary>
    MustBeInWorld,
    /// <summary>The target is a holdable and needed to be in the hand. </summary>
    MustBeInHand,
    /// <summary>The target is a holdable and needed to be in the inventory. </summary>
    MustBeInInventory,
    /// <summary>There was an internal error or exception during the interaction check.</summary>
    InternalError,
    /// <summary>Used for InteractableNPCs. Indicates that the InteractableNpc refused to interrupt into the animation state.</summary>
    NpcInterruptFailed
}

[Serializable]
public class HumanReadableFailureReason
{
    public string HumanReadableReason;
    public InteractionFailureReason Reason;
    public float Priority;  // Defines which failure reason will be shown to the user
    
    public HumanReadableFailureReason(InteractionFailureReason reason, float priority, string humanReadableReason)
    {
        Reason = reason;
        Priority = priority;
        HumanReadableReason = humanReadableReason;
    }
}

/// <summary>
/// Holds detailed status information about the possibility and visibility of an interaction
/// for a specific initiator and target. Replaces the previous enum tuple system.
/// </summary>
[Serializable] // Serializable for potential debugging or Inspector display if needed
public class InteractionStatus
{
    /// <summary>True if the interaction should be visible to the initiator (e.g., in UI), even if CanInteract is false.</summary>
    public bool IsVisible { get; set; }
    
    /// <summary>True if performing the interaction is considered inherently suspicious according to the definition.</summary>
    public bool IsSuspicious { get; set; }

    /// <summary>A set of reasons why CanInteract might be false.</summary>
    public HashSet<HumanReadableFailureReason> FailureReasons { get; private set; } = new HashSet<HumanReadableFailureReason>();
    
    /// <summary>
    /// For checking if a specific InteractionFailureReason is present in the failure reasons.
    /// </summary>
    public bool HasFailureReason(InteractionFailureReason reason)
    {
        return FailureReasons != null && FailureReasons.Any(x => x.Reason == reason);
    }
    
    /// <summary>
    /// Finds the highest priority failure reason and returns that string
    /// </summary>
    public string GetHumanReadableFailureReasonString()
    {
        if (FailureReasons == null || FailureReasons.Count == 0)
            return "";

        var highestPriority = FailureReasons.OrderByDescending(x => x.Priority).FirstOrDefault();
        return highestPriority?.HumanReadableReason ?? "";
    }

    public string HumanReadableFailureReason => GetHumanReadableFailureReasonString();
    
    /// <summary>True if the interaction can be successfully initiated right now. CanInteract is false if there is any failure reason besides ProximityFailed. That was is handled gracefully.</summary>
    // public bool CanInteract => !FailureReasons.Any(x => x.Reason != InteractionFailureReason.ProximityFailed);

    public bool CanInteract(bool ignoreProximity = false)
    {
        if (ignoreProximity)
        {
            return !FailureReasons.Any(x => x.Reason != InteractionFailureReason.ProximityFailed);
        }
        else
        {
            return !FailureReasons.Any();
        }
    }
    
    
    // Optional context (can be useful for debugging or complex UI)
    // public InteractionDefinitionSO InteractionDefinition { get; private set; }
    // public Interactable TargetInteractable { get; private set; }

    /// <summary>
    /// Default constructor, initializes to a 'failed and hidden' state.
    /// </summary>
    public InteractionStatus()
    {
        IsSuspicious = false; // Default to not suspicious unless determined otherwise
    }

    /// <summary>
    /// Adds a failure reason to the set.
    /// </summary>
    public void AddFailureReason(HumanReadableFailureReason reason)
    {
        FailureReasons.Add(reason);
    }

    override public string ToString()
    {
        return $"CanInteraction: {CanInteract()} ({CanInteract(true)} - IsVisible: {IsVisible} - IsSuspicious: {IsSuspicious}";
    }

    // Potential helper methods could be added here if needed, e.g.,
    // public bool HasFailureReason(InteractionFailureReason reason) => FailureReasons != null && FailureReasons.Contains(reason);
}