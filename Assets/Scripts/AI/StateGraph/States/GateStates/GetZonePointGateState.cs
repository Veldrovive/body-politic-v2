using System;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class GetZonePointGateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(GetZonePointGateState);
    
    public Zone FeasibleZone;
    public bool SnapToNavMesh = false;
    public int NumRetries = 10; // Number of tries to find a point within the zone and on the NavMesh
    public float SnapRadius = 0.5f; // The sphere radius for snapping to the mesh
    public Vector3VariableSO RandomPoint;
}

public enum GetZonePointGateStateOutcome
{
    PointFound,
    PointNotFound,
}

public class GetZonePointGateState : GenericAbstractState<GetZonePointGateStateOutcome, GetZonePointGateStateConfiguration>
{
    [Tooltip("The zone in which to select a random point.")]
    [SerializeField] private Zone feasibleZone;
    
    [Tooltip("The variableSO to store the random point in.")]
    [SerializeField] private Vector3VariableSO randomPoint;
    
    [Header("NavMesh Settings")]
    [Tooltip("If true, will try to snap to a point on the NavMesh.")]
    [SerializeField] private bool snapToNavMesh = false;
    // Note that when we snap to the NavMesh, we need to re-check that we are within the feasible zone as the snap
    // could potentially move us outside of it.
    [Tooltip("The sphere radius for snapping to the mesh.")]
    [SerializeField] private float snapRadius = 0.5f;
    [Tooltip("Number of tries to find a both within the zone and on the NavMesh.")]
    [SerializeField] private int numRetries = 10;  // We will try to find a point up to this many times before giving up.
    // Only applicable if trying to snap to the NavMesh.

    public override void ConfigureState(GetZonePointGateStateConfiguration configuration)
    {
        feasibleZone = configuration.FeasibleZone;
        snapToNavMesh = configuration.SnapToNavMesh;
        numRetries = configuration.NumRetries;
        snapRadius = configuration.SnapRadius;
        randomPoint = configuration.RandomPoint;
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        if (feasibleZone == null)
        {
            Debug.LogWarning($"GetZonePoint on {gameObject.name} does not have a zone set.");
            TriggerExit(GetZonePointGateStateOutcome.PointNotFound);
            return;
        }

        if (randomPoint == null)
        {
            Debug.LogWarning($"GetZonePoint on {gameObject.name} does not have a random point variable set.");
            TriggerExit(GetZonePointGateStateOutcome.PointNotFound);
            return;
        }

        if (numRetries <= 0)
        {
            Debug.LogError("GetRandomZonePointGateState must be greater than or equal to zero.");
            TriggerExit(GetZonePointGateStateOutcome.PointNotFound);
            return;
        }

        if (snapRadius <= 0)
        {
            Debug.LogError("SnapRadius must be greater than zero.");
            TriggerExit(GetZonePointGateStateOutcome.PointNotFound);
            return;
        }
        
        for (int i = 0; i < numRetries; i++)
        {
            Vector3 proposalPoint = feasibleZone.GetRandomPointInZone();
            if (!snapToNavMesh)
            {
                // We are done. We just wanted any point in the zone.
                randomPoint.Value = proposalPoint;
                TriggerExit(GetZonePointGateStateOutcome.PointFound);
                return;
            }
            else
            {
                // We have more work here. We first need to try to snap to the NavMesh.
                NavMesh.SamplePosition(proposalPoint, out NavMeshHit hit, snapRadius, NavMesh.AllAreas);
                if (hit.hit)
                {
                    // Then we have a point on the NavMesh, but did we move out of the zone? We have a utility for that.
                    if (feasibleZone.IsPointInsideZone(hit.position))
                    {
                        // We are good to go. We have a point on the NavMesh and within the zone.
                        randomPoint.Value = hit.position;
                        TriggerExit(GetZonePointGateStateOutcome.PointFound);
                        return;
                    }
                    else
                    {
                        // We are not in the zone. Try again.
                        Debug.LogWarning($"GetRandomZonePointGateState: Point {hit.position} is not in the zone. Retrying...");
                    }
                }
            }
        }
        
        // We failed to find a point in the zone and on the NavMesh after the specified number of tries.
        Debug.LogError($"GetRandomZonePointGateState: Failed to find a point in the zone and on the NavMesh after {numRetries} tries.");
        TriggerExit(GetZonePointGateStateOutcome.PointNotFound);
    }
}