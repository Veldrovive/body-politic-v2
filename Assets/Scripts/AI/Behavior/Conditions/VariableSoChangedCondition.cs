using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "VariableSO Changed", story: "[VariableSO] has changed", category: "Variable Conditions", id: "9f6a59f9663711e6425fde03efa48144")]
public partial class VariableSoChangedCondition : Condition
{
    [SerializeReference] public BlackboardVariable VariableSO;
    
    private IVariableSOValueProvider valueProvider;
    private object previousValue;

    private void OnLoad()
    {
        if (VariableSO == null)
        {
            valueProvider = null;
            return;
        }

        valueProvider = VariableSO.ObjectValue as IVariableSOValueProvider;
        if (valueProvider == null)
        {
            Debug.LogWarning($"The variable '{VariableSO.Name}' does not implement IVariableSOValueProvider. It will not be able to detect changes.", VariableSO.ObjectValue as UnityEngine.Object);
        }

        // Initialize the first "previous" value.
        previousValue = valueProvider?.GetValueAsObject();
    }
    
    public override bool IsTrue()
    {
        // If the provider was never set up correctly, we can't detect a change.
        if (valueProvider == null)
        {
            return false;
        }

        object currentValue = valueProvider.GetValueAsObject();
        
        // object.Equals handles all cases: null/null, value/null, value/value.
        bool hasChanged = !Equals(currentValue, previousValue);

        // Always update the previous value for the next frame's check.
        previousValue = currentValue;

        return hasChanged;
    }

    public override void OnStart()
    {
        OnLoad();
    }
    
    public void OnDeserialize()
    {
        OnLoad();
    }
}
