using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// Base class for ScriptableObjects that represent a boolean condition.
/// Uses a pull-based evaluation model (calculates value on demand).
/// Inherits from AbstractVariableSO<bool> so it can be used wherever a bool variable is needed.
/// </summary>
public abstract class AbstractConditionalSO : BoolVariableSO
{
    // [SerializeField] protected override bool _resetOnPlay => false;

    [Header("Condition Description")]
    [TextArea(3, 5)]
    public string description = "Describes the purpose of this condition.";

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

#if UNITY_EDITOR
    // Optional: Prevent direct editing of the 'Value' field in the inspector
    // as it's now calculated. You might need a custom editor for AbstractConditionalSO
    // to hide the inherited 'Value' field effectively, or just rely on the 'new' keyword's behavior.
    // The reset logic from AbstractVariableSO<bool> might not be relevant here,
    // as the 'Value' isn't stored state but calculated. Consider overriding
    // StoreStartValue/ResetToStartValue to do nothing or handle differently if needed.

    protected override void OnEnable() { /* Calculated value, no start value needed */ }
    protected override void OnDisable() { /* Calculated value, no reset needed */ }
    protected override void OnPlayModeStateChanged(PlayModeStateChange state) { /* Calculated value, no reset needed */ }
#endif
}
