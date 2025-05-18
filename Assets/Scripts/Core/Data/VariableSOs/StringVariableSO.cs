using UnityEngine;

[CreateAssetMenu(fileName = "StringVariableSO", menuName = "Variables/String Variable SO")]
public class StringVariableSO : AbstractVariableSO<string>
{
    // This class inherits from AbstractVariableSO<int>, which means it can be used as an int variable.
    // You can add any additional functionality specific to int variables here if needed.
}