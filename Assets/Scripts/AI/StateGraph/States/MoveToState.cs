using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum MoveToStateTargettingType
{
    Transform,
    Position
}

[Serializable]
public class MoveToStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(MoveToState);
    
    public MoveToStateTargettingType TargettingType = MoveToStateTargettingType.Transform;  // Default to moving to a transform. This is the most common use case as it is better practically.
    public TransformReference TargetTransform = new();
    public Vector3Reference TargetPosition = new();

    public MovementSpeed DesiredSpeed = MovementSpeed.Walk;

    // High level planning configuration
    public bool RequireExactPosition = true;  // Will not try to find nearby better locations by default. Assumes the target position is possible to reach.
    public bool RequireFinalAlignment = false;  // Will not face the same direction as the target transform. Not applicable when UseTransformAsTarget=false.
    
    // Low level conditions
    public float StoppingDistance = 0.1f;
    public float AlignmentAngularSpeed = 360f;
    public float AlignmentAngleTolerance = 1.0f;
    
    // Replanning
    public float? ReplanAtDistance = null;  // If set, we will redo pathfinding when we are within this distance of the target.
    public float? ReplanAtTargetMoveDistance = 0.2f;  // We default to replanning when the target transform has moved more than this distance.
    
    // If RequireExactPosition is false, we will search on a line between the target and the agent
    public float AcceptanceRadius = 2.0f;  // The max distance from the target position we will accept as "close enough" to the target.
    public int NumberOfSamplePoints = 5;  // The number of points to sample along the line between the target and the agent.
    public float SamplePointSearchRadius = 2.0f;  // The radius around each sample point to try to find a mesh position
    // SamplePointSearchRadius should not exceed twice the height of the agent NavMesh.SamplePosition docs
    // have more information on this.
    // NOTE: For RequireExactPosition==true, this radius is always 0.5f. We assume that whatever is setting the target
    // position is placing it very near the NavMesh.

    public MoveToStateConfiguration() : base()
    {
        
    }

    public MoveToStateConfiguration(Vector3 targetPosition)
    {
        // In this case we are using a position as the target.
        TargettingType = MoveToStateTargettingType.Position;
        TargetPosition = new Vector3Reference(targetPosition);
    }

    public MoveToStateConfiguration(Vector3VariableSO targetPosition)
    {
        TargettingType = MoveToStateTargettingType.Position;
        TargetPosition = new Vector3Reference(targetPosition);
    }

    public MoveToStateConfiguration(Transform targetTransform)
    {
        // And now we are using a transform as the target.
        TargettingType = MoveToStateTargettingType.Transform;
        TargetTransform = new TransformReference(targetTransform);
    }
    
    public MoveToStateConfiguration(TransformVariableSO targetTransform)
    {
        TargettingType = MoveToStateTargettingType.Transform;
        TargetTransform = new TransformReference(targetTransform);
    }
}

public enum MoveToStateOutcome
{
    Arrived,
    TargetDestinationInvalid,
    MovementExecutionFailed,
    NavigationTimeout,
    DoorRoleFailed
}

public class MoveToState : GenericAbstractState<MoveToStateOutcome, MoveToStateConfiguration>
{
    #region Inspector Fields

    [Header("Target Selection")]
    [Tooltip("If true, the target is a transform. If false, the target is a position.")]
    [SerializeField] private MoveToStateTargettingType targettingType = MoveToStateTargettingType.Transform;
    [Tooltip("The target transform to move to. If UseTransformAsTarget is false, this is ignored.")]
    [SerializeField] private TransformReference targetTransform = new TransformReference();
    [Tooltip("The target position to move to. If UseTransformAsTarget is true, this is ignored.")]
    [SerializeField] private Vector3Reference targetPosition = new Vector3Reference(Vector3.zero);
    [Tooltip("The radius to search for a better position.")]
    [SerializeField] private float acceptanceRadius = 2.0f;

    [Tooltip("If true, we will move to this exact position. If false, we will search for a better position along the line between the target and the agent.")]
    [SerializeField] private bool requireExactPosition = true;
    [Tooltip("If true, we will face the same direction as the target transform. If false, we will not face the target transform.")]
    [SerializeField] private bool requireFinalAlignment = false;
    
    [Header("Retry Logic (For Initial Movement Request)")]
    [Tooltip("Maximum time (in seconds) allowed to make a successful movement request to NpcMovementManager before failing the state.")]
    [SerializeField] private float navigationTimeout = 5.0f;
    [Tooltip("Time interval (in seconds) between retries if an initial movement request attempt fails for a retryable reason.")]
    [SerializeField] private float retryInterval = 0.5f;

    [Header("Speed")]
    [Tooltip("The desired speed to move at.")]
    [SerializeField] private MovementSpeed desiredSpeed = MovementSpeed.Walk;
    
    [Header("Replanning")]
    [Tooltip("If true, we will replan when we are within this distance of the target.")]
    [SerializeField] private float replanAtDistance = 5f;
    [Tooltip("If the target transform moves by this amount, we will replan.")]
    [SerializeField] private float replaceTargetMoveDistance = 0.2f;
    
    [Header("Finalization")]
    [Tooltip("The distance to stop at. If the target is within this distance, we will stop moving.")]
    [SerializeField] private float stoppingDistance = 0.1f;
    [Tooltip("The angle speed to align to the target. If the target is within this angle, we will stop moving.")]
    [SerializeField] private float alignmentAngularSpeed = 360f;
    [Tooltip("The angle tolerance to align to the target. If the target is within this angle, we will stop moving.")]
    [SerializeField] private float alignmentAngleTolerance = 1.0f;
    
    [Header("Fuzzy Targeting (Used when requireExactPosition==false)")]
    [Tooltip("The number of sample points to search for a better position.")]
    [SerializeField] private int numberOfSamplePoints = 5;
    [Tooltip("The radius to search for a better position.")]
    [SerializeField] private float samplePointSearchRadius = 2.0f;
    
    #endregion
    
    #region Internal Variables

    private bool initialPlanningHasSucceeded = false;
    private bool failed = false;  // Signals to not try to plan any more
    private float startTime;
    private float lastPlanAttemptTime;

    #endregion
    
    #region Configuration

    public override void ConfigureState(MoveToStateConfiguration configuration)
    {
        // Set the configuration data
        targettingType = configuration.TargettingType;
        targetTransform.Value = configuration.TargetTransform;
        targetPosition.Value = configuration.TargetPosition;
        desiredSpeed = configuration.DesiredSpeed;
        requireExactPosition = configuration.RequireExactPosition;
        requireFinalAlignment = configuration.RequireFinalAlignment;

        stoppingDistance = configuration.StoppingDistance;
        alignmentAngularSpeed = configuration.AlignmentAngularSpeed;
        alignmentAngleTolerance = configuration.AlignmentAngleTolerance;

        replanAtDistance = configuration.ReplanAtDistance ?? 5f;
        replaceTargetMoveDistance = configuration.ReplanAtTargetMoveDistance ?? 0.2f;

        acceptanceRadius = configuration.AcceptanceRadius;
        numberOfSamplePoints = configuration.NumberOfSamplePoints;
        samplePointSearchRadius = configuration.SamplePointSearchRadius;
    }

    #endregion
    
    #region Lifecycle

    protected void OnEnable()
    {
        // Called when the state is enabled
        // We just reset the values and Update takes care of the rest
        initialPlanningHasSucceeded = false;
        failed = false;
        startTime = Time.time;
        lastPlanAttemptTime = float.MinValue;
        
        // We don't want to link up events yet as we could mistakenly get signals from the movement manager due to
        // things like having no path counting as a success.
    }
    
    protected void OnDestroy()
    {
        if (npcContext != null && npcContext.MovementManager != null)
        {
            npcContext.MovementManager.OnRequestCompleted -= HandleRequestCompleted;
            npcContext.MovementManager.OnRequestFailed -= HandleRequestFailed;
        }
    }

    private void Update()
    {
        if (failed) return;

        if (!initialPlanningHasSucceeded)
        {
            // Check if we should try to plan
            if (Time.time - lastPlanAttemptTime < retryInterval)
            {
                // We have not waited long enough to try again
                return;
            }
            lastPlanAttemptTime = Time.time;
            
            // We do a manual check to see if the target is valid here. If we are targetting a transform and that
            // transform is null, we fail the state.
            if (targettingType == MoveToStateTargettingType.Transform && targetTransform.Value == null)
            {
                // The target transform is null, so we fail the state
                TriggerExit(MoveToStateOutcome.TargetDestinationInvalid);
                failed = true;
                return;
            }
            
            // Then we are still in the initializing phase. Our job here is to keep requesting to the movement
            // manager to see if we can path to the location. If we fail and are after the timeout, we fail the state
            var (canPath, _) = npcContext.MovementManager.CanSatisfyRequest(ConstructMovementRequest());
            if (canPath)
            {
                // Then we now start the actual movement
                initialPlanningHasSucceeded = true;
                npcContext.MovementManager.SetMovementTarget(ConstructMovementRequest());
                
                // Link up the events
                if (npcContext.MovementManager != null)
                {
                    npcContext.MovementManager.OnRequestCompleted -= HandleRequestCompleted;
                    npcContext.MovementManager.OnRequestFailed -= HandleRequestFailed;
                    npcContext.MovementManager.OnRequestCompleted += HandleRequestCompleted;
                    npcContext.MovementManager.OnRequestFailed += HandleRequestFailed;
                }
            }
            else
            {
                // Check if we have timed out
                if (Time.time - startTime > navigationTimeout)
                {
                    // We have timed out, so we fail the state
                    TriggerExit(MoveToStateOutcome.NavigationTimeout);
                    failed = true;
                }
            }
        }
    }

    #endregion
    
    #region Event Handlers

    public override bool InterruptState()
    {
        npcContext.MovementManager.InterruptCurrentRequest();
        return true;
    }

    private void HandleRequestCompleted()
    {
        // This is called when the movement request is completed. This is a success state so we can just complete.
        TriggerExit(MoveToStateOutcome.Arrived);
    }
    
    private void HandleRequestFailed(MovementFailureReason reason)
    {
        // This is called when the movement request fails. This should only occur after the initial planning has
        // succeeded since it requires that SetMovementTarget has been called.
        if (initialPlanningHasSucceeded)
        {
            // We basically just need to map from MovementFailureReason to MoveToStateError
            switch (reason)
            {
                case MovementFailureReason.TargetTransformNull:
                    TriggerExit(MoveToStateOutcome.TargetDestinationInvalid);
                    break;
                case MovementFailureReason.TargetPositionInvalid:
                    TriggerExit(MoveToStateOutcome.TargetDestinationInvalid);
                    break;
                case MovementFailureReason.AgentNotOnNavMesh:
                    TriggerExit(MoveToStateOutcome.MovementExecutionFailed);
                    break;
                case MovementFailureReason.NoValidPathFound:
                    TriggerExit(MoveToStateOutcome.TargetDestinationInvalid);
                    break;
                case MovementFailureReason.LinkTraversalFailed:
                    TriggerExit(MoveToStateOutcome.DoorRoleFailed);
                    break;
                case MovementFailureReason.RequestNull:
                    TriggerExit(MoveToStateOutcome.MovementExecutionFailed);
                    break;
                case MovementFailureReason.InvalidRequestParameters:
                    TriggerExit(MoveToStateOutcome.TargetDestinationInvalid);
                    break;
                case MovementFailureReason.Interrupted:
                    // This is not an error. We just ignore it.
                    break;
                case MovementFailureReason.ReplanningFailed:
                    // This is not an error. We just ignore it.
                    break;
            }
        }
        else
        {
            Debug.LogError("MoveToState failed before planning was completed. This should not happen.");
            TriggerExit(MoveToStateOutcome.MovementExecutionFailed);
        }
    }

    #endregion
    
    #region Helpers

    /// <summary>
    /// Uses the current configuration to construct a movement request
    /// </summary>
    /// <returns></returns>
    private NpcMovementRequest ConstructMovementRequest()
    {
        NpcMovementRequest request;
        if (targettingType == MoveToStateTargettingType.Transform)
        {
            request = new NpcMovementRequest(targetTransform);
        }
        else
        {
            request = new NpcMovementRequest(targetPosition);
        }

        request.DesiredSpeed = desiredSpeed;
        request.ReplanAtDistance = replanAtDistance;
        request.ReplanAtTargetMoveDistance = replaceTargetMoveDistance;
        request.RequireExactPosition = requireExactPosition;
        request.RequireFinalAlignment = requireFinalAlignment;
        request.ExitOnComplete = true;
        request.StoppingDistance = stoppingDistance;
        request.AlignmentAngularSpeed = alignmentAngularSpeed;
        request.AlignmentAngleTolerance = alignmentAngleTolerance;
        request.SampleRadius = acceptanceRadius;
        request.NumberOfSamplePoints = numberOfSamplePoints;
        request.SamplePointSearchRadius = samplePointSearchRadius;

        return request;
    }

    #endregion
}