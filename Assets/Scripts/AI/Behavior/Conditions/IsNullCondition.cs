using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Is Null", story: "[Variable] is null", category: "Variable Conditions", id: "d391ac62e3543a3a84304cae3cf9419b")]
public partial class IsNullCondition : Condition
{
    [SerializeReference] public BlackboardVariable Variable;

    public override bool IsTrue()
    {
        // Debug.Log($"IsNullCondition: Checking if variable {Variable?.Name} with value {Variable?.ObjectValue} is null {Variable?.ObjectValue == null}.");
        if (Variable == null)
        {
            // If the variable itself is null, we consider it null.
            Debug.LogWarning("IsNullCondition: Variable is null.");
            return true;
        }
        
        // Null checking is a bit more complicated than you would think as different types have different nullability.
        if (Variable.Type == typeof(Transform))
        {
            Transform transformValue = Variable.ObjectValue as Transform;
            if (transformValue == null)
            {
                return true;
            }
            return false;
        }
        return Variable?.ObjectValue == null;
    }
}
