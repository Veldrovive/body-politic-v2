using UnityEngine;

[CreateAssetMenu(fileName = "IntVariableSO", menuName = "Variables/Int Variable SO")]
public class IntVariableSO : AbstractVariableSO<int>
{
    // This class inherits from AbstractVariableSO<int>, which means it can be used as an int variable.
    // You can add any additional functionality specific to int variables here if needed.
}
// public class VariableSO : ScriptableObject
// {
//     public int Value;
// }
