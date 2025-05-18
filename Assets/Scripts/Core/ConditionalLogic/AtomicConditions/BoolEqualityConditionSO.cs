using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A condition that checks if two BoolReferences are equal.
/// </summary>
[Serializable]
public class BoolReferenceEqualityCondition
{
    [Tooltip("Left side of the comparison.")]
    [SerializeField] private BoolReference left = new BoolReference();
    [Tooltip("Right side of the comparison.")]
    [SerializeField] private BoolReference right = new BoolReference();

    public bool AreEqual()
    {
        // Reference types handle null checks internally via their Value getter potentially
        return left.Value == right.Value;
    }
}

/// <summary>
/// A ScriptableObject that checks if a set of BoolReferences are equal
/// </summary>
[CreateAssetMenu(menuName = "Conditions/Atomic/Bool Equality Condition", fileName = "BoolEqualityConditionSO")]
public class BoolEqualityConditionSO : AbstractConditionalSO
{
    [Header("Condition Settings")]
    [Tooltip("How the results of individual comparisons are combined.")]
    [SerializeField] private CombinationType combinationType = CombinationType.AND;

    [Tooltip("List of boolean equality comparisons to perform.")]
    [SerializeField] private List<BoolReferenceEqualityCondition> conditions = new List<BoolReferenceEqualityCondition>();


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
            return conditions.All(condition => condition.AreEqual());
        }
        else // OR
        {
            // Using Linq Any() for conciseness
            return conditions.Any(condition => condition.AreEqual());
        }
    }
}