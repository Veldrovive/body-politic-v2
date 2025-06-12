using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "InteractionMenuVisualDefinition", menuName = "Body Politic/Interaction Menu Visual Definition")]
public class InteractionMenuVisualDefinitionSO : ScriptableObject
{
    [Header("Base Interactable")]
    [SerializeField] private VisualTreeAsset baseMenuAsset;
    [SerializeField] private VisualTreeAsset baseActionButtonTemplate;
    [SerializeField] private Sprite suspiciousSprite;
    public Sprite SuspiciousSprite => suspiciousSprite;
    
    [Header("Holdable Interactable")]
    [SerializeField] private VisualTreeAsset holdableMenuAsset;
    [SerializeField] private VisualTreeAsset holdableActionButtonTemplate;
    
    [Header("Consumable Interactable")]
    [SerializeField] private VisualTreeAsset consumableMenuAsset;
    [SerializeField] private VisualTreeAsset consumableActionButtonTemplate;
    [SerializeField] private Sprite infectionSprite;
    public Sprite InfectionSprite => infectionSprite;
    
    [Header("Npc Interactable")]
    [SerializeField] private VisualTreeAsset npcMenuAsset;
    [SerializeField] private VisualTreeAsset npcActionButtonTemplate;

    public VisualTreeAsset GetMenuAssetForInteractable(Interactable interactable)
    {
        if (interactable.GetType() == typeof(Consumable))
        {
            return consumableMenuAsset;
        }
        else if (interactable.GetType() == typeof(Holdable))
        {
            return holdableMenuAsset;
        }
        else if (interactable.GetType() == typeof(InteractableNpc))
        {
            return npcMenuAsset;
        }
        else if (interactable.GetType() == typeof(Interactable))
        {
            return baseMenuAsset;
        }
        else
        {
            Debug.LogError($"No menu asset defined for interactable type: {interactable.GetType()}");
            return null;
        }
    }

    public VisualTreeAsset GetActionButtonTemplateForInteractable(Interactable interactable)
    {
        if (interactable.GetType() == typeof(Consumable))
        {
            return consumableActionButtonTemplate;
        }
        else if (interactable.GetType() == typeof(Holdable))
        {
            return holdableActionButtonTemplate;
        }
        else if (interactable.GetType() == typeof(InteractableNpc))
        {
            return npcActionButtonTemplate;
        }
        else if (interactable.GetType() == typeof(Interactable))
        {
            return baseActionButtonTemplate;
        }
        else
        {
            Debug.LogError($"No action button template defined for interactable type: {interactable.GetType()}");
            return null;
        }
    }
}