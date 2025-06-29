using System;
using Unity.Behavior;
using UnityEngine;
using Composite = Unity.Behavior.Composite;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Get Zone Point", story: "Select [Position] in [Zone]", category: "Flow", id: "c0188e1e72d07fd108b51430789de366")]
public partial class GetZonePointSequence : Composite
{
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    [SerializeReference] public BlackboardVariable<Zone> Zone;
    
    [SerializeReference] public BlackboardVariable<bool> SnapToNavMesh = new(false);
    [SerializeReference] public BlackboardVariable<float> SnapRadius = new(0.5f);
    [SerializeReference] public BlackboardVariable<int> NumRetries = new(10);
    
    [SerializeReference] public Node PointSelected;
    [SerializeReference] public Node PointNotFound;

    protected override Status OnStart()
    {
        // BUG IN UNITY BEHAVIOR: Nodes are not properly deserialized so we need to grab them from the children.
        PointSelected = Children[0];
        PointNotFound = Children[1];
        
        if (Zone.Value == null)
        {
            Debug.LogError("GetZonePointSequence: Zone is not set.");
            return StartNode(PointNotFound);
        }
        
        if (NumRetries <= 0)
        {
            Debug.LogError("GetRandomZonePointGateState must be greater than or equal to zero.");
            return StartNode(PointNotFound);
        }
        
        if (SnapRadius <= 0)
        {
            Debug.LogError("SnapRadius must be greater than zero.");
            return StartNode(PointNotFound);
        }
        
        for (int i = 0; i < NumRetries; i++)
        {
            Vector3 proposalPoint = Zone.Value.GetRandomPointInZone();
            if (!SnapToNavMesh)
            {
                // We are done. We just wanted any point in the zone.
                Position.Value = proposalPoint;
                return StartNode(PointSelected);
            }
            else
            {
                // We have more work here. We first need to try to snap to the NavMesh.
                NavMesh.SamplePosition(proposalPoint, out NavMeshHit hit, SnapRadius, NavMesh.AllAreas);
                if (hit.hit)
                {
                    // Then we have a point on the NavMesh, but did we move out of the zone? We have a utility for that.
                    if (Zone.Value.IsPointInsideZone(hit.position))
                    {
                        // We are good to go. We have a point on the NavMesh and within the zone.
                        Position.Value = hit.position;
                        return StartNode(PointSelected);
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
        Debug.LogError($"GetRandomZonePointGateState: Failed to find a point in the zone and on the NavMesh after {NumRetries} tries.");
        return StartNode(PointNotFound);
    }
}

