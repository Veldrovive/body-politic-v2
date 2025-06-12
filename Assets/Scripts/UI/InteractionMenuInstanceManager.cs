using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class InteractionMenuFloatingUIConfig : AbstractFloatingUIConfig
{
    public InteractionMenuVisualDefinitionSO visualDefinition;
    public InteractableDefinitionSO TargetInteractableDefinition;
    public Interactable TargetInteractable;
    public List<PlayerControlTrigger> Triggers;
    public InteractionMenuInstanceManager InstanceManager;
    
    public InteractionMenuFloatingUIConfig(VisualTreeAsset template, UnityEngine.Object lifetimeOwner) 
        : base(template, lifetimeOwner) { }
}

public class InteractionMenuInstanceManager
{
    private InteractionMenuVisualDefinitionSO visualDefinition;
    private VisualElement floaterRoot;
    private Interactable targetInteractable;
    private InteractableDefinitionSO interactableDefinition;
    private List<PlayerControlTrigger> triggers;
    
    private Label titleLabel;
    private Label descriptionLabel;
    private VisualElement actionsContainer;
    private VisualTreeAsset actionButtonTemplate;  // Template for the interaction buttons.
    private Dictionary<string, object> kwargs;  // Additional parameters for the interaction menu, if needed.
    
    private List<InteractionButtonData> currentButtonsData = new List<InteractionButtonData>();
    private List<InteractionButtonData> newButtonsData = new List<InteractionButtonData>();  // Avoid extra GC allocations by reusing the same list.
    
    public InteractionMenuInstanceManager(
        VisualElement floaterRoot,
        InteractionMenuVisualDefinitionSO visualDefinition,
        Interactable targetInteractable,
        InteractableDefinitionSO interactableDefinition,
        List<PlayerControlTrigger> triggers,
        Dictionary<string, object> kwargs = null
    )
    {
        this.visualDefinition = visualDefinition;
        this.floaterRoot = floaterRoot;
        this.interactableDefinition = interactableDefinition;
        this.triggers = triggers;
        this.actionButtonTemplate = visualDefinition.GetActionButtonTemplateForInteractable(targetInteractable);
        this.targetInteractable = targetInteractable;
        
        // Set the event so that when the floater is destroyed, we can clean up.
        floaterRoot.RegisterCallback<DetachFromPanelEvent>(evt => OnRootDestroyed());
        
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

    private void SetCursorMode(bool selecting)
    {
        if (selecting)
        {
            CursorManager.Instance.SetCursor(CursorType.Selection);
        }
        else
        {
            CursorManager.Instance.SetCursor(CursorType.Default);
        }
    }

    private void OnActionButtonHover(MouseEnterEvent evt)
    {
        SetCursorMode(true);
    }
    
    private void OnActionButtonLeave(MouseLeaveEvent evt)
    {
        SetCursorMode(false);
    }

    private void OnRootDestroyed()
    {
        SetCursorMode(false);
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
        if (isInfected)
        {
            Sprite infectionSprite = visualDefinition.InfectionSprite;
            Image infectionIcon = floaterRoot.Q<Image>("InfectionIcon");
            if (infectionIcon != null && infectionSprite != null)
            {
                infectionIcon.sprite = infectionSprite;
                infectionIcon.style.display = DisplayStyle.Flex; // Make it visible.
            }
            else
            {
                Debug.LogWarning("Infection icon or sprite is missing in the UI.");
            }
        }
    }
    
    private struct InteractionButtonData
    {
        public bool IsVisible;  // If false, the button should not be shown in the UI.
        public bool IsSuspicious;  // If true, we add the suspicion icon to the button.
        public bool CanInteract;  // If false and visible, then the interaction is disabled in the UI.
        public string InteractionTitle;

        public PlayerControlTrigger Trigger;
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
    /// <param name="onActionClicked"></param>
    public void UpdateActions(Action onActionClicked = null)
    {
        Sprite isSuspiciousIcon = visualDefinition.SuspiciousSprite;
        newButtonsData.Clear();
        foreach (var trigger in triggers)
        {
            Interactable interactable = trigger.TargetInteractable;
            GameObject initiator = PlayerManager.Instance.CurrentFocusedNpc.gameObject;
            
            InteractionStatus interactionStatus = trigger.GetActionStatus(initiator);
            InteractionButtonData buttonData = new InteractionButtonData()
            {
                IsVisible = interactionStatus.IsVisible,
                IsSuspicious = interactionStatus.IsSuspicious,
                CanInteract = interactionStatus.CanInteract(ignoreProximity: true),
                InteractionTitle = trigger.Title,
                
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
            actionsContainer.RegisterCallback<MouseEnterEvent>(OnActionButtonHover);
            actionsContainer.RegisterCallback<MouseLeaveEvent>(OnActionButtonLeave);
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

    public static InteractionMenuFloatingUIConfig GenerateFloatingUIConfig(
        // GameObject controlTriggerGO,
        Interactable interactable,
        InteractionMenuVisualDefinitionSO visualDefinition,
        UnityEngine.Object lifetimeOwner = null,
        FloatingUIPositionType positionType = FloatingUIPositionType.Transform,
        object targetObject = null,  // Can a screen position, world position, or transform
        FloatingUIAnchor anchor = FloatingUIAnchor.BottomCenter,
        float verticalOffset = 0f,
        float maxWidth = 15f,
        float minWidth = 10f,
        bool keepOnScreen = true
    )
    {
        if (lifetimeOwner == null)
        {
            lifetimeOwner = interactable;
        }
        
        if (interactable == null)
        {
            Debug.LogError($"WSCMM: Cannot open menu for '{interactable.name}' - no Interactable found on the trigger parent.", interactable);
            return null; // Cannot open a menu without an interactable
        }
        PlayerControlTriggerVisualDefinition visualDef = interactable.GetComponentInChildren<PlayerControlTriggerVisualDefinition>();
        GameObject controlTriggerGO = visualDef?.gameObject;
        
        List<PlayerControlTrigger> triggers = controlTriggerGO?.GetComponents<PlayerControlTrigger>().ToList();
        
        InteractableDefinitionSO interactableDefinition = interactable.InteractableDefinition;
        if (interactableDefinition == null)
        {
            Debug.LogError($"WSCMM: Cannot open menu for '{interactable.name}' - no InteractableDefinition found on the Interactable.", interactable);
            return null; // Cannot open a menu without an interactable definition
        }
        
        VisualTreeAsset menuTemplate = visualDefinition.GetMenuAssetForInteractable(interactable);
        InteractionMenuFloatingUIConfig config = new InteractionMenuFloatingUIConfig(menuTemplate, lifetimeOwner)
        {
            Anchor = anchor,
            ScreenSpaceOffset = Vector2.up * verticalOffset, // Use the vertical offset to position the menu above the icon
            
            ContainerMaxWidthPercent = maxWidth,
            ContainerMinWidthPercent = minWidth,
            KeepOnScreen = keepOnScreen,
            
            visualDefinition = visualDefinition,
            TargetInteractable = interactable,
            TargetInteractableDefinition = interactableDefinition,
            Triggers = triggers
        };

        if (positionType == FloatingUIPositionType.Transform)
        {
            if (targetObject is not Transform targetTransform)
            {
                Debug.LogError("Target object for Transform position type must be a Transform.");
                return null;
            }
            config.PositionType = positionType;
            config.TargetTransform = targetTransform;
        }
        else if (positionType == FloatingUIPositionType.WorldPosition)
        {
            if (targetObject is not Vector3 targetPosition)
            {                
                Debug.LogError("Target object for WorldPosition position type must be a Vector3.");
                return null;
            }
            config.PositionType = positionType;
            config.TargetWorldPosition = targetPosition;
        }
        else if (positionType == FloatingUIPositionType.ScreenPosition)
        {
            if (targetObject is not Vector2 targetScreenPosition)
            {
                Debug.LogError("Target object for ScreenPosition position type must be a Vector2.");
                return null;
            }
            config.PositionType = positionType;
            config.TargetScreenPosition = targetScreenPosition;
        }
        else if (positionType == FloatingUIPositionType.CursorPosition)
        {
            config.PositionType = positionType;
            // Cursor position will be handled by the FloatingUIManager, no need to set a target.
        }
        else
        {
            Debug.LogError($"Unsupported position type: {positionType}");
            return null;
        }
        
        return config;
    }
}