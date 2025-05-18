using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class FloatReferenceRangeCondition
{
    [Tooltip("The value to check if it's within the range.")]
    [SerializeField] private FloatReference valueToCheck = new FloatReference();
    [Tooltip("The minimum value of the range.")]
    [SerializeField] private FloatReference min = new FloatReference();
    [Tooltip("The maximum value of the range.")]
    [SerializeField] private FloatReference max = new FloatReference();
    [Tooltip("If true, check >= min and <= max. If false, check > min and < max.")]
    [SerializeField] private bool inclusive = true;

    public bool IsInRange()
    {
        float val = valueToCheck.Value;
        float minVal = min.Value;
        float maxVal = max.Value;
        return inclusive ? (val >= minVal && val <= maxVal) : (val > minVal && val < maxVal);
    }
}

/// <summary>
/// A ScriptableObject that checks if a set of FloatReference are in a range
/// </summary>
[CreateAssetMenu(menuName = "Conditions/Atomic/Float Range Condition", fileName = "FloatRangeConditionSO")]
public class FloatRangeConditionSO : AbstractConditionalSO
{
    [Header("Condition Settings")]
    [Tooltip("How the results of individual comparisons are combined.")]
    [SerializeField] private CombinationType combinationType = CombinationType.AND;

    [Tooltip("List of boolean equality comparisons to perform.")]
    [SerializeField] private List<FloatReferenceRangeCondition> conditions = new List<FloatReferenceRangeCondition>();


    protected override bool EvaluateCondition()
    {
        if (conditions == null || conditions.Count == 0)
        {
             Debug.LogWarning($"Condition '{name}': No sub-conditions defined.", this);
             // Default for empty list: AND -> true, OR -> false
             return combinationType == CombinationType.AND;
        }

        if (combinationType == CombinationType.AND)
        {
            // Using Linq All() for conciseness
            return conditions.All(condition => condition.IsInRange());
        }
        else // OR
        {
            // Using Linq Any() for conciseness
            return conditions.Any(condition => condition.IsInRange());
        }
    }
}