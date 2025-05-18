using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Conditions/Combination/OR Condition", fileName = "OrConditionSO")]
public class OrConditionSO : AbstractConditionalSO
{
    [Header("Inputs")]
    [SerializeField]
    private List<AbstractVariableSO<bool>> conditions = new List<AbstractVariableSO<bool>>();

    protected override bool EvaluateCondition()
    {
        if (conditions == null || conditions.Count == 0)
        {
             Debug.LogWarning($"Condition '{name}': No conditions assigned to OR.", this);
             // Define behavior for empty list. Usually false for OR.
             return false;
        }

        // Check for null entries and evaluate. Return true if any condition is non-null and true.
        foreach (var condition in conditions)
        {
            if (condition != null && condition.Value) // Access the Value property
            {
                return true;
            }
            // Optional: Warn if a condition is null? Or just ignore it.
            // else if (condition == null) { Debug.LogWarning($"Condition '{name}': Contains a null condition entry.", this); }

        }
        // If loop completes, no condition was non-null and true
        return false;

        // Linq alternative:
        // return conditions.Any(c => c != null && c.Value);
    }
}