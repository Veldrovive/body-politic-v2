using UnityEngine;

[CreateAssetMenu(fileName = "BoolVariableSO", menuName = "Variables/Bool Variable SO")]
public class BoolVariableSO : AbstractVariableSO<bool>
{
    // This class inherits from AbstractVariableSO<int>, which means it can be used as an int variable.
    // You can add any additional functionality specific to int variables here if needed.
}