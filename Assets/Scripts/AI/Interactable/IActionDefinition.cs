// Update Type: Full File
// File: Assets/Scripts/AI/Interactable/IActionDefinition.cs

using UnityEngine; // Required for GameObject

/// <summary>
/// Interface for any action definition (standard or custom) that can be triggered by the player or NPC.
/// Provides display information and a method to check the action's current status for a given initiator and target.
/// </summary>
public interface IActionDefinition
{
    /// <summary>
    /// The user-facing name of the action (e.g., for menus).
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// A description of the action (e.g., for tooltips).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Checks the fundamental possibility and visibility of this action for a given initiator and target Interactable.
    /// Does NOT check the InteractionInstance's IsEnabled flag (that's handled by Interactable.TryInitiateInteraction).
    /// </summary>
    /// <param name="initiator">The GameObject of the NPC attempting the action.</param>
    /// <param name="targetInteractable">The Interactable component being targeted.</param>
    /// <returns>An InteractionStatus object containing detailed status flags and reasons.</returns>
    InteractionStatus GetStatus(GameObject initiator, Interactable targetInteractable);
}