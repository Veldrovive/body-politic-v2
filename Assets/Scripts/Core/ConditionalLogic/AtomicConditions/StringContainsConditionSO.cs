using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class StringReferenceContainsCondition
{
    [Tooltip("The string to search within.")]
    [SerializeField] private StringReference stringToCheck = new StringReference();
    [Tooltip("The substring to search for.")]
    [SerializeField] private StringReference substring = new StringReference();
    [SerializeField] private bool ignoreCase = false;

    public bool DoesContain()
    {
        string mainStr = stringToCheck.Value ?? ""; // Handle nulls
        string subStr = substring.Value ?? "";     // Handle nulls

        if (string.IsNullOrEmpty(subStr)) return true; // Contains "" is true

        System.StringComparison comparison = ignoreCase
            ? System.StringComparison.OrdinalIgnoreCase
            : System.StringComparison.Ordinal;

        #if UNITY_2021_2_OR_NEWER || NETSTANDARD_2_1_OR_GREATER
            return mainStr.Contains(subStr, comparison);
        #else
            // Fallback
             return mainStr.IndexOf(subStr, comparison) >= 0;
        #endif
    }
}

/// <summary>
/// A ScriptableObject that checks if a set of StringReference contains a substring
/// </summary>
[CreateAssetMenu(menuName = "Conditions/Atomic/String Contains Condition", fileName = "StringContainsConditionSO")]
public class StringContainsConditionSO : AbstractConditionalSO
{
    [Header("Condition Settings")]
    [Tooltip("How the results of individual comparisons are combined.")]
    [SerializeField] private CombinationType combinationType = CombinationType.AND;

    [Tooltip("List of boolean equality comparisons to perform.")]
    [SerializeField] private List<StringReferenceContainsCondition> conditions = new List<StringReferenceContainsCondition>();


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
            return conditions.All(condition => condition.DoesContain());
        }
        else // OR
        {
            // Using Linq Any() for conciseness
            return conditions.Any(condition => condition.DoesContain());
        }
    }
}