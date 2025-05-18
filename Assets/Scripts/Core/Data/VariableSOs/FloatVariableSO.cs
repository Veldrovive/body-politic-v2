using UnityEngine;

[CreateAssetMenu(fileName = "FloatVariableSO", menuName = "Variables/Float Variable SO")]
public class FloatVariableSO : AbstractVariableSO<float>
{
    // This class inherits from AbstractVariableSO<float>, which means it can be used as a float variable.
    // You can add any additional functionality specific to float variables here if needed.
}
// public class FloatVariableSO : ScriptableObject
// {
//     public float Value;
// }
