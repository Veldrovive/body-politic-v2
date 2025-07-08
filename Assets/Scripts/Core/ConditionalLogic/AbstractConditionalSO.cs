using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.PlayerLoop;

/// <summary>
/// Base class for ScriptableObjects that represent a boolean condition.
/// Uses a pull-based evaluation model (calculates value on demand).
/// Inherits from AbstractVariableSO<bool> so it can be used wherever a bool variable is needed.
/// </summary>
public abstract class AbstractConditionalSO : BoolVariableSO
{
    // [SerializeField] protected override bool _resetOnPlay => false;

    /// <summary>
    /// Hides the base 'Value' field and provides a calculated value
    /// based on the specific condition's logic.
    /// Accessing this property triggers the evaluation.
    /// </summary>
    public override bool Value => EvaluateCondition();

    /// <summary>
    /// Derived classes must implement this method to define their specific
    /// condition logic based on their input variables/values.
    /// </summary>
    /// <returns>True if the condition is met, false otherwise.</returns>
    protected abstract bool EvaluateCondition();
}
