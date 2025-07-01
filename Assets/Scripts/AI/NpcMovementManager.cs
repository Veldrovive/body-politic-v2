using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Action = System.Action;

/// <summary>
/// Defines the frame of reference for a target offset.
/// </summary>
public enum TargetOffsetFrame
{
    /// <summary>
    /// The offset is applied in world space.
    /// </summary>
    Global,
    /// <summary>
    /// The offset is applied in the target Transform's local space.
    /// </summary>
    TargetLocal
}

[BlackboardEnum]
public enum MovementSpeed
{
    Stroll,
    Walk,
    Run,
    Sprint,
    NpcSpeed
}

/// <summary>
/// Reasons why a movement command might fail within the NpcMovementManager.
/// </summary>
public enum MovementFailureReason
{
    TargetTransformNull,       // The provided target Transform is null when it's required.
    TargetPositionInvalid,     // There was no valid target position found. Likely the point was in the air.
    AgentNotOnNavMesh,         // The NavMeshAgent is not currently on a valid NavMesh.
    NoValidPathFound,          // NavMeshAgent failed to find a valid path to the target position.
    LinkTraversalFailed,       // The agent could not pass through a door.
    RequestNull,                // The movement request was null when it was expected to be valid.
    InvalidRequestParameters,
    Interrupted,
    ReplanningFailed,
}

public class NpcMovementManagerSaveableData : SaveableData
{
    // Transform
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

/// <summary>
/// A data class to configure a movement request for the NpcMovementManager.
/// </summary>
[Serializable]
public class NpcMovementRequest
{
    public Vector3? TargetPosition { get; private set; } = null;
    public Quaternion? TargetRotation { get; private set; } = null;
    public Transform TargetTransform { get; private set; } = null;
    public Vector3 TargetOffset { get; set; } = Vector3.zero;
    public TargetOffsetFrame OffsetFrame { get; set; } = TargetOffsetFrame.Global;

    public MovementSpeed DesiredSpeed { get; set; } = MovementSpeed.Walk;

    public float? ReplanAtDistance { get; set; } = null;
    public float? ReplanAtTargetMoveDistance { get; set; } = null;

    public bool RequireExactPosition { get; set; } = true;
    public bool RequireFinalAlignment { get; set; } = true;
    public bool ExitOnComplete { get; set; } = true; // If false, will continue to follow/replan for TargetTransform

    public float StoppingDistance { get; set; } = 0.1f;
    public float AlignmentAngularSpeed { get; set; } = 360f;
    public float AlignmentAngleTolerance { get; set; } = 1.0f;
    
    public float SampleRadius { get; set; } = 2.0f;

    // Sampling parameters (used if RequireExactPosition is false)
    public int NumberOfSamplePoints { get; set; } = 5;
    public float SamplePointSearchRadius { get; set; } = 1.0f;

    /// <summary>
    /// Initializes a new instance of the NpcMovementRequest class for a specific world-space position.
    /// </summary>
    /// <param name="targetPosition">The world-space position to move to.</param>
    public NpcMovementRequest(Vector3 targetPosition)
    {
        TargetPosition = targetPosition;
        TargetTransform = null; // Explicitly null for position-based requests
    }

    /// <summary>
    /// Initializes a new instance of the NpcMovementRequest class for a target Transform.
    /// </summary>
    /// <param name="targetTransform">The Transform to move towards.</param>
    public NpcMovementRequest(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            Debug.LogError("NpcMovementRequest: Constructor received a null targetTransform.");
        }
        TargetTransform = targetTransform;
        TargetPosition = null; // Not using a fixed position if a transform is provided
    }

    /// <summary>
    /// Validates the request parameters and logs warnings for common issues.
    /// </summary>
    /// <returns>True if basic validation passes, false otherwise.</returns>
    public bool IsValid()
    {
        bool isValid = true;
        if (TargetTransform == null && !TargetPosition.HasValue)
        {
            Debug.LogWarning("NpcMovementRequest: Invalid request - TargetTransform is null and TargetPosition is not set.");
            isValid = false;
        }
        // ... (rest of IsValid remains the same) ...
        if (StoppingDistance < 0)
        {
            Debug.LogWarning($"NpcMovementRequest: StoppingDistance ({StoppingDistance}) is negative. Clamping to 0.");
            StoppingDistance = 0f;
        }
        if (RequireFinalAlignment)
        {
            if (AlignmentAngularSpeed <= 0)
            {
                Debug.LogWarning($"NpcMovementRequest: AlignmentAngularSpeed ({AlignmentAngularSpeed}) is non-positive. Setting to 360.");
                AlignmentAngularSpeed = 360f;
            }
            if (AlignmentAngleTolerance < 0)
            {
                Debug.LogWarning($"NpcMovementRequest: AlignmentAngleTolerance ({AlignmentAngleTolerance}) is negative. Clamping to 0.");
                AlignmentAngleTolerance = 0f;
            }
        }
        if (!RequireExactPosition)
        {
            if (SampleRadius < 0)
            {
                Debug.LogWarning($"NpcMovementRequest: AcceptanceRadius ({SampleRadius}) is negative. Clamping to 0.");
                SampleRadius = 0f;
            }
            if (SamplePointSearchRadius <= 0)
            {
                Debug.LogWarning($"NpcMovementRequest: SamplePointSearchRadius ({SamplePointSearchRadius}) is non-positive. Setting to 0.1.");
                SamplePointSearchRadius = 0.1f;
            }
            if (NumberOfSamplePoints < 1)
            {
                Debug.LogWarning($"NpcMovementRequest: NumberOfSamplePoints ({NumberOfSamplePoints}) is less than 1. Setting to 1.");
                NumberOfSamplePoints = 1;
            }
        }
        if (ReplanAtTargetMoveDistance.HasValue && TargetTransform == null)
        {
            // Debug.LogWarning("NpcMovementRequest: ReplanAtTargetMoveDistance is set, but TargetTransform is null. This replan condition will not function.");
        }
        if (ReplanAtTargetMoveDistance.HasValue && ReplanAtTargetMoveDistance.Value <= 0)
        {
             Debug.LogWarning($"NpcMovementRequest: ReplanAtTargetMoveDistance ({ReplanAtTargetMoveDistance.Value}) is non-positive. Disabling this replan condition.");
             ReplanAtTargetMoveDistance = null;
        }
        if (ReplanAtDistance.HasValue && ReplanAtDistance.Value < 0)
        {
            Debug.LogWarning($"NpcMovementRequest: ReplanAtDistance ({ReplanAtDistance.Value}) is negative. Disabling this replan condition.");
            ReplanAtDistance = null;
        }
        return isValid;
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class NpcMovementManager : SaveableGOConsumer
{
    [Header("Movement Speeds")]
    [SerializeField] private float strollSpeed = 1.0f;
    [SerializeField] private float walkSpeed = 2.0f;
    [SerializeField] private float runSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5.0f;

    enum NpcMovementStateMachineState
    {
        WaitingForPlanning,  // We enter this state when we have a movement request but have not planned yet
        FinalAlignment,  // We enter this once we reached the destination if we are doing a final alignment
        HitLink,  // We reached an off-mesh link and next frame we should validate whether we can cross it
        AligningForLinkTraversal,  // We enter this when we have reached an off-mesh link and are aligning to the link end
        TraversingLink,  // We enter this when we are traversing an off-mesh link
        Moving,  // Standard movement state
        Stopped,  // We enter this when we are stopped
    }
    private NpcMovementStateMachineState currentState = NpcMovementStateMachineState.Stopped;

    private NpcContext npcContext;
    private NavMeshAgent navMeshAgent;
    private NpcMovementRequest currentMovementRequest;

    private Quaternion currentAlignmentTarget;
    
    private Vector3 traversalStartPositionCache;
    private Quaternion traversalStartRotationCache;
    private Vector3? traversalInitialFacingDirectionCache;
    
    private float replanAtTargetMoveDistanceSqr;
    private float lastRemainingDistance = 0f;
    private Vector3? lastTargetPosition;
    
    public bool HasMovementRequest => currentMovementRequest != null;
    
    public event Action OnRequestCompleted;
    public event Action<MovementFailureReason, object> OnRequestFailed;  // Raised when the request fails. Or when it is interrupted.

    [NonSerialized] public Vector3 velocity;  // Usually pegged to the NavMeshAgent velocity, but set manually during link traversal

    public override SaveableData GetSaveData()
    {
        NpcMovementManagerSaveableData data = new NpcMovementManagerSaveableData
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Scale = transform.localScale
        };
        return data;
    }
    
    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            if (data is not NpcMovementManagerSaveableData npcMovementData)
            {
                Debug.LogError($"NpcMovementManager: LoadSaveData called with invalid data type: {data.GetType()}. Expected NpcMovementManagerSaveableData.", this);
                return;
            }
        
            // It's recommended to disable the agent before warping
            navMeshAgent.enabled = false;

            transform.position = npcMovementData.Position;
            transform.rotation = npcMovementData.Rotation;
            transform.localScale = npcMovementData.Scale;

            // Re-enable the agent to allow Warp to correctly place it on the NavMesh
            navMeshAgent.enabled = true;

            // Warp the agent to the new position
            navMeshAgent.Warp(npcMovementData.Position);
            // If there is an active request, pass it through SetMovementTarget again to recalculate the path
            if (currentMovementRequest != null)
            {
                MovementFailureReason? failureReason = SetMovementTarget(currentMovementRequest);
                if (failureReason.HasValue)
                {
                    Debug.LogError($"NpcMovementManager: Failed to reapply movement request after loading save data: {failureReason.Value}", this);
                }
            }
        }
    }

    void Awake()
    {
        npcContext = GetComponent<NpcContext>();
        if (npcContext == null)
        {
            Debug.LogError($"NpcMovementManager: NpcContext on {gameObject.name} is null", this);
        }
        
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError($"NavMeshAgent on {gameObject.name} is null");
        }
        else
        {
            navMeshAgent.autoTraverseOffMeshLink = false;
        }
    }
    
    /// <summary>
    /// Validates the movement request and sets the target position.
    /// Returns null if the request was valid and an initial path was found.
    /// Otherwise returns the failure reason
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public MovementFailureReason? SetMovementTarget(NpcMovementRequest request)
    {
        if (request == null)
        {
            Debug.LogError("NpcMovementManager: SetMovementTarget called with a null request.", this);
            return MovementFailureReason.RequestNull;
        }

        if (navMeshAgent == null)
        {
            Debug.LogError($"NavMeshAgent on {gameObject.name} is null");
            return MovementFailureReason.AgentNotOnNavMesh;
        }

        if (!navMeshAgent.isOnNavMesh)
        {
            Debug.LogError($"NavMeshAgent on {gameObject.name} is not on navmesh");
            return MovementFailureReason.AgentNotOnNavMesh;
        }

        if (!request.IsValid())
        {
            Debug.LogError("NpcMovementManager: Movement request is invalid.", this);
            return MovementFailureReason.InvalidRequestParameters;
        }

        if (currentState != NpcMovementStateMachineState.Stopped)
        {
            InterruptCurrentRequest();
        }

        if (currentState != NpcMovementStateMachineState.Stopped || currentMovementRequest != null)
        {
            Debug.LogError("NpcMovementManager: Previous movement request was not properly cleared.", this);
        }

        if (!request.ExitOnComplete)
        {
            // This is not supported yet
            Debug.LogError("NpcMovementManager: ExitOnComplete==false is not supported yet.", this);
        }

        // Before we start, we first check if there is in fact a valid path to the target.
        Vector3? targetPosition = GetTargetPosition(request);
        if (!targetPosition.HasValue)
        {
            Debug.LogError($"NpcMovementManager: Target position is invalid: {request}", this);
            return MovementFailureReason.TargetPositionInvalid;
        }
        NavMeshPath path = new NavMeshPath();
        navMeshAgent.CalculatePath(targetPosition.Value, path);
        if (path.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError($"NpcMovementManager: Path to target position is invalid: {request}", this);
            return MovementFailureReason.NoValidPathFound;
        }

        // This request is valid and can be completed
        currentMovementRequest = request;
        
        // Cache the replan distance
        replanAtTargetMoveDistanceSqr = request.ReplanAtTargetMoveDistance.HasValue ? request.ReplanAtTargetMoveDistance.Value * request.ReplanAtTargetMoveDistance.Value : -1f;

        // Set the NavMeshAgent configuration
        SetNavAgentConfiguration(request);
        // And finally set the destination
        navMeshAgent.SetDestination(targetPosition.Value);
        // We are now in the waiting for planning state. We expect to move out of this next frame.
        currentState = NpcMovementStateMachineState.WaitingForPlanning;
        // Debug.Log("MovementStateMachineUpdate: WaitingForPlanning", this);
        
        return null;
    }

    /// <summary>
    /// This just runs the initial checks, but does not actually set the target.
    /// Checks do include an initial pathfinding.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public (bool, Vector3) CanSatisfyRequest (NpcMovementRequest request)
    {
        if (request == null)
        {
            Debug.LogError("NpcMovementManager: CanSatisfyRequest called with a null request.", this);
            return (false, Vector3.zero);
        }

        if (navMeshAgent == null)
        {
            Debug.LogError($"NavMeshAgent on {gameObject.name} is null");
            return (false, Vector3.zero);
        }

        if (!navMeshAgent.isOnNavMesh)
        {
            Debug.LogError($"NavMeshAgent on {gameObject.name} is not on navmesh");
            return (false, Vector3.zero);
        }

        if (!request.IsValid())
        {
            Debug.LogError("NpcMovementManager: Movement request is invalid.", this);
            return (false, Vector3.zero);
        }

        // Before we start, we first check if there is in fact a valid path to the target.
        Vector3? targetPosition = GetTargetPosition(request);
        if (!targetPosition.HasValue)
        {
            // Debug.LogError($"NpcMovementManager: Target position is invalid: {request}", this);
            return (false, Vector3.zero);
        }
        
        NavMeshPath path = new NavMeshPath();
        navMeshAgent.CalculatePath(targetPosition.Value, path);
        
        return (path.status != NavMeshPathStatus.PathInvalid, targetPosition.Value);
    }
    
    private void Update()
    {
        velocity = navMeshAgent.velocity;  // This may or may not get overwritten below during traversal
        
        if (currentState == NpcMovementStateMachineState.Stopped)
        {
            // Nothing to do
            return;
        }
        else if (currentState == NpcMovementStateMachineState.WaitingForPlanning)
        {
            // Check if we should go to another state
            if (navMeshAgent.pathPending)
            {
                // We are still waiting
                return;
            }
            
            // We are done so we should check if there is a valid path
            if (navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                // We don't have a path. Time to error
                EndRequestWithError(MovementFailureReason.NoValidPathFound);
                return;
            }
            
            // I don't know if this can actually happen, but we might also check if we are on a link
            if (navMeshAgent.isOnOffMeshLink)
            {
                // We should then move to the HitLink state
                // If we cannot traverse the link, it will immeidately be rejected. There might be a frame that you could
                // cancel and teleport to the other side, but who cares.
                currentState = NpcMovementStateMachineState.HitLink;
                // Debug.Log("MovementStateMachineUpdate: HitLink", this);
                // This state will cache the current position and rotation for resetting if necessary
                return;
            }
            else
            {
                // Then we have now entered the moving state
                currentState = NpcMovementStateMachineState.Moving;
                // Debug.Log("MovementStateMachineUpdate: Moving", this);
                return;
            }
        }
        else if (currentState == NpcMovementStateMachineState.FinalAlignment)
        {
            if (!currentMovementRequest.RequireFinalAlignment)
            {
                // We're already done
                EndRequestWithCompletetion();
            }

            if (currentMovementRequest.TargetTransform == null)
            {
                // We can't align with something that doesn't exist
                EndRequestWithError(MovementFailureReason.TargetTransformNull);
            }
            
            // Otherwise we are good to go
            Vector3 targetForward = currentMovementRequest.TargetTransform.forward;
            
            navMeshAgent.updateRotation = false;

            bool aligned = AlignToward(targetForward);

            if (aligned)
            {
                // Then this request is done
                EndRequestWithCompletetion();
                return;
            }
            else
            {
                // Keep going
                return;
            }
        }
        else if (currentState == NpcMovementStateMachineState.Moving)
        {
            if (HasArrivedAtDestination())
            {
                // Success, if we require final alignment, move to the final alignment state. Otherwise we are done.
                if (currentMovementRequest.RequireFinalAlignment)
                {
                    currentState = NpcMovementStateMachineState.FinalAlignment;
                    // Debug.Log("MovementStateMachineUpdate: FinalAlignment", this);
                    return;
                }
                else
                {
                    EndRequestWithCompletetion();
                    return;
                }
            }
            
            if (navMeshAgent.isOnOffMeshLink)
            {
                // We hit an off mesh link. There's a state for that
                currentState = NpcMovementStateMachineState.HitLink;
                // Debug.Log("MovementStateMachineUpdate: HitLink", this);
                // This state will cache the current position and rotation for resetting if necessary
                return;
            }
            
            if (shouldReplan())
            {
                Vector3? newDestination = GetTargetPosition(currentMovementRequest);
                if (!newDestination.HasValue)
                {
                    EndRequestWithError(MovementFailureReason.ReplanningFailed);
                    return;
                }
                navMeshAgent.SetDestination(newDestination.Value);
                // We are now in the waiting for planning state. We expect to move out of this next frame.
                currentState = NpcMovementStateMachineState.WaitingForPlanning;
                // Debug.Log("MovementStateMachineUpdate: WaitingForPlanning", this);
                return;
            }
        }
        else if (currentState == NpcMovementStateMachineState.HitLink)
        {
            OffMeshLinkData currLink = navMeshAgent.currentOffMeshLinkData;
            if (WillFailLink(currLink))
            {
                // Then we need to abort the link traversal.
                // From https://discussions.unity.com/t/mis-feature-in-navmeshagent-regarding-off-mesh-links/742068/7
                // we can do this by "Warp()"ing to the current location and then resetting the path
                navMeshAgent.Warp(transform.position);
                EndRequestWithError(MovementFailureReason.LinkTraversalFailed, GetMisingLinkRoles(currLink));  // This calls the reset path method
                return;
            }
            else
            {
                // We are then free to traverse the link
                // Here we just cache the values we need and move to the aligning for link traversal state
                traversalStartPositionCache = transform.position;
                traversalStartRotationCache = transform.rotation;
                traversalInitialFacingDirectionCache = null;
                currentState = NpcMovementStateMachineState.AligningForLinkTraversal;
                // Debug.Log("MovementStateMachineUpdate: AligningForLinkTraversal", this);
                return;
            }
        }
        else if (currentState == NpcMovementStateMachineState.AligningForLinkTraversal)
        {
            if (!navMeshAgent.isOnOffMeshLink)
            {
                Debug.LogWarning($"NpcMovementManager: We are not on an off mesh link anymore. This should not happen.", this);
                // Try to recover by going back to the moving state
                currentState = NpcMovementStateMachineState.Moving;
                // Debug.Log("MovementStateMachineUpdate: Moving", this);
                return;
            }
            
            // We want to point the agent in the direction of the link end
            OffMeshLinkData currLink = navMeshAgent.currentOffMeshLinkData;
            Vector3 linkEnd = currLink.endPos;
            Vector3 endDirection = (linkEnd - transform.position).normalized;
            Vector3 faceDirection = Vector3.ProjectOnPlane(endDirection, Vector3.up);

            traversalInitialFacingDirectionCache ??= faceDirection;  // If the cache is null, set it

            navMeshAgent.updateRotation = false; // We will manually be rotating
            bool aligned = AlignToward(faceDirection);
            
            if (aligned)
            {
                // We are aligned, so we can now traverse the link
                navMeshAgent.updateRotation = true;
                currentState = NpcMovementStateMachineState.TraversingLink;
                // Debug.Log("MovementStateMachineUpdate: TraversingLink", this);
                return;
            }
            else
            {
                // We are not aligned yet, so we need to keep going
                return;
            }
        }
        else if (currentState == NpcMovementStateMachineState.TraversingLink)
        {
            // Ensure NavMeshAgent doesn't interfere with manual movement/rotation
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;

            if (!navMeshAgent.isOnOffMeshLink)
            {
                Debug.LogWarning("NpcMovementManager: No longer on an OffMeshLink while in TraversingLink state. This might mean the link was completed externally or an error occurred. Attempting to recover.", this);
                navMeshAgent.updatePosition = true;
                navMeshAgent.updateRotation = true;
                currentState = NpcMovementStateMachineState.Moving;
                // Debug.Log("MovementStateMachineUpdate: Moving (recovered from TraversingLink anomaly)", this);
                return;
            }

            OffMeshLinkData currentLink = navMeshAgent.currentOffMeshLinkData;
            Vector3 linkEndPoint = currentLink.endPos;

            Vector3 currentPosition = transform.position;
            Vector3 directionToLinkEnd = (linkEndPoint - currentPosition);
            directionToLinkEnd.y = 0;
            float distanceToLinkEndSqr = directionToLinkEnd.sqrMagnitude;

            if (distanceToLinkEndSqr < 0.001f)
            {
                // We are already at the link end point
                directionToLinkEnd = Vector3.zero;
            }
            // Debug.Log(distanceToLinkEndSqr);

            if (directionToLinkEnd != Vector3.zero)
            {
                directionToLinkEnd.Normalize();
            }
            else // Already at the exact end point (or extremely close)
            {
                distanceToLinkEndSqr = 0f; // Force completion logic
            }
            
            float agentSpeed = GetSpeedValue(currentMovementRequest.DesiredSpeed);
            float stepMovement = agentSpeed * Time.deltaTime;

            // Check for completion: if the remaining distance is less than or equal to this frame's movement,
            // or if we are already there.
            if (distanceToLinkEndSqr <= (stepMovement * stepMovement) + 0.001f) // + epsilon for float precision
            {
                // Reached the end of the link.
                // CompleteOffMeshLink will handle warping the agent to the precise end point.
                navMeshAgent.CompleteOffMeshLink();

                // Restore NavMeshAgent's control
                navMeshAgent.updatePosition = true;
                navMeshAgent.updateRotation = true;
                
                this.velocity = Vector3.zero; // Agent will calculate its new velocity.

                // Debug.Log($"MovementStateMachineUpdate: Completed OffMeshLink. Agent is now at {transform.position}", this);

                // Determine next state based on path status and overall destination
                if (navMeshAgent.pathPending)
                {
                    currentState = NpcMovementStateMachineState.WaitingForPlanning;
                    // Debug.Log("MovementStateMachineUpdate: WaitingForPlanning (post-link, path pending)", this);
                }
                else if (navMeshAgent.hasPath && navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid)
                {
                    // Check if this link completion also means we've arrived at the final destination
                    if (HasArrivedAtDestination())
                    {
                         if (currentMovementRequest.RequireFinalAlignment)
                         {
                            currentState = NpcMovementStateMachineState.FinalAlignment;
                            // Debug.Log("MovementStateMachineUpdate: FinalAlignment (post-link, arrived at final dest)", this);
                         }
                         else
                         {
                            EndRequestWithCompletetion();
                         }
                    }
                    else // Still have more path to follow
                    {
                        currentState = NpcMovementStateMachineState.Moving;
                        // Debug.Log("MovementStateMachineUpdate: Moving (post-link, path valid)", this);
                    }
                }
                else // No path or path is invalid after link
                {
                    // Debug.LogWarning("NpcMovementManager: Path is invalid or does not exist after OffMeshLink completion. Attempting to re-plan.", this);
                    Vector3? targetPos = GetTargetPosition(currentMovementRequest);
                    if (targetPos.HasValue && navMeshAgent.SetDestination(targetPos.Value))
                    {
                        currentState = NpcMovementStateMachineState.WaitingForPlanning;
                        // Debug.Log("MovementStateMachineUpdate: WaitingForPlanning (post-link, replan needed)", this);
                    }
                    else
                    {
                        Debug.LogError("NpcMovementManager: Failed to re-plan after OffMeshLink. Ending request.", this);
                        EndRequestWithError(MovementFailureReason.ReplanningFailed); // Or a more specific error
                    }
                }
            }
            else
            {
                // Still traversing: move the agent manually
                transform.position += directionToLinkEnd * stepMovement;
                
                // Manually update rotation to face the direction of movement
                if (directionToLinkEnd != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToLinkEnd);
                    // Use agent's general angularSpeed for rotation during link traversal
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, navMeshAgent.angularSpeed * Time.deltaTime);
                }
                
                this.velocity = directionToLinkEnd * agentSpeed;
                navMeshAgent.velocity = velocity;
            }
            return; 
        }
    }

    private bool WillFailLink(OffMeshLinkData link)
    {
        if (!link.valid)
        {
            // There is no link
            return false;
        }
        
        NavMeshLink linkComponent = link.owner as NavMeshLink;
        if (linkComponent == null)
        {
            Debug.LogWarning($"NpcMovementManager: Link component is null. This should not happen.", this);
            return false;
        }
        
        GameObject linkParent = linkComponent.transform.parent.gameObject;
        RoleDoor doorComponent = linkParent.GetComponent<RoleDoor>();
        if (doorComponent == null)
        {
            // Then this isn't a role door. let them pass
            return false;
        }

        return !doorComponent.CanEnter(npcContext);
    }

    private List<NpcRoleSO> GetMisingLinkRoles(OffMeshLinkData link)
    {
        if (!link.valid)
        {
            // There is no link
            return null;
        }
        
        NavMeshLink linkComponent = link.owner as NavMeshLink;
        if (linkComponent == null)
        {
            Debug.LogWarning($"NpcMovementManager: Link component is null. This should not happen.", this);
            return null;
        }
        
        GameObject linkParent = linkComponent.transform.parent.gameObject;
        RoleDoor doorComponent = linkParent.GetComponent<RoleDoor>();
        if (doorComponent == null)
        {
            // Then this isn't a role door. let them pass
            return null;
        }

        return doorComponent.GetMissingRoles(npcContext);
    }
    
    /// <summary>
    /// Checks for both types of replanning
    /// </summary>
    /// <returns></returns>
    private bool shouldReplan()
    {
        float remainingDistance = navMeshAgent.remainingDistance;
        if (lastRemainingDistance >= currentMovementRequest.ReplanAtDistance &&
            remainingDistance < currentMovementRequest.ReplanAtDistance)
        {
            // Then we need to replan
            // Debug.Log($"NpcMovementManager: Replanning because we are within the replan distance. (Last: {lastRemainingDistance}, current: {remainingDistance})", this);
            lastRemainingDistance = remainingDistance;
            return true;
        }
        lastRemainingDistance = remainingDistance;
        
        if (currentMovementRequest.TargetTransform == null)
        {
            // We don't have a target transform, so we can't replan
            return false;
        }
        
        Vector3 targetPosition = currentMovementRequest.TargetTransform.position;
        if (!lastTargetPosition.HasValue)
        {
            // We've never set the target position. This is the initialization state and we don't need to replan here.
            lastTargetPosition = targetPosition;
            return false;
        }

        Vector3 targetPositionDelta = targetPosition - lastTargetPosition.Value;
        if (replanAtTargetMoveDistanceSqr > 0f &&
            targetPositionDelta.sqrMagnitude > replanAtTargetMoveDistanceSqr)
        {
            // Then we need to replan
            lastTargetPosition = targetPosition;
            // Debug.Log("NpcMovementManager: Replanning because the target moved.", this);
            return true;
        }
        
        // Otherwise we don't need to replan
        return false;
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
            currentMovementRequest.AlignmentAngularSpeed * Time.deltaTime
        );

        bool aligned = Quaternion.Angle(transform.rotation, targetRotation) < currentMovementRequest.AlignmentAngleTolerance;
        if (aligned)
        {
            // We also just snap to the target rotation
            transform.rotation = targetRotation;
        }

        return aligned;
    }

    /// <summary>
    /// Checks if the NavMeshAgent has arrived at its destination.
    /// </summary>
    /// <returns>True if arrived, false otherwise.</returns>
    private bool HasArrivedAtDestination()
    {
        if (navMeshAgent == null || navMeshAgent.pathPending) return false;

        // Check if we are within stopping distance
        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            // Then check if we either have no path (meaning we are at the destination and path was cleared/completed)
            // OR our velocity is negligible (we've actually stopped moving).
            if (!navMeshAgent.hasPath || navMeshAgent.velocity.sqrMagnitude < 0.01f)
            {
                return true;
            }
        }
        return false;
    }
    
    private Vector3? GetTargetPosition(NpcMovementRequest request)
    {
        if (request == null)
        {
            Debug.LogError("NpcMovementManager: GetTargetPosition called with a null request.", this);
            return null;
        }
        
        // First we find the target without regard to whether it is possible to get there
        Vector3 targetPosition;
        if (request.TargetTransform != null)
        {
            targetPosition = request.TargetTransform.position;
        }
        else if (request.TargetPosition.HasValue)
        {
            targetPosition = request.TargetPosition.Value;
        }
        else
        {
            return null;
        }
        
        // Then we apply the offset
        if (request.TargetOffset != Vector3.zero)
        {
            if (request.OffsetFrame == TargetOffsetFrame.Global)
            {
                targetPosition += request.TargetOffset;
            }
            else
            {
                if (request.TargetTransform != null)
                {
                    targetPosition += request.TargetTransform.TransformDirection(request.TargetOffset);
                }
                else
                {
                    Debug.LogWarning($"NpcMovementManager: TargetTransform is null, but TargetOffsetFrame is set to Local. Defaulting to Global.", this);
                    targetPosition += request.TargetOffset;
                }
            }
        }
        
        // Now we apply the exact versus fuzzy positioning logic
        if (request.RequireExactPosition)
        {
            // Then the target position is exactly where we want it. We just need to find the closest position
            // on the NavMesh
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, request.SamplePointSearchRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
            else
            {
                // Debug.LogWarning($"NpcMovementManager: Target position is not on the NavMesh.", this);
                return null;
            }
        }
        else
        {
            // Then we are getting a fuzzy position
            // Basically the logic here is that we want to find a position that is on the line between us and the target
            // that is closest to us and still within the acceptance radius
            Vector3 agentPosition = transform.position;
            Vector3 directionToTarget = (targetPosition - agentPosition).normalized;
            if (directionToTarget == Vector3.zero && Vector3.Distance(agentPosition, targetPosition) < 0.01f)
            {
                 directionToTarget = -transform.forward; // Sample behind or around current point
            }
            
            float currentDistanceToTarget = Vector3.Distance(agentPosition, targetPosition);
            float effectiveSampleLineLength = Mathf.Min(request.SampleRadius, currentDistanceToTarget);
            effectiveSampleLineLength = Mathf.Max(effectiveSampleLineLength, request.SamplePointSearchRadius * 0.5f); // Ensure it's not too small
            
            Vector3 sampleLineStart = targetPosition;
            Vector3 sampleLineEnd = targetPosition - directionToTarget * effectiveSampleLineLength;
            
            Vector3 bestPointFound = Vector3.zero;
            float minSqrDistanceToAgent = float.MaxValue;
            bool foundValidSamplePoint = false;
            for (int i = 0; i < request.NumberOfSamplePoints; i++)
            {
                float t = (request.NumberOfSamplePoints <= 1) ? 0f : i / (float)(request.NumberOfSamplePoints - 1);
                Vector3 sampleOrigin = Vector3.Lerp(sampleLineStart, sampleLineEnd, t);
                
                if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, request.SamplePointSearchRadius, NavMesh.AllAreas))
                {
                    float sqrDistToAgent = (agentPosition - hit.position).sqrMagnitude;
                    if (sqrDistToAgent < minSqrDistanceToAgent)
                    {
                        minSqrDistanceToAgent = sqrDistToAgent;
                        bestPointFound = hit.position;
                        foundValidSamplePoint = true;
                    }
                }
            }
            if (foundValidSamplePoint)
            {
                // We found a valid sample point, so we return it
                return bestPointFound;
            }
            else
            {
                // We didn't find a valid sample point, so we return null
                Debug.LogWarning($"NpcMovementManager: No valid sample point found.", this);
                return null;
            }
        }
    }
    
    /// <summary>
    /// Gets the speed value for a given MovementSpeed enum.
    /// </summary>
    private float GetSpeedValue(MovementSpeed speedEnum)
    {
        if (speedEnum == MovementSpeed.NpcSpeed)
        {
            if (npcContext.Speed == MovementSpeed.NpcSpeed)
            {
                // This will cause an infinite recursion, so we need to handle it
                Debug.LogWarning("NpcMovementManager: Default speed is set to Default. This will cause an infinite recursion. Defaulting to walkSpeed.", this);
                return walkSpeed;
            }
        }
        switch (speedEnum)
        {
            case MovementSpeed.Stroll: return strollSpeed;
            case MovementSpeed.Walk: return walkSpeed;
            case MovementSpeed.Run: return runSpeed;
            case MovementSpeed.Sprint: return sprintSpeed;
            case MovementSpeed.NpcSpeed: return GetSpeedValue(npcContext.Speed);
            default:
                Debug.LogWarning($"NpcMovementManager: Unknown MovementSpeed enum '{speedEnum}'. Defaulting to walkSpeed.", this);
                return GetSpeedValue(npcContext.Speed);
        }
    }

    private void SetNavAgentConfiguration(NpcMovementRequest request)
    {
        navMeshAgent.speed = GetSpeedValue(request.DesiredSpeed);
        navMeshAgent.stoppingDistance = request.StoppingDistance;
    }
    
    private void AbandonOffMeshLink()
    {
        // This is called when we are well and truly in the off-mesh link and yet we are abandoning it.
        // To do this we need to use the stored position to reset our position instead of the current position
        // like we do when we reject a link traversal in the HitLink state. We still use the same warp strategy though.
        navMeshAgent.Warp(traversalStartPositionCache);
    }
    
    public void InterruptCurrentRequest()
    {
        if (currentMovementRequest == null)
        {
            // This was probably due to a safety interrupt to ensure that nothing was running. It wasn't so we're fine.
            return;
        }
        
        EndRequestWithError(MovementFailureReason.Interrupted);
    }

    private void Cleanup()
    {
        if (currentState == NpcMovementStateMachineState.TraversingLink ||
            currentState == NpcMovementStateMachineState.AligningForLinkTraversal)
        {
            // Whoops, we are abandoning the state in the middle of a traversal
            AbandonOffMeshLink();
        }

        if (navMeshAgent.isActiveAndEnabled)
        {
            // Ensure that everything is stopped and all variables are reset
            navMeshAgent.isStopped = true;
            navMeshAgent.updateRotation = true; // give rotation power back to the agent.
            navMeshAgent.updatePosition = true;
            navMeshAgent.ResetPath();
        }
        currentMovementRequest = null;
        lastTargetPosition = null;
        lastRemainingDistance = 0f;
        traversalStartPositionCache = Vector3.zero;
        traversalStartRotationCache = Quaternion.identity;
        traversalInitialFacingDirectionCache = null;
        replanAtTargetMoveDistanceSqr = -1f;
        
        currentState = NpcMovementStateMachineState.Stopped;
        // Debug.Log("NpcMovementManager: Stopped.", this);
    }
    
    private void EndRequestWithError(MovementFailureReason reason, object failureData = null)
    {
        Cleanup();
        OnRequestFailed?.Invoke(reason, failureData);
    }
    
    private void EndRequestWithCompletetion()
    {
        Cleanup();
        OnRequestCompleted?.Invoke();
    }
}