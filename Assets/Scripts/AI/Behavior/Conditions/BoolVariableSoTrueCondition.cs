using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Bool Variable SO True", story: "[BoolVariableSO] is True", category: "Variable Conditions", id: "d0ae4e7be0a83fd60a624be6e8aa9797")]
public partial class BoolVariableSoTrueCondition : Condition
{
    [SerializeReference] public BlackboardVariable<BoolVariableSO> BoolVariableSO;

    public override bool IsTrue()
    {
        if (BoolVariableSO == null || BoolVariableSO.Value == null)
        {
            Debug.LogWarning("BoolVariableSO is not assigned or its value is null.");
            return false;
        }

        return BoolVariableSO.Value.Value;
    }
}
