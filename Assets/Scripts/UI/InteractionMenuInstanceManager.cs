using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class InteractionMenuInstanceManager
{
    private VisualElement floaterRoot;
    private Interactable targetInteractable;
    private InteractableDefinitionSO interactableDefinition;
    private List<PlayerControlTrigger> triggers;
    
    private Label titleLabel;
    private Label descriptionLabel;
    private VisualElement actionsContainer;
    private VisualTreeAsset actionButtonTemplate;  // Template for the interaction buttons.
    
    private List<InteractionButtonData> currentButtonsData = new List<InteractionButtonData>();
    private List<InteractionButtonData> newButtonsData = new List<InteractionButtonData>();  // Avoid extra GC allocations by reusing the same list.
    
    public InteractionMenuInstanceManager(
        VisualElement floaterRoot,
        Interactable targetInteractable,
        InteractableDefinitionSO interactableDefinition,
        List<PlayerControlTrigger> triggers,
        VisualTreeAsset actionButtonTemplate
    )
    {
        this.floaterRoot = floaterRoot;
        this.interactableDefinition = interactableDefinition;
        this.triggers = triggers;
        this.actionButtonTemplate = actionButtonTemplate;
        
        titleLabel = floaterRoot.Q<Label>("TitleLabel");
        descriptionLabel = floaterRoot.Q<Label>("DescriptionLabel");
        actionsContainer = floaterRoot.Q<VisualElement>("ActionsContainer");
        
        switch (targetInteractable.GetType())
        {
            case Type t when t == typeof(Interactable):
                InitializeBasicInteractable();
                break;
            case Type t when t == typeof(InteractableNpc):
                InitializeNpcInteractable(targetInteractable as InteractableNpc);
                break;
            case Type t when t == typeof(Holdable):
                InitializeHoldableInteractable(targetInteractable as Holdable);
                break;
            case Type t when t == typeof(Consumable):
                InitializeConsumableInteractable(targetInteractable as Consumable);
                break;
            default:
                Debug.LogError($"{targetInteractable.GetType()} is not a valid interactable");
                break;
        }
    }

    private void InitializeBasicInteractable()
    {
        titleLabel.text = interactableDefinition.Name;
        descriptionLabel.text = interactableDefinition.Description;
    }

    private void InitializeNpcInteractable(InteractableNpc interactableNpc)
    {
        NpcContext npcContext = interactableNpc.gameObject.GetComponent<NpcContext>();
        if (npcContext == null)
        {
            titleLabel.text = "Unknown NPC";
            descriptionLabel.text = "NPC is missing NpcContext.";
            return;
        }
        
        titleLabel.text = npcContext.Identity.PrimaryRole.RoleName;
        descriptionLabel.text = npcContext.Identity.PrimaryRole.RoleDescription;
    }

    private void InitializeHoldableInteractable(Holdable holdable)
    {
        InitializeBasicInteractable();
    }

    private void InitializeConsumableInteractable(Consumable consumable)
    {
        InitializeBasicInteractable();
        // We also need to get whether this consumable is infected or not.
        bool isInfected = consumable.Infected;
    }
    
    private struct InteractionButtonData
    {
        public bool IsVisible;  // If false, the button should not be shown in the UI.
        public bool IsSuspicious;  // If true, we add the suspicion icon to the button.
        public bool CanInteract;  // If false and visible, then the interaction is disabled in the UI.
        public string InteractionTitle;

        public PlayerControlTrigger Trigger;
        public InteractionDefinitionSO interactionDefinition;
        public Interactable interactable;
        public GameObject initiator;
        
        public Action OnClickAction;  // Action to perform when the button is clicked.

        public override bool Equals(object obj)
        {
            if (obj is InteractionButtonData other)
            {
                return IsVisible == other.IsVisible &&
                       IsSuspicious == other.IsSuspicious &&
                       CanInteract == other.CanInteract &&
                       InteractionTitle == other.InteractionTitle;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            unchecked // Allow overflow for better hash distribution
            {
                int hash = 17;
                hash = hash * 31 + IsVisible.GetHashCode();
                hash = hash * 31 + IsSuspicious.GetHashCode();
                hash = hash * 31 + CanInteract.GetHashCode();
                hash = hash * 31 + (InteractionTitle?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
    
    /// <summary>
    /// This method is called to update the interaction buttons in the UI. If nothing has changed since last call,
    /// then the UI is not updated. If there has been a change, it clears the entire container and rebuilds it.
    /// Also manages the callbacks for each interaction button, including hover states and click actions.
    /// </summary>
    /// <param name="actionsContainer"></param>
    /// <param name="triggers"></param>
    public void UpdateActions(Sprite isSuspiciousIcon, Action onActionClicked = null)
    {
        newButtonsData.Clear();
        foreach (var trigger in triggers)
        {
            InteractionDefinitionSO interactionDef = trigger.TargetInteractionDefinition;
            Interactable interactable = trigger.TargetInteractable;
            GameObject initiator = PlayerManager.Instance.CurrentFocusedNpc.gameObject;
            
            InteractionStatus interactionStatus = interactionDef.GetStatus(initiator, interactable);
            InteractionButtonData buttonData = new InteractionButtonData()
            {
                IsVisible = interactionStatus.IsVisible,
                IsSuspicious = interactionStatus.IsSuspicious,
                CanInteract = interactionStatus.CanInteract(ignoreProximity: true),
                InteractionTitle = interactionDef.DisplayName,
                
                interactionDefinition = interactionDef,
                interactable = interactable,
                initiator = initiator,
                Trigger = trigger,
            };
            // If the interaction is not visible, we do not add it to the list.
            if (buttonData.IsVisible)
            {
                newButtonsData.Add(buttonData);
            }
        }
        
        // Check if anything has changed between currentButtonsData and newButtonsData.
        if (currentButtonsData.Count != newButtonsData.Count || !currentButtonsData.SequenceEqual(newButtonsData))
        {
            Debug.Log("Updating interaction buttons in the UI.");
            
            actionsContainer.Clear();
            foreach (var buttonData in newButtonsData)
            {
                // Instantiate the button from the UXML template.
                VisualElement buttonElement = actionButtonTemplate.Instantiate();

                // Query for the child elements within the instantiated template.
                Label label = buttonElement.Q<Label>("InteractionLabel");
                Image suspicionIcon = buttonElement.Q<Image>("SuspicionIcon");

                // Set the button's display text.
                label.text = buttonData.InteractionTitle;

                // Show or hide the suspicion icon based on the interaction status.
                if (buttonData.IsSuspicious && isSuspiciousIcon != null)
                {
                    suspicionIcon.sprite = isSuspiciousIcon;
                    suspicionIcon.style.display = DisplayStyle.Flex; // Make it visible.
                }
                else
                {
                    suspicionIcon.style.display = DisplayStyle.None; // Hide it.
                }

                // Set the button's enabled/disabled state for interactivity.
                // UI Toolkit will apply the :disabled pseudo-class styling automatically.
                buttonElement.SetEnabled(buttonData.CanInteract);

                // Register a callback for when the button is clicked.
                buttonElement.RegisterCallback<ClickEvent>(evt =>
                {
                    // It's good practice to re-check if the interaction is still possible.
                    if (buttonData.CanInteract)
                    {
                        // Execute the interaction logic.
                        PlayerManager.Instance?.HandleTriggerInteraction(buttonData.Trigger);
            
                        // Close the menu after a successful interaction.
                        onActionClicked?.Invoke();
                    }
                });

                // Add the fully configured button to the actions container.
                actionsContainer.Add(buttonElement);
            }
            
            currentButtonsData.Clear();
            currentButtonsData.AddRange(newButtonsData);
        }
    }
}