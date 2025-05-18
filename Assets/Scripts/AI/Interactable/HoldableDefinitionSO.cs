using UnityEngine;

/// <summary>
/// Defines the shared properties for a specific type of holdable item.
/// </summary>
[CreateAssetMenu(fileName = "HoldableDefinitionSO", menuName = "Body Politic/Items/Holdable Definition")]
public class HoldableDefinitionSO : ScriptableObject
{
    [Tooltip("The user-facing name of the item (for UI/Debugging).")]
    public string ItemName;

    [Tooltip("The user-facing description of the item (for UI/Debugging).")]
    [TextArea]
    public string ItemDescription;

    [Tooltip("The role conferred to the NPC when this item is held in their hand. Can be null.")]
    public NpcRoleSO HeldRole;

    [Tooltip("The role conferred to the NPC when this item is stored in their inventory slots. Can be null.")]
    public NpcRoleSO InventoryRole;

    // Optional fields from design doc (uncomment or add as needed)
    // public GameObject VisualPrefab;
    // public float Weight;
    // public int MaxStackSize = 1;
}