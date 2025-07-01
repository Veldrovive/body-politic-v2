using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Move To Position", story: "[Self] moves to [Position]", category: "Action", id: "bc9ecced224d3b9941bc15c5039199d5")]
public partial class MoveToPositionAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    
    [SerializeReference] public BlackboardVariable<MoveToTransformActionError> error;
    
    [SerializeReference] public BlackboardVariable<MovementSpeed> desiredSpeed = new(MovementSpeed.NpcSpeed);
    [SerializeReference] public BlackboardVariable<bool> requireExactPosition = new(true);
    [SerializeReference] public BlackboardVariable<bool> requireFinalAlignment = new(false);
    
    [SerializeReference] public BlackboardVariable<float> replanAtDistance = new(5f);
    [SerializeReference] public BlackboardVariable<float> replaceTargetMoveDistance = new(0.2f);
    [SerializeReference] public BlackboardVariable<float> stoppingDistance = new(0.1f);
    [SerializeReference] public BlackboardVariable<float> alignmentAngularSpeed = new(360f);
    [SerializeReference] public BlackboardVariable<float> alignmentAngleTolerance = new(1.0f);
    [SerializeReference] public BlackboardVariable<float> acceptanceRadius = new(2.0f);
    [SerializeReference] public BlackboardVariable<float> navigationTimeout = new(5.0f);
    [SerializeReference] public BlackboardVariable<float> retryInterval = new(0.5f);
    [SerializeReference] public BlackboardVariable<int> numberOfSamplePoints = new(5);
    [SerializeReference] public BlackboardVariable<float> samplePointSearchRadius = new(2.0f);
    
    private NpcContext npcContext;
    private bool initialPlanningHasSucceeded = false;
    private bool failed = false;
    private bool arrived = false;
    private float lastPlanAttemptTime;
    private float elapsedTime;

    protected override Status OnLoad()
    {
        // Debug.Log("Starting MoveToPositionAction for " + Self.Value.name);
        if (!Self.Value.TryGetComponent(out npcContext))
        {
            Debug.LogError("MoveToTransformAction: Self does not have a NpcContext component.");
            error.Value = MoveToTransformActionError.Error;
            return Status.Failure;
        }
        
        initialPlanningHasSucceeded = false;
        failed = false;
        arrived = false;
        lastPlanAttemptTime = float.MinValue;
        elapsedTime = 0;
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
        
        if (failed) return Status.Failure;

        if (arrived) return Status.Success;
        
        if (!initialPlanningHasSucceeded)
        {
            // Check if we should try to plan
            if (elapsedTime - lastPlanAttemptTime < retryInterval)
            {
                // We have not waited long enough to try again
                return Status.Running;
            }
            lastPlanAttemptTime = elapsedTime;
            
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
                return Status.Running;
            }
            else
            {
                // Check if we have timed out
                if (elapsedTime > navigationTimeout)
                {
                    error.Value = MoveToTransformActionError.Error;
                    failed = true;
                    return Status.Failure;
                }
            }
        }

        elapsedTime += Time.deltaTime;
        return Status.Running;
    }
    
    private void HandleRequestCompleted()
    {
        // This is called when the movement request is completed. This is a success state so we can just complete.
        arrived = true;
    }
    
    private void HandleRequestFailed(MovementFailureReason reason, object failureData)
    {
        // This is called when the movement request fails. This should only occur after the initial planning has
        // succeeded since it requires that SetMovementTarget has been called.
        if (initialPlanningHasSucceeded)
        {
            // We basically just need to map from MovementFailureReason to MoveToStateError
            switch (reason)
            {
                case MovementFailureReason.TargetTransformNull:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
                    break;
                case MovementFailureReason.TargetPositionInvalid:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
                    break;
                case MovementFailureReason.AgentNotOnNavMesh:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
                    break;
                case MovementFailureReason.NoValidPathFound:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
                    break;
                case MovementFailureReason.LinkTraversalFailed:
                    // try
                    // {
                    //     List<NpcRoleSO> missingRoles = (List<NpcRoleSO>) failureData;
                    //     OnDoorRoleFailed?.Invoke(missingRoles);
                    // }
                    // catch (InvalidCastException)
                    // {
                    //     Debug.LogError("MoveToState: LinkTraversalFailed failureData is not a List<NpcRoleSO>");
                    // }
                    // TriggerExit(MoveToStateOutcome.DoorRoleFailed);
                    failed = true;
                    error.Value = MoveToTransformActionError.DoorRoleFailed;
                    break;
                case MovementFailureReason.RequestNull:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
                    break;
                case MovementFailureReason.InvalidRequestParameters:
                    // TriggerExit(MoveToStateOutcome.Error);
                    failed = true;
                    error.Value = MoveToTransformActionError.Error;
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
            // TriggerExit(MoveToStateOutcome.Error);
            failed = true;
            error.Value = MoveToTransformActionError.Error;
        }
    }
    
    /// <summary>
    /// Uses the current configuration to construct a movement request
    /// </summary>
    /// <returns></returns>
    private NpcMovementRequest ConstructMovementRequest()
    {
        var request = new NpcMovementRequest(Position);

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

    protected override void OnEnd()
    {
        base.OnEnd();
        
        // Debug.Log("Stopping MoveToPositionAction for " + Self.Value.name);
        npcContext.MovementManager.InterruptCurrentRequest();
    }
}

