// Which update method you are using: Full File
using System;
using UnityEngine; // Required for Debug.LogWarning
using UnityEngine.UIElements;

/// <summary>
/// Manages the display and interaction logic for a single Player Control Trigger menu element.
/// This is a non-MonoBehaviour class intended to be managed by a dedicated menu manager.
/// </summary>
public class ControlMenuDisplay
{
    // --- Private Fields ---
    // Note: _controlTrigger isn't strictly required after initialization if we store title/desc,
    // but keeping it might be useful for future extensions.
    // private readonly PlayerControlTrigger _controlTrigger;

    private readonly VisualElement _rootElement;       // The instantiated UXML element for this menu
    private readonly Action _clickCallback;          // Action to invoke when the button is clicked

    // Cached child elements for efficiency
    private readonly Button _executeButton;
    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;

    private void DefaultDisabledCallback(InteractionStatus interactionStatus)
    {
        if (ParasiteSpeechUIManager.Instance == null)
        {
            Debug.LogError($"ControlMenuDisplay: No ParasiteSpeechUIManager instance found.");
        }

        ParasiteSpeechUIManager.Instance?.ShowSpeech(
            string.IsNullOrEmpty(interactionStatus.HumanReadableFailureReason)
                ? "I can't do that right now."
                : interactionStatus.HumanReadableFailureReason, 2f, immediate: true
            );
    }

    /// <summary>
    /// Initializes a new instance of the ControlMenuDisplay class.
    /// Sets up the visual element with data from the control trigger and connects the execute callback.
    /// </summary>
    /// <param name="controlTrigger">The PlayerControlTrigger providing the menu's data (title, description).</param>
    /// <param name="menuElement">The root VisualElement instance created from the UXML template.</param>
    /// <param name="executeCallback">The Action to execute when the menu's button is clicked.</param>
    public ControlMenuDisplay(PlayerControlTrigger controlTrigger, VisualElement menuElement, Action executeCallback, Action disabledCallback = null, InteractionStatus interactionStatus = null)
    {
        // --- Store References ---
        if (controlTrigger == null) throw new ArgumentNullException(nameof(controlTrigger));
        _rootElement = menuElement ?? throw new ArgumentNullException(nameof(menuElement));
        
        // Set the button click callback based on the interaction status
        if (interactionStatus != null && !interactionStatus.CanInteract(true))
        {
            // Then this interaction is disabled and we should use the disabld callback instead of execute
            if (disabledCallback != null)
            {
                _clickCallback = disabledCallback;
            }
            else
            {
                // If none was provided, we have a default disabled callback
                _clickCallback = () => DefaultDisabledCallback(interactionStatus);
            }
        }
        else
        {
            // Trigger is enabled so we use the execute callback
            _clickCallback = executeCallback; // Callback can legitimately be null if the button does nothing
        }

        // --- Ensure Absolute Positioning ---
        // This ensures SetPanelPosition works as expected.
        _rootElement.style.position = Position.Absolute;
        // Start hidden; the manager should decide when to show it and position it.
        _rootElement.style.visibility = Visibility.Hidden;

        // --- Query Child Elements ---
        // Cache references to frequently used child elements. Names must match the UXML template.
        _executeButton = _rootElement.Q<Button>("ExecuteButton");
        _titleLabel = _rootElement.Q<Label>("TitleLabel");
        _descriptionLabel = _rootElement.Q<Label>("DescriptionLabel");

        // --- Validate Child Elements (Optional but Recommended) ---
        if (_titleLabel == null)
        {
            Debug.LogWarning("ControlMenuDisplay: Could not find child Label with name 'TitleLabel' in the provided menuElement."); // Provide context if possible
        }
        if (_descriptionLabel == null)
        {
            Debug.LogWarning("ControlMenuDisplay: Could not find child Label with name 'DescriptionLabel' in the provided menuElement.");
        }
        if (_executeButton == null)
        {
            // Only warn if a callback was actually provided, otherwise a missing button might be intentional.
            if (_clickCallback != null)
            {
                 Debug.LogWarning("ControlMenuDisplay: Could not find child Button with name 'ExecuteButton' in the provided menuElement, but an executeCallback was provided.");
            }
        }
        else // Only wire callback if button exists
        {
             // --- Connect Execute Button Callback ---
            if (_clickCallback != null)
            {
                _executeButton.clicked += _clickCallback;
            }
        }

        // --- Set Initial Content ---
        // Use the trigger's properties via the private helper
        SetContent(controlTrigger.Title, controlTrigger.Description);
        if (interactionStatus != null)
        {
            UpdateStatusVisuals(interactionStatus.CanInteract(true), interactionStatus.IsSuspicious);   
        }
    }

    /// <summary>
    /// Sets the panel position (top-left corner) of the menu's root element
    /// relative to its parent container.
    /// Assumes the element's position style is set to Absolute.
    /// </summary>
    /// <param name="position">The desired position (in panel coordinates, typically Y=0 at top).</param>
    public void SetPanelPosition(Vector2 position)
    {
        // It's unlikely _rootElement is null if the constructor succeeded, but check is safe.
        if (_rootElement == null) return;

        _rootElement.style.left = position.x;
        _rootElement.style.top = position.y;
    }

    /// <summary>
    /// Makes the menu visible. Called by the manager after positioning.
    /// </summary>
    public void Show()
    {
         if (_rootElement != null)
         {
            _rootElement.style.visibility = Visibility.Visible;
         }
    }

    /// <summary>
    /// Makes the menu hidden. Can be called by the manager before cleanup
    /// or if needing to temporarily hide without destroying.
    /// </summary>
    public void Hide()
    {
         if (_rootElement != null)
         {
             _rootElement.style.visibility = Visibility.Hidden;
         }
    }

    /// <summary>
    /// Gets the root VisualElement managed by this instance.
    /// Used by the manager to:
    /// 1. Add/Remove the element from the visual tree.
    /// 2. Register external event handlers (e.g., PointerEnter/Leave for locking).
    /// </summary>
    /// <returns>The root VisualElement instance.</returns>
    public VisualElement GetRootElement()
    {
        return _rootElement;
    }

    /// <summary>
    /// Cleans up resources used by this instance, specifically detaching the
    /// button click handler to prevent potential memory leaks or errors.
    /// This should be called by the manager *before* discarding the instance
    /// and *before* removing the element from the hierarchy if events might fire during removal.
    /// </summary>
    public void Cleanup()
    {
        // Unregister the callback to prevent memory leaks if the callback holds references
        // or if the target object might be destroyed before this menu.
        if (_executeButton != null && _clickCallback != null)
        {
            _executeButton.clicked -= _clickCallback;
        }
    }

    /// <summary>
    /// Helper method to set the text content of the title and description labels.
    /// Called internally by the constructor.
    /// </summary>
    /// <param name="title">The text for the title label.</param>
    /// <param name="description">The text for the description label.</param>
    private void SetContent(string title, string description)
    {
        if (_titleLabel != null)
        {
            // Assign text to the label element.
            _titleLabel.text = title;
        }

        if (_descriptionLabel != null)
        {
            // Assign text to the label element.
            _descriptionLabel.text = description;
        }
    }

    private void UpdateStatusVisuals(bool canInteract, bool isSuspicious)
    {
        if (_executeButton == null)
        {
            return;
        }

        if (isSuspicious)
        {
            _executeButton.text = "Execute (Sus)";
        }

        if (!canInteract)
        {
            _executeButton.text = "Disabled";
            _executeButton.style.opacity = 0.5f;
            _executeButton.style.color = Color.red;
        }
    }
}