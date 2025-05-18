using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class IntReferenceRangeCondition
{
    [Tooltip("The value to check if it's within the range.")]
    [SerializeField] private IntReference valueToCheck = new IntReference();
    [Tooltip("The minimum value of the range.")]
    [SerializeField] private IntReference min = new IntReference();
    [Tooltip("The maximum value of the range.")]
    [SerializeField] private IntReference max = new IntReference();
    [Tooltip("If true, check >= min and <= max. If false, check > min and < max.")]
    [SerializeField] private bool inclusive = true;

    public bool IsInRange()
    {
        int val = valueToCheck.Value;
        int minVal = min.Value;
        int maxVal = max.Value;
        return inclusive ? (val >= minVal && val <= maxVal) : (val > minVal && val < maxVal);
    }
}

/// <summary>
/// A ScriptableObject that checks if a set of IntReference are in a range
/// </summary>
[CreateAssetMenu(menuName = "Conditions/Atomic/Int Range Condition", fileName = "IntRangeConditionSO")]
public class IntRangeConditionSO : AbstractConditionalSO
{
    [Header("Condition Settings")]
    [Tooltip("How the results of individual comparisons are combined.")]
    [SerializeField] private CombinationType combinationType = CombinationType.AND;

    [Tooltip("List of boolean equality comparisons to perform.")]
    [SerializeField] private List<IntReferenceRangeCondition> conditions = new List<IntReferenceRangeCondition>();


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