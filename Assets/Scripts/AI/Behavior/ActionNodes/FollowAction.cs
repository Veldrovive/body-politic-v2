using System;
using System.Collections.Generic;
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
    Error
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Follow", story: "[Self] follows [Target]", category: "Action", id: "ebe11c50a81a5fd7bcb3e0605b592824")]
public partial class FollowAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<FollowActionOutcome> Outcome;
    
    [SerializeReference] public BlackboardVariable<FollowActionTargetingType> TargetingType = new(FollowActionTargetingType.Transform);
    [SerializeReference] public BlackboardVariable<Transform> TargetTransform = new(null);
    [SerializeReference] public BlackboardVariable<Vector3> TargetPosition = new(Vector3.zero);
    
    [SerializeReference] public BlackboardVariable<FollowActionDistanceConfiguration> DistanceConfiguration = new(FollowActionDistanceConfiguration.KeepWithinDistance);
    [SerializeReference] public BlackboardVariable<float> HorizontalDistanceParameter = new(1.5f);
    [SerializeReference] public BlackboardVariable<float> VerticalDistanceParameter = new(1.5f);
    
    [SerializeReference] public BlackboardVariable<FollowActionLoSConfiguration> LoSConfiguration = new(FollowActionLoSConfiguration.KeepWithinLoS);
    [SerializeReference] public BlackboardVariable<int> LoSObstacleLayerMask = new(0);
    
    [SerializeReference] public BlackboardVariable<FollowActionExitConditionCombination> ConditionCombination = new(FollowActionExitConditionCombination.AND);
    [SerializeReference] public BlackboardVariable<bool> FaceTargetWhenStopped = new(true);
    [SerializeReference] public BlackboardVariable<MovementSpeed> DesiredSpeed = new(MovementSpeed.Walk);
    [SerializeReference] public BlackboardVariable<float> AlignmentAngularSpeed = new(360f);
    [SerializeReference] public BlackboardVariable<float> AlignmentAngleTolerance = new(1f);
    
    [SerializeReference] public BlackboardVariable<float> MaxDuration = new(-1f);
    [SerializeReference] public BlackboardVariable<float> MaxDurationWithoutLoS = new(-1f); // If we are not keeping LoS, this is the max duration to wait before exiting.

    [CreateProperty] private float _startTime;  // Serialized to track the start time of the action.
    [CreateProperty] private float _lastLoSSuccessTime = 0f; // Used to track when we last had LoS, for the MaxDurationWithoutLoS exit condition.
    
    private enum FollowActionInternalState
    {
        Stopped,
        MovingToTarget,
    }
    
    private bool shouldExit = false;
    
    private FollowActionInternalState _internalState = FollowActionInternalState.Stopped;
    
    private bool isWithinDistance = false;
    private bool hasLoS = false;
    private bool initialized = false;

    private NpcContext selfContext;
    
    protected override Status OnLoad()
    {
        if (LoSObstacleLayerMask.Value == 0)
        {
            LoSObstacleLayerMask.Value = LayerMask.GetMask("Default");
        }
        selfContext = Self.Value.GetComponent<NpcContext>();
        if (selfContext == null)
        {
            Debug.LogError("FollowAction requires a valid NpcContext on the Self GameObject.", Self);
            return Status.Failure;
        }
        
        // Initialize the internal state to Stopped and set the initial conditions.
        BeginStoppedState();
        if (GetIsWithinDistance())
        {
            HandleMovedWithinDistance();
        }
        else
        {
            HandleMovedOutsideDistance();
        }
        
        if (GetHasLoS())
        {
            HandleLoSGained();
        }
        else
        {
            HandleLoSLost();
        }
        return Status.Running;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        _startTime = SaveableDataManager.Instance.time;
        _lastLoSSuccessTime = SaveableDataManager.Instance.time;
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();

        if (shouldExit)
        {
            return Status.Success;  // This action always succeeds and we use the Outcome variable to indicate the exit condition.
        }
        
        bool newIsWithinDistance = GetIsWithinDistance();
        bool newHasLoS = GetHasLoS();
        if (newHasLoS)
        {
            _lastLoSSuccessTime = SaveableDataManager.Instance.time;
        }

        if (newIsWithinDistance != isWithinDistance || !initialized)
        {
            if (newIsWithinDistance)
            {
                HandleMovedWithinDistance();
            }
            else
            {
                HandleMovedOutsideDistance();
            }
        }

        if (newHasLoS != hasLoS  || !initialized)
        {
            if (newHasLoS)
            {
                HandleLoSGained();
            }
            else
            {
                HandleLoSLost();
            }
        }

        bool isAligned = true;
        if (_internalState == FollowActionInternalState.Stopped && FaceTargetWhenStopped)
        {
            // Forward is the direction toward the target.
            Vector3 targetPosition = TargetingType == FollowActionTargetingType.Transform
                ? TargetTransform.Value.position
                : TargetPosition.Value;
            
            Vector3 forwardTarget = targetPosition - selfContext.transform.position;
            if (!AlignToward(forwardTarget))
            {
                isAligned = false;
            }
        }

        initialized = true;

        FollowActionOutcome? exitCondition = HandleShouldExit();
        if (exitCondition.HasValue)
        {
            if (FaceTargetWhenStopped && !isAligned)
            {
                // Then we don't actually exit because we need to align first.
                return Status.Running;
            }
            // We have an exit condition and we aren't aligning, so we trigger the exit.
            Outcome.Value = exitCondition.Value;
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (selfContext != null && selfContext.MovementManager != null)
        {
            CancelCurrentMovementRequest();
        }
        
        base.OnEnd();
    }
    
    private void CancelCurrentMovementRequest()
    {
        if (selfContext.MovementManager.HasMovementRequest)
        {
            selfContext.MovementManager.InterruptCurrentRequest();
        }
    }
    
    /// <summary>
    /// Ensure that all movement requests are stopped
    /// </summary>
    private void BeginStoppedState()
    {
        _internalState = FollowActionInternalState.Stopped;
        // All we have to do is make sure that we stop any previous movement requests.
        CancelCurrentMovementRequest();
    }
    
    /// <summary>
    /// Stops any previous requests and begins a new request to move to the target.
    /// </summary>
    private void BeginMovingToTargetState()
    {
        _internalState = FollowActionInternalState.MovingToTarget;
        // First we stop any previous movement requests.
        CancelCurrentMovementRequest();
        
        NpcMovementRequest request;
        if (TargetingType == FollowActionTargetingType.Transform)
        {
            request = new NpcMovementRequest(TargetTransform);
        }
        else if (TargetingType == FollowActionTargetingType.Position)
        {
            request = new NpcMovementRequest(TargetPosition);
        }
        else
        {
            Debug.LogError($"FollowState on {Self.Value?.name} has an invalid TargetingType: {TargetingType}");
            shouldExit = true;
            Outcome.Value = FollowActionOutcome.Error;
            return;
        }
        
        request.DesiredSpeed = DesiredSpeed;
        request.ReplanAtTargetMoveDistance = 0.1f;
        request.RequireExactPosition = true;
        request.RequireFinalAlignment = false;
        request.ExitOnComplete = true;

        MovementFailureReason? failureReason = selfContext.MovementManager?.SetMovementTarget(request);
        if (failureReason.HasValue)
        {
            HandleMovementFailure(failureReason.Value, null);
            _internalState = FollowActionInternalState.Stopped;
        }
    }
    
    /// <summary>
    /// Handles calling the correct method to shift states. Does not handle exiting the state.
    /// If Stopped, there is nothing to do.
    /// If AligningFaceDirection, there is nothing to do.
    /// If MovingToTarget we first check if we also need to KeepLoS and if so and we do not have LoS, do nothing.
    /// If that condition is satisfied, then we check if we should align the face direction. If not, we move to Stopped
    /// state. If so, we move to AligningFaceDirection state.
    /// </summary>
    private void HandleMovedWithinDistance()
    {
        isWithinDistance = true;

        if (_internalState == FollowActionInternalState.Stopped)
        {
            // Nothing to do, we are already stopped.
        }
        else if (_internalState == FollowActionInternalState.MovingToTarget)
        {
            // If we are also maintaining LoS and we do not have LoS, we do nothing.
            if (LoSConfiguration == FollowActionLoSConfiguration.KeepWithinLoS && !hasLoS)
            {
                // We do not have LoS, so we do nothing.
                return;
            }
            
            // We are moving to the target, so we check if we need to align the face direction.
            BeginStoppedState();
        }
    }
    
    /// <summary>
    /// Handles calling the correct method to shift states. Does not handle exiting the state.
    /// If Stopped, check if we are KeepingWithinDistance. If we are, we move to MovingToTarget state.
    /// If AligningFaceDirection, we check if we are KeepingWithinDistance. If we are, we move to MovingToTarget state
    ///     as moving has higher priority than aligning face direction.
    /// If MovingToTarget, there is nothing more to do as we are already moving to the target.
    /// </summary>
    private void HandleMovedOutsideDistance()
    {
        isWithinDistance = false;

        if (_internalState == FollowActionInternalState.Stopped)
        {
            // If we are keeping within distance, we move to MovingToTarget state.
            // We also begin moving if we are in the ExitWhenWithinDistance
            if (
                DistanceConfiguration.Value is 
                    FollowActionDistanceConfiguration.KeepWithinDistance or
                    FollowActionDistanceConfiguration.ExitWhenWithinDistance
            )
            {
                BeginMovingToTargetState();
            }
            // Otherwise, we do nothing as we are not maintaining distance.
        }
        else if (_internalState == FollowActionInternalState.MovingToTarget)
        {
            // We are already moving to the target, so we have nothing more to do.
        }
    }
    
    /// <summary>
    /// Handles calling the correct method to shift states. Does not handle exiting the state.
    /// If Stopped, there is nothing to do.
    /// If AligningFaceDirection, there is nothing to do.
    /// If MovingToTarget, check if we also need to KeepWithinDistance and if that has been satisfied. If not, do nothing.
    /// If we are within the distance, we check if we should align the face direction. If not, we move to Stopped state.
    /// If so, we move to AligningFaceDirection state.
    /// </summary>
    private void HandleLoSGained()
    {
        hasLoS = true;
        
        if (_internalState == FollowActionInternalState.Stopped)
        {
            // Nothing to do, we are already stopped.
        }
        else if (_internalState == FollowActionInternalState.MovingToTarget)
        {
            // If we are also maintaining distance and we are not within distance, we do nothing.
            if (DistanceConfiguration == FollowActionDistanceConfiguration.KeepWithinDistance && !isWithinDistance)
            {
                // We are not within distance, so we do nothing.
                return;
            }

            // We are moving to the target, so we check if we need to align the face direction.
            BeginStoppedState();
        }
    }
    
    /// <summary>
    /// Handles calling the correct method to shift states. Does not handle exiting the state.
    /// If Stopped, check if we are KeepingWithinLoS. If we are, we move to MovingToTarget state.
    /// If AligningFaceDirection, we check if we are KeepingWithinLoS. If we are, we move to MovingToTarget state
    ///     as moving has higher priority than aligning face direction.
    /// if MovingToTarget, there is nothing more to do as we are already moving to the target.
    /// </summary>
    private void HandleLoSLost()
    {
        hasLoS = false;
        
        if (_internalState == FollowActionInternalState.Stopped)
        {
            // If we are keeping within LoS, we move to MovingToTarget state.
            if (
                LoSConfiguration.Value is 
                    FollowActionLoSConfiguration.KeepWithinLoS or 
                    FollowActionLoSConfiguration.ExitWhenWithinLoS
            )
            {
                BeginMovingToTargetState();
            }
            // Otherwise, we do nothing as we are not maintaining LoS.
        }
        else if (_internalState == FollowActionInternalState.MovingToTarget)
        {
            // We are already moving to the target, so we have nothing more to do.
        }
    }
    
    private void HandleMovementFailure(MovementFailureReason reason, object failureData)
    {
        Debug.LogWarning($"FollowState on {Self.Value?.name} encountered a movement failure: {reason}");
        switch (reason)
        {
            // There's a big long list of reasons that we can fail to move, but most of them are just generic errors
            // as far as a designer is concerned. All of these just result in a generic MovementManagerError outcome.
            case MovementFailureReason.AgentNotOnNavMesh:
            case MovementFailureReason.InvalidRequestParameters:
            case MovementFailureReason.NoValidPathFound:
            case MovementFailureReason.ReplanningFailed:
            case MovementFailureReason.RequestNull:
            case MovementFailureReason.TargetPositionInvalid:
            case MovementFailureReason.TargetTransformNull:
                Outcome.Value = FollowActionOutcome.Error;
                shouldExit = true;
                return;
            case MovementFailureReason.LinkTraversalFailed:
                Outcome.Value = FollowActionOutcome.Error;
                shouldExit = true;
                return;
            case MovementFailureReason.Interrupted:
                // This is expected when we are exiting the state, so we do nothing.
                return;
            default:
                // This is an unexpected failure reason, so we log an error and exit the state.
                Debug.LogWarning($"FollowState on {Self.Value?.name} encountered an unexpected movement failure reason: {reason}");
                return;
        }
    }
    
    /// <summary>
    /// Checks whether we are within both the horizontal and vertical distance parameters. Horizontal is computed
    /// using a distance on the xz plane, and vertical is just the absolute difference in the y coordinate.
    /// </summary>
    /// <returns></returns>
    private bool GetIsWithinDistance()
    {
        Vector3 targetPosition = TargetingType == FollowActionTargetingType.Transform
            ? TargetTransform.Value.position
            : TargetPosition.Value;
        
        Vector3 npcPosition = selfContext.transform.position;
        
        // Calculate the horizontal distance on the xz plane
        float verticalDistance = Mathf.Abs(npcPosition.y - targetPosition.y);
        if (verticalDistance > VerticalDistanceParameter)
        {
            return false; // If the vertical distance is greater than the allowed parameter, we are not within distance.
        }
        
        Vector3 difference = npcPosition - targetPosition;
        difference.y = 0;
        if (difference.sqrMagnitude > HorizontalDistanceParameter * HorizontalDistanceParameter)
        {
            return false; // If the horizontal distance is greater than the allowed parameter, we are not within distance.
        }
        
        return true; // We are within both the horizontal and vertical distance parameters.
    }
    
    /// <summary>
    /// Checks whether a raycast from the NPC to the target is clear of obstacles using a LineCast.
    /// </summary>
    /// <returns></returns>
    private bool GetHasLoS()
    {
        Vector3 targetPosition = TargetingType == FollowActionTargetingType.Transform
            ? TargetTransform.Value.position
            : TargetPosition.Value;
        
        Vector3 originPoint = selfContext.transform.position;
        
        RaycastHit hitInfo;
        if (Physics.Linecast(originPoint, targetPosition, out hitInfo, LoSObstacleLayerMask.Value))
        {
            // An obstacle was hit.
            if (TargetingType == FollowActionTargetingType.Transform &&
                // Check if the hit transform is the target or a descendant of the target.
                hitInfo.transform.IsChildOf(TargetTransform.Value))
            {
                // Then what we hit was part of the target, so we have LoS.
                return true;
            }
    
            // Otherwise we hit something else, so we do not have LoS.
            return false;
        }
        
        // No obstacle was hit along the path, so we have LoS.
        return true;
    }
    
    /// <summary>
    /// Triggers exit based on the exit conditions. Does not handle the special case of exiting due to a role door.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private FollowActionOutcome? HandleShouldExit()
    {
        if (MaxDuration > 0f && SaveableDataManager.Instance.time - _startTime > MaxDuration)
        {
            return FollowActionOutcome.Timeout;
        }
        
        if (MaxDurationWithoutLoS > 0f && !hasLoS && SaveableDataManager.Instance.time - _lastLoSSuccessTime > MaxDurationWithoutLoS)
        {
            return FollowActionOutcome.Timeout;
        }

        bool distanceExitConditionMet = false;
        bool useDistanceExitCondition = false;
        switch (DistanceConfiguration.Value)
        {
            case FollowActionDistanceConfiguration.IgnoreDistance:
                distanceExitConditionMet = false;
                useDistanceExitCondition = false;
                break;
            case FollowActionDistanceConfiguration.KeepWithinDistance:
                distanceExitConditionMet = false;
                useDistanceExitCondition = false;
                break;
            case FollowActionDistanceConfiguration.ExitWhenOutsideDistance:
                distanceExitConditionMet = !isWithinDistance;
                useDistanceExitCondition = true;
                break;
            case FollowActionDistanceConfiguration.ExitWhenWithinDistance:
                distanceExitConditionMet = isWithinDistance;
                useDistanceExitCondition = true;
                break;
        }
        
        bool loSExitConditionMet = false;
        bool useLoSExitCondition = false;
        switch (LoSConfiguration.Value)
        {
            case FollowActionLoSConfiguration.IgnoreLoS:
                loSExitConditionMet = false;
                useLoSExitCondition = false;
                break;
            case FollowActionLoSConfiguration.KeepWithinLoS:
                loSExitConditionMet = false;
                useLoSExitCondition = false;
                break;
            case FollowActionLoSConfiguration.ExitWhenOutsideLoS:
                loSExitConditionMet = !hasLoS;
                useLoSExitCondition = true;
                break;
            case FollowActionLoSConfiguration.ExitWhenWithinLoS:
                loSExitConditionMet = hasLoS;
                useLoSExitCondition = true;
                break;
        }

        if (!useDistanceExitCondition && !useLoSExitCondition)
        {
            // We don't have any exit conditions to check.
            return null;
        }
        else if (useDistanceExitCondition && !useLoSExitCondition)
        {
            // Then we should exit if the distance condition is met not matter what because we don't care about LoS.
            if (distanceExitConditionMet)
            {
                return FollowActionOutcome.Completed;
            }
        }
        else if (!useDistanceExitCondition && useLoSExitCondition)
        {
            // Then we should exit if the LoS condition is met not matter what because we don't care about distance.
            if (loSExitConditionMet)
            {
                return FollowActionOutcome.Completed;
            }
        }
        else
        {
            // We have both distance and LoS exit conditions to check.
            if (ConditionCombination == FollowActionExitConditionCombination.AND)
            {
                if (distanceExitConditionMet && loSExitConditionMet)
                {
                    return FollowActionOutcome.Completed;
                }
            }
            else
            {
                // OR condition
                if (distanceExitConditionMet || loSExitConditionMet)
                {
                    return FollowActionOutcome.Completed;
                }
            }
        }
        
        // If we reach here, we have not met any exit conditions.
        return null;
    }
    
    /// <summary>
    /// Turns the agent to face the target direction.
    /// Returns True if we are within the angle tolerance.
    /// </summary>
    /// <param name="forwardTarget"></param>
    /// <returns></returns>
    private bool AlignToward(Vector3 forwardTarget)
    {
        // We need to be careful that the forward target is on the plane
        forwardTarget = Vector3.ProjectOnPlane(forwardTarget, Vector3.up);
        Quaternion targetRotation = Quaternion.LookRotation(forwardTarget);
        Transform transform = Self.Value.transform;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            AlignmentAngularSpeed * Time.deltaTime
        );

        bool aligned = Quaternion.Angle(transform.rotation, targetRotation) < AlignmentAngleTolerance;
        if (aligned)
        {
            // We also just snap to the target rotation
            transform.rotation = targetRotation;
        }

        return aligned;
    }
    
    
}

