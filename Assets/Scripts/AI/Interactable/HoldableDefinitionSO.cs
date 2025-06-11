using UnityEngine;

/// <summary>
/// Defines the shared properties for a specific type of holdable item.
/// </summary>
[CreateAssetMenu(fileName = "HoldableDefinitionSO", menuName = "Body Politic/Items/Holdable Definition")]
public class HoldableDefinitionSO : InteractableDefinitionSO
{
    [Tooltip("The role conferred to the NPC when this item is held in their hand. Can be null.")]
    public NpcRoleSO HeldRole;

    [Tooltip("The role conferred to the NPC when this item is stored in their inventory slots. Can be null.")]
    public NpcRoleSO InventoryRole;
}