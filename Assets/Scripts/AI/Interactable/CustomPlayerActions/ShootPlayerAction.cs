using System;
using UnityEngine;

public class ShootPlayerAction : AbstractCustomPlayerAction
{
    [SerializeField] private InteractionDefinitionSO shootDefinition;

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
        MoveAndUseGraphFactory graphFactory = new MoveAndUseGraphFactory(new MoveAndUseGraphConfiguration()
        {
            MoveToTargetTransform = context.InitiatorNpc.transform,
            RequireExactPosition = true,
            RequireFinalAlignment = true,
            TargetInteractable = context.InteractedObject,
            TargetInteractionDefinition = shootDefinition,
        });
        return graphFactory;
    }
}