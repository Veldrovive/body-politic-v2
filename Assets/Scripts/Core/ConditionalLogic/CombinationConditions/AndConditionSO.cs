using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Conditions/Combination/AND Condition", fileName = "AndConditionSO")]
public class AndConditionSO : AbstractConditionalSO
{
    [Header("Inputs")]
    [SerializeField]
    private List<AbstractVariableSO<bool>> conditions = new List<AbstractVariableSO<bool>>();

    protected override bool EvaluateCondition()
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.LogWarning($"Condition '{name}': No conditions assigned to AND.", this);
            // Define behavior for empty list. Usually true for AND (vacuously true).
            return true;
        }

        // Check for null entries and evaluate. Return false if any condition is null or false.
        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                 Debug.LogWarning($"Condition '{name}': Contains a null condition entry.", this);
                 return false; // Treat null entry as false
            }
            if (!condition.Value) // Access the Value property (which evaluates if it's a ConditionalSOBase)
            {
                return false;
            }
        }
        // If loop completes, all conditions were non-null and true
        return true;

        // Linq alternative (slightly less performant due to allocation, potentially clearer):
        // return conditions.All(c => c != null && c.Value);
    }
}