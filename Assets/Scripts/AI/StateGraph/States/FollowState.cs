using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
public enum FollowStateDistanceConfiguration
{
    IgnoreDistance,
    KeepWithinDistance,
    ExitWhenWithinDistance,
    ExitWhenOutsideDistance,
}

public enum FollowStateLoSConfiguration
{
    IgnoreLoS,
    KeepWithinLoS,
    ExitWhenWithinLoS,
    ExitWhenOutsideLoS,
}

public enum FollowStateExitConditionCombination
{
    AND,
    OR
}

public enum FollowStateTargetingType
{
    Transform,
    Position
}

[Serializable]
public class FollowStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(FollowState);
    
    [Header("Targeting")]
    public FollowStateTargetingType TargetingType = FollowStateTargetingType.Transform;
    public TransformReference TargetTransform = new TransformReference();
    public Vector3Reference TargetPosition = new Vector3Reference();

    [Header("Distance/LoS Configuration")]
    public FollowStateDistanceConfiguration DistanceConfiguration = FollowStateDistanceConfiguration.KeepWithinDistance;
    public float HorizontalDistanceParameter = 1.5f;
    public float VerticalDistanceParameter = 1.5f;
    
    public FollowStateLoSConfiguration LoSConfiguration = FollowStateLoSConfiguration.KeepWithinLoS;
    public LayerMask LoSObstacleLayerMask;

    public FollowStateExitConditionCombination ConditionCombination = FollowStateExitConditionCombination.AND;
    
    [Header("Movement Configuration")]
    public bool FaceTargetWhenStopped = true; // Whether to face the target when stopped.
    public MovementSpeed MovementSpeed = MovementSpeed.Walk; // The speed at which to move toward the target.
    public float AlignmentAngularSpeed = 360f; // The speed at which to align the face direction toward the target.
    public float AlignmentAngleTolerance = 1f;
    
    [Header("Duration Exit Condition")]
    public float MaxDuration = -1f;
    public float MaxDurationWithoutLoS = -1f; // If we are not keeping LoS, this is the max duration to wait before exiting.
}

public enum FollowStateOutcome
{
    Completed,
    RoleDoorFailed,
    MovementManagerError,
}

public class FollowState : GenericAbstractState<FollowStateOutcome, FollowStateConfiguration>
{
    #region Serialized Fields

    [SerializeField] private FollowStateConfiguration _config;

    #endregion

    private enum FollowStateInternalState
    {
        Stopped,
        MovingToTarget,
    }

    #region Internal Fields

    private bool isExited = false;

    private float _startTime;
    private float _lastLoSSuccessTime = 0f; // Used to track when we last had LoS, for the MaxDurationWithoutLoS exit condition.
    private FollowStateInternalState _internalState = FollowStateInternalState.Stopped;
    
    private bool isWithinDistance = false;
    private bool hasLoS = false;

    #endregion
    
    public static string ON_ROLE_DOOR_FAILED_PORT_NAME = "On DoorRoleFailed";
    [EventOutputPort("On DoorRoleFailed")]
    public event Action<List<NpcRoleSO>> OnDoorRoleFailed;
    
    public override void ConfigureState(FollowStateConfiguration configuration)
    {
        _config = configuration;
    }

    public override bool InterruptState()
    {
        CancelCurrentMovementRequest();
        return true;
    }

    private void OnEnable()
    {
        _startTime = Time.time;
        if (_config == null)
        {
            // If we have no target that we are done.
            Debug.LogWarning($"FollowState on {gameObject.name} has no follow configuration");
            TriggerExit(FollowStateOutcome.Completed);
            isExited = true;
            return;
        }
        
        
    }

    #region State Helpers

    private void CancelCurrentMovementRequest()
    {
        if (npcContext.MovementManager.HasMovementRequest)
        {
            npcContext.MovementManager.InterruptCurrentRequest();
        }
    }

    /// <summary>
    /// Ensure that all movement requests are stopped
    /// </summary>
    private void BeginStoppedState()
    {
        _internalState = FollowStateInternalState.Stopped;
        // All we have to do is make sure that we stop any previous movement requests.
        CancelCurrentMovementRequest();
    }

    /// <summary>
    /// Stops any previous requests and begins a new request to move to the target.
    /// </summary>
    private void BeginMovingToTargetState()
    {
        _internalState = FollowStateInternalState.MovingToTarget;
        // First we stop any previous movement requests.
        CancelCurrentMovementRequest();
        
        NpcMovementRequest request;
        if (_config.TargetingType == FollowStateTargetingType.Transform)
        {
            request = new NpcMovementRequest(_config.TargetTransform);
        }
        else if (_config.TargetingType == FollowStateTargetingType.Position)
        {
            request = new NpcMovementRequest(_config.TargetPosition);
        }
        else
        {
            Debug.LogError($"FollowState on {gameObject.name} has an invalid TargetingType: {_config.TargetingType}");
            TriggerExit(FollowStateOutcome.MovementManagerError);
            isExited = true;
            return;
        }
        
        request.DesiredSpeed = _config.MovementSpeed;
        request.ReplanAtTargetMoveDistance = 0.1f;
        request.RequireExactPosition = true;
        request.RequireFinalAlignment = false;
        request.ExitOnComplete = true;

        MovementFailureReason? failureReason = npcContext.MovementManager?.SetMovementTarget(request);
        if (failureReason.HasValue)
        {
            HandleMovementFailure(failureReason.Value, null);
            _internalState = FollowStateInternalState.Stopped;
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

        if (_internalState == FollowStateInternalState.Stopped)
        {
            // Nothing to do, we are already stopped.
        }
        else if (_internalState == FollowStateInternalState.MovingToTarget)
        {
            // If we are also maintaining LoS and we do not have LoS, we do nothing.
            if (_config.LoSConfiguration == FollowStateLoSConfiguration.KeepWithinLoS && !hasLoS)
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

        if (_internalState == FollowStateInternalState.Stopped)
        {
            // If we are keeping within distance, we move to MovingToTarget state.
            if (_config.DistanceConfiguration == FollowStateDistanceConfiguration.KeepWithinDistance)
            {
                BeginMovingToTargetState();
            }
            // Otherwise, we do nothing as we are not maintaining distance.
        }
        else if (_internalState == FollowStateInternalState.MovingToTarget)
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
        
        if (_internalState == FollowStateInternalState.Stopped)
        {
            // Nothing to do, we are already stopped.
        }
        else if (_internalState == FollowStateInternalState.MovingToTarget)
        {
            // If we are also maintaining distance and we are not within distance, we do nothing.
            if (_config.DistanceConfiguration == FollowStateDistanceConfiguration.KeepWithinDistance && !isWithinDistance)
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
        
        if (_internalState == FollowStateInternalState.Stopped)
        {
            // If we are keeping within LoS, we move to MovingToTarget state.
            if (_config.LoSConfiguration == FollowStateLoSConfiguration.KeepWithinLoS)
            {
                BeginMovingToTargetState();
            }
            // Otherwise, we do nothing as we are not maintaining LoS.
        }
        else if (_internalState == FollowStateInternalState.MovingToTarget)
        {
            // We are already moving to the target, so we have nothing more to do.
        }
    }
    
    private void HandleMovementFailure(MovementFailureReason reason, object failureData)
    {
        Debug.LogWarning($"FollowState on {gameObject.name} encountered a movement failure: {reason}");
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
                TriggerExit(FollowStateOutcome.MovementManagerError);
                isExited = true;
                return;
            case MovementFailureReason.LinkTraversalFailed:
                try
                {
                    List<NpcRoleSO> missingRoles = (List<NpcRoleSO>) failureData;
                    OnDoorRoleFailed?.Invoke(missingRoles);
                }
                catch (InvalidCastException)
                {
                    Debug.LogError("MoveToState: LinkTraversalFailed failureData is not a List<NpcRoleSO>");
                }
                TriggerExit(FollowStateOutcome.RoleDoorFailed);
                isExited = true;
                return;
            case MovementFailureReason.Interrupted:
                // This is expected when we are exiting the state, so we do nothing.
                return;
            default:
                // This is an unexpected failure reason, so we log an error and exit the state.
                Debug.LogWarning($"FollowState on {gameObject.name} encountered an unexpected movement failure reason: {reason}");
                return;
        }
    }

    #endregion

    /// <summary>
    /// Checks whether we are within both the horizontal and vertical distance parameters. Horizontal is computed
    /// using a distance on the xz plane, and vertical is just the absolute difference in the y coordinate.
    /// </summary>
    /// <returns></returns>
    private bool GetIsWithinDistance()
    {
        Vector3 targetPosition = _config.TargetingType == FollowStateTargetingType.Transform
            ? _config.TargetTransform.Value.position
            : _config.TargetPosition.Value;
        
        Vector3 npcPosition = npcContext.transform.position;
        
        // Calculate the horizontal distance on the xz plane
        float verticalDistance = Mathf.Abs(npcPosition.y - targetPosition.y);
        if (verticalDistance > _config.VerticalDistanceParameter)
        {
            return false; // If the vertical distance is greater than the allowed parameter, we are not within distance.
        }
        
        Vector3 difference = npcPosition - targetPosition;
        difference.y = 0;
        if (difference.sqrMagnitude > _config.HorizontalDistanceParameter * _config.HorizontalDistanceParameter)
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
        Vector3 targetPosition = _config.TargetingType == FollowStateTargetingType.Transform
            ? _config.TargetTransform.Value.position
            : _config.TargetPosition.Value;
        
        Vector3 originPoint = npcContext.transform.position;
        
        RaycastHit hitInfo;
        if (Physics.Linecast(originPoint, targetPosition, out hitInfo, _config.LoSObstacleLayerMask))
        {
            // An obstacle was hit.
            if (_config.TargetingType == FollowStateTargetingType.Transform &&
                hitInfo.transform == _config.TargetTransform.Value)
            {
                // Then what we hit was the target transform, so we have LoS.
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
    private void HandleShouldExit()
    {
        if (_config.MaxDuration > 0f && Time.time - _startTime > _config.MaxDuration)
        {
            TriggerExit(FollowStateOutcome.Completed);
            isExited = true;
            return;
        }
        
        if (_config.MaxDurationWithoutLoS > 0f && !hasLoS && Time.time - _lastLoSSuccessTime > _config.MaxDurationWithoutLoS)
        {
            TriggerExit(FollowStateOutcome.Completed);
            isExited = true;
            return;
        }

        bool distanceExitConditionMet = false;
        bool useDistanceExitCondition = false;
        switch (_config.DistanceConfiguration)
        {
            case FollowStateDistanceConfiguration.IgnoreDistance:
                distanceExitConditionMet = false;
                useDistanceExitCondition = false;
                break;
            case FollowStateDistanceConfiguration.KeepWithinDistance:
                distanceExitConditionMet = false;
                useDistanceExitCondition = false;
                break;
            case FollowStateDistanceConfiguration.ExitWhenOutsideDistance:
                distanceExitConditionMet = !isWithinDistance;
                useDistanceExitCondition = true;
                break;
            case FollowStateDistanceConfiguration.ExitWhenWithinDistance:
                distanceExitConditionMet = isWithinDistance;
                useDistanceExitCondition = true;
                break;
        }
        
        bool loSExitConditionMet = false;
        bool useLoSExitCondition = false;
        switch (_config.LoSConfiguration)
        {
            case FollowStateLoSConfiguration.IgnoreLoS:
                loSExitConditionMet = false;
                useLoSExitCondition = false;
                break;
            case FollowStateLoSConfiguration.KeepWithinLoS:
                loSExitConditionMet = false;
                useLoSExitCondition = false;
                break;
            case FollowStateLoSConfiguration.ExitWhenOutsideLoS:
                loSExitConditionMet = !hasLoS;
                useLoSExitCondition = true;
                break;
            case FollowStateLoSConfiguration.ExitWhenWithinLoS:
                loSExitConditionMet = hasLoS;
                useLoSExitCondition = true;
                break;
        }

        if (!useDistanceExitCondition && !useLoSExitCondition)
        {
            // We don't have any exit conditions to check.
            return;
        }
        else if (useDistanceExitCondition && !useLoSExitCondition)
        {
            // Then we should exit if the distance condition is met not matter what because we don't care about LoS.
            if (distanceExitConditionMet)
            {
                TriggerExit(FollowStateOutcome.Completed);
                isExited = true;
            }
        }
        else if (!useDistanceExitCondition && useLoSExitCondition)
        {
            // Then we should exit if the LoS condition is met not matter what because we don't care about distance.
            if (loSExitConditionMet)
            {
                TriggerExit(FollowStateOutcome.Completed);
                isExited = true;
            }
        }
        else
        {
            // We have both distance and LoS exit conditions to check.
            if (_config.ConditionCombination == FollowStateExitConditionCombination.AND)
            {
                if (distanceExitConditionMet && loSExitConditionMet)
                {
                    TriggerExit(FollowStateOutcome.Completed);
                    isExited = true;
                }
            }
            else
            {
                // OR condition
                if (distanceExitConditionMet || loSExitConditionMet)
                {
                    TriggerExit(FollowStateOutcome.Completed);
                    isExited = true;
                }
            }
        }
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
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _config.AlignmentAngularSpeed * Time.deltaTime
        );

        bool aligned = Quaternion.Angle(transform.rotation, targetRotation) < _config.AlignmentAngleTolerance;
        if (aligned)
        {
            // We also just snap to the target rotation
            transform.rotation = targetRotation;
        }

        return aligned;
    }
    
    private void Update()
    {
        if (isExited) return;
        
        bool newIsWithinDistance = GetIsWithinDistance();
        bool newHasLoS = GetHasLoS();
        if (newHasLoS)
        {
            _lastLoSSuccessTime = Time.time;
        }

        if (newIsWithinDistance != isWithinDistance)
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

        if (newHasLoS != hasLoS)
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

        if (_internalState == FollowStateInternalState.Stopped && _config.FaceTargetWhenStopped)
        {
            // Forward is the direction toward the target.
            Vector3 targetPosition = _config.TargetingType == FollowStateTargetingType.Transform
                ? _config.TargetTransform.Value.position
                : _config.TargetPosition.Value;
            
            Vector3 forwardTarget = targetPosition - npcContext.transform.position;
            AlignToward(forwardTarget);
        }

        HandleShouldExit();
    }

    private void Start()
    {
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

        _lastLoSSuccessTime = Time.time;
        if (GetHasLoS())
        {
            HandleLoSGained();
        }
        else
        {
            HandleLoSLost();
        }
    }

    private void OnDestroy()
    {
        if (npcContext != null && npcContext.MovementManager != null)
        {
            CancelCurrentMovementRequest();
        }
        isExited = true;
    }
}