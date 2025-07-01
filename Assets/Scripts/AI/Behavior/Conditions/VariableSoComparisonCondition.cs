using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "VariableSO Comparison", story: "[VariableSO] is [Value]", category: "Variable Conditions", id: "2537ba4c78d5d11698a04bded0c8c7b8")]
public partial class VariableSoComparisonCondition : Condition
{
    [SerializeReference] public BlackboardVariable VariableSO;
    [SerializeReference] public BlackboardVariable Value;

    public override bool IsTrue()
    {
        // 1. Get the object referenced by the 'VariableSO' BlackboardVariable.
        //    This should resolve to your ScriptableObject instance.
        object variableSoRef = VariableSO.ObjectValue;

        // 2. Check if the resolved object is null or doesn't implement our interface.
        if (variableSoRef is not IVariableSOValueProvider valueProvider)
        {
            // Log an error if it's not the right type. It might be null or the user
            // might have assigned something other than a VariableSO.
            Debug.LogError($"The object assigned to 'VariableSO' is either null or does not implement IValueProvider.");
            return false;
        }

        // 3. Get the actual underlying value from the VariableSO (e.g., the int, string, etc.).
        object valueFromSO = valueProvider.GetValueAsObject();

        // 4. Get the comparison value from the 'Value' BlackboardVariable.
        //    This could be a literal (e.g., 5) or resolve from another variable.
        object comparisonValue = Value.ObjectValue;
        
        // 5. Perform the comparison. Use object.Equals for robust checking,
        //    which correctly handles nulls.
        return Equals(valueFromSO, comparisonValue);
    }
}
