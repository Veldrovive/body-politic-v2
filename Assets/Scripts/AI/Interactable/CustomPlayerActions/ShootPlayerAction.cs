using System;
using UnityEngine;

public class ShootPlayerAction : AbstractCustomPlayerAction
{
    [SerializeField] private InteractionDefinitionSO shootDefinition;
    [SerializeField] private NpcRoleSO requiredRoleForShootInteraction;
    [SerializeField] private float proximityMargin = 2f;
    [SerializeField] private float chaseDuration = 20f;
    [SerializeField] private float chaseDurationWithoutLoS = 10f;

    private void Awake()
    {
        if (shootDefinition == null)
        {
            Debug.LogError("ShootPlayerAction requires a valid InteractionDefinitionSO for shooting.", this);
            return;
        }

        if (string.IsNullOrEmpty(uiTitle))
        {
            uiTitle = shootDefinition.DisplayName;
        }

        if (string.IsNullOrEmpty(uiDescription))
        {
            uiDescription = shootDefinition.Description;
        }
    }

    public override InteractionStatus GetStatus(GameObject initiator, Interactable targetInteractable)
    {
        return shootDefinition.GetStatus(initiator, targetInteractable);
    }

    public override AbstractGraphFactory GenerateGraph(PlayerActionContext context)
    {
        ShootGraphFactory factory = new(new ShootGraphConfiguration()
        {
            RequiredRoleForShootInteraction = requiredRoleForShootInteraction,
            ShootInteractionDefinition = shootDefinition,
            DistanceMargin = proximityMargin,
            MaxChaseDuration = chaseDuration,
            MaxChaseDurationWithoutLoS = chaseDurationWithoutLoS,
            TargetInteractable = context.InteractedObject as InteractableNpc
        });
        return factory;
    }
}