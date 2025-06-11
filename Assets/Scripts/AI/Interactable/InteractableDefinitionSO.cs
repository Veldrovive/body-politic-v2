using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Defines the shared properties for a specific type of holdable item.
/// </summary>
[CreateAssetMenu(fileName = "InteractableDefinitionSO", menuName = "Body Politic/Items/Interactable Definition")]
public class InteractableDefinitionSO : ScriptableObject
{
    [FormerlySerializedAs("ItemName")] [Tooltip("The user-facing name of the item (for UI/Debugging).")]
    public string Name;

    [FormerlySerializedAs("ItemDescription")]
    [Tooltip("The user-facing description of the item (for UI/Debugging).")]
    [TextArea]
    public string Description;
}