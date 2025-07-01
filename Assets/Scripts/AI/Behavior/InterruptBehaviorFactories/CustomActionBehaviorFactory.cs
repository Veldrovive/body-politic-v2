using System;
using Unity.Behavior;
using UnityEngine;

/// Unifies a set of custom actions that can be used in similar situations. The first use case is for actions
/// that can be taken with knowledge only of the initiator and target, such as shooting, following, or running away.

[BlackboardEnum]
public enum CustomActionTargetType
{
    None,
    GameObject,
    Position
}

[Serializable]
public class CustomActionBehaviorParameters : BehaviorParameters
{
    public GameObject InitiatorGO;

    public CustomActionTargetType TargetType = CustomActionTargetType.GameObject;
    public GameObject TargetGO;
    public Vector3 TargetPosition;
}

public abstract class AbstractCustomActionBehaviorFactory : InterruptBehaviorFactory<CustomActionBehaviorParameters>
{
    [SerializeField] protected BehaviorGraph graph;
    [SerializeField] protected string displayName = "";
    [SerializeField] protected string displayDescription = "";
}