using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

/// Handles an NPC following a transform
/// Exit conditions include duration, satisfying the distance, LoS, and facing conditions, or a BoolVariable being set.
/// Optionally keeps within line of sight (State are keep LoS, ignore LoS, or require LoS)
/// Optionally always faces the target when stopped
/// For both distance and LoS, we can either move to keep within distance/LoS, ignore distance/LoS, exit
/// when within distance/LoS, or exit when outside distance/LoS.
/// What about when we only want to look and not move? This is where the exit when outside distance/LoS comes in, but right
/// now there doesn't seem to be a way to look without moving without infinite distance. maybe a bool for whether to move and a bool for whether to face?
///
/// Tech Details: We can achieve this by closely managing the movement manager. When we are not in a "finished" state
/// where we have satisfied the distance, LoS, and facing conditions, we stop the movement manager.
/// When we have not satisfied either the distance or LoS conditions, we set the movement manager to move to the target.
/// If distance and LoS are satisfied, we stop the movement manager and start a new target for right where we are but with
/// a facing direction toward the target if required.
/// Whenever we swap between one of these states, we first have to exit the current movement manager state
/// using InterruptCurrentRequest. This immediately stops the current movement request so that we can start a new one
/// in the same frame.
///
/// The default exit is "Completed" which means we exited gracefully because an exit condition was met.
/// If we are stopped by a role door, we do not retry. We exit with a custom condition.
[BlackboardEnum]
public enum FollowActionDistanceConfiguration
{
    IgnoreDistance,
    KeepWithinDistance,
    ExitWhenWithinDistance,
    ExitWhenOutsideDistance,
}

[BlackboardEnum]
public enum FollowActionLoSConfiguration
{
    IgnoreLoS,
    KeepWithinLoS,
    ExitWhenWithinLoS,
    ExitWhenOutsideLoS,
}

[BlackboardEnum]
public enum FollowActionExitConditionCombination
{
    AND,
    OR
}

[BlackboardEnum]
public enum FollowActionTargetingType
{
    Transform,
    Position
}

[BlackboardEnum]
public enum FollowActionOutcome
{
    Completed,
    Timeout,
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Follow", story: "[Self] follows [Target]", category: "Action", id: "ebe11c50a81a5fd7bcb3e0605b592824")]
public partial class FollowAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<FollowActionOutcome> Outcome;
    
    [SerializeReference] public BlackboardVariable<FollowActionTargetingType> TargetingType = new(FollowActionTargetingType.Transform);
    [SerializeReference] public BlackboardVariable<Transform> TargetTransform = new(null);
    [SerializeReference] public BlackboardVariable<Vector3> TargetPosition = new(Vector3.zero);
    
    [SerializeReference] public BlackboardVariable<FollowActionDistanceConfiguration> DistanceConfiguration = new(FollowActionDistanceConfiguration.KeepWithinDistance);
    [SerializeReference] public BlackboardVariable<float> HorizontalDistanceParameter = new(1.5f);
    [SerializeReference] public BlackboardVariable<float> VerticalDistanceParameter = new(1.5f);
    
    [SerializeReference] public BlackboardVariable<FollowActionLoSConfiguration> LoSConfiguration = new(FollowActionLoSConfiguration.KeepWithinLoS);
    [SerializeReference] public BlackboardVariable<LayerMask> LoSObstacleLayerMask = new(LayerMask.GetMask("Default"));
    
    [SerializeReference] public BlackboardVariable<FollowActionExitConditionCombination> ConditionCombination = new(FollowActionExitConditionCombination.AND);
    [SerializeReference] public BlackboardVariable<bool> FaceTargetWhenStopped = new(true);
    [SerializeReference] public BlackboardVariable<MovementSpeed> DesiredSpeed = new(MovementSpeed.Walk);
    [SerializeReference] public BlackboardVariable<float> AlignmentAngularSpeed = new(360f);
    [SerializeReference] public BlackboardVariable<float> AlignmentAngleTolerance = new(1f);
    
    [SerializeReference] public BlackboardVariable<float> MaxDuration = new(-1f);
    [SerializeReference] public BlackboardVariable<float> MaxDurationWithoutLoS = new(-1f); // If we are not keeping LoS, this is the max duration to wait before exiting.

    private enum FollowActionInternalState
    {
        Stopped,
        MovingToTarget,
    }
    
    private bool isExited = false;

    private float _startTime;
    private float _lastLoSSuccessTime = 0f; // Used to track when we last had LoS, for the MaxDurationWithoutLoS exit condition.
    private FollowActionInternalState _internalState = FollowActionInternalState.Stopped;
    
    private bool isWithinDistance = false;
    private bool hasLoS = false;
    private bool initialized = false;
    
    protected override Status OnLoad()
    {
        return Status.Running;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();
        return Status.Success;
    }

    protected override void OnEnd()
    {
        base.OnEnd();
    }
}

