using UnityEngine;

/// <summary>
/// Defines the shared properties for a specific type of holdable item.
/// </summary>
[CreateAssetMenu(fileName = "ConsumableDefinitionSO", menuName = "Body Politic/Items/Consumable Definition")]
public class ConsumableDefinitionSO : HoldableDefinitionSO
{
    [Tooltip("The role conferred to the NPC once the item has been consumed.")]
    public NpcRoleSO ConsumedRole;
}