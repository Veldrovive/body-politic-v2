using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SequentialMoveStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(SequentialMoveState);
    
    public List<Transform> waypoints = new List<Transform>();
    
    public MovementSpeed DesiredSpeed = MovementSpeed.NpcSpeed;
    
    public bool RequireFinalAlignment = false;  // Whether we should align to the final waypoint before finishing the state.
    public float WaypointTolerance = 1f; // How close to a waypoint (except for final) before we consider it reached.
    
    public float AlignmentAngularSpeed = 360f;
}

public enum SequentialMoveStateOutcome
{
    Arrived,
    Error,
    DoorRoleFailed
}

/// <summary>
/// Moves an NPC continuously through a sequence of waypoints without stopping.
/// It monitors the distance to intermediate waypoints in its Update loop. When an intermediate
/// waypoint is reached (within tolerance), it immediately issues a new movement command for the next waypoint.
/// The NPC only comes to a full stop upon reaching the final waypoint in the sequence.
/// This state supports save/load by remembering the current waypoint index.
/// </summary>
public class SequentialMoveState : GenericAbstractState<SequentialMoveStateOutcome, SequentialMoveStateConfiguration>
{
    #region Save/Load
    
    private static readonly string CURRENT_WAYPOINT_INDEX_KEY = "SequentialMoveState.CurrentWaypointIndex";

    #endregion

    #region Configuration Fields
    
    private List<Transform> _waypoints = new();
    private MovementSpeed _desiredSpeed = MovementSpeed.NpcSpeed;
    private bool _requireFinalAlignment;
    private float _waypointTolerance;
    private float _alignmentAngularSpeed;
    
    #endregion
    
    #region Internal State

    private int _currentWaypointIndex;
    private bool _isStateActive = false; // Prevents Update logic from running before OnEnable or after completion.
    private float _waypointToleranceSqr; // Use squared distance for performance.
    
    #endregion

    #region Configuration

    public override void ConfigureState(SequentialMoveStateConfiguration configuration)
    {
        _waypoints = new List<Transform>(configuration.waypoints); // Create a copy to be safe
        _desiredSpeed = configuration.DesiredSpeed;
        _requireFinalAlignment = configuration.RequireFinalAlignment;
        _waypointTolerance = configuration.WaypointTolerance;
        _alignmentAngularSpeed = configuration.AlignmentAngularSpeed;
        
        _waypointToleranceSqr = _waypointTolerance * _waypointTolerance;
    }
    
    #endregion
    
    #region Lifecycle

    protected void OnEnable()
    {
        _isStateActive = false;
        
        // Retrieve the current waypoint from save data, defaulting to the start (0).
        _currentWaypointIndex = GetStateData<int>(CURRENT_WAYPOINT_INDEX_KEY, 0);

        if (_waypoints == null || _waypoints.Count == 0)
        {
            Debug.LogError("SequentialMoveState has no waypoints. Exiting with Error.", this);
            TriggerExit(SequentialMoveStateOutcome.Error); // Use TriggerExit directly, CompleteState is for internal use
            return;
        }

        // If the saved index is now invalid (e.g., waypoints list changed), reset to the start.
        if (_currentWaypointIndex >= _waypoints.Count)
        {
            Debug.LogWarning($"SequentialMoveState: Saved waypoint index ({_currentWaypointIndex}) is out of bounds. Resetting to 0.", this);
            _currentWaypointIndex = 0;
            SetStateData(CURRENT_WAYPOINT_INDEX_KEY, 0);
        }

        // Subscribe to movement events.
        if (npcContext?.MovementManager != null)
        {
            npcContext.MovementManager.OnRequestCompleted += HandleRequestCompleted;
            npcContext.MovementManager.OnRequestFailed += HandleRequestFailed;
        }
        
        _isStateActive = true;
        MoveToCurrentWaypoint();
    }
    
    private void Update()
    {
        if (!_isStateActive) return;
        
        // The final waypoint is handled by the OnRequestCompleted event.
        // We only check for advancing to the next waypoint in Update for intermediate ones.
        bool isLastWaypoint = (_currentWaypointIndex >= _waypoints.Count - 1);
        if (isLastWaypoint) return;

        Transform currentTarget = _waypoints[_currentWaypointIndex];
        if (currentTarget == null)
        {
            // If the current target becomes null mid-sequence, it's an error.
            Debug.LogError($"Waypoint at index {_currentWaypointIndex} is null. Aborting sequence.", this);
            CompleteState(SequentialMoveStateOutcome.Error);
            return;
        }

        // Check distance to the current intermediate waypoint
        float distanceSqr = (currentTarget.position - npcContext.transform.position).sqrMagnitude;
        if (distanceSqr <= _waypointToleranceSqr)
        {
            // We are close enough, advance to the next waypoint without stopping.
            _currentWaypointIndex++;
            SetStateData(CURRENT_WAYPOINT_INDEX_KEY, _currentWaypointIndex);
            MoveToCurrentWaypoint();
        }
    }

    protected void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks and dangling references.
        if (npcContext?.MovementManager != null)
        {
            npcContext.MovementManager.OnRequestCompleted -= HandleRequestCompleted;
            npcContext.MovementManager.OnRequestFailed -= HandleRequestFailed;
        }
    }

    public override bool InterruptState()
    {
        CompleteState(SequentialMoveStateOutcome.Error); // Use error outcome for interrupt
        npcContext.MovementManager.InterruptCurrentRequest();
        return true;
    }
    
    #endregion

    #region Event Handlers

    /// <summary>
    /// This will now only be called when the NPC successfully reaches the FINAL waypoint,
    /// because any intermediate movement requests are interrupted by the Update loop.
    /// </summary>
    private void HandleRequestCompleted()
    {
        if (_isStateActive)
        {
            // We have successfully arrived at the final destination.
            CompleteState(SequentialMoveStateOutcome.Arrived);
        }
    }

    /// <summary>
    /// Called by the NpcMovementManager when a movement request fails for any reason.
    /// This is our primary error-handling mechanism.
    /// </summary>
    private void HandleRequestFailed(MovementFailureReason reason, object failureData)
    {
        if (!_isStateActive) return;

        switch (reason)
        {
            case MovementFailureReason.LinkTraversalFailed:
                CompleteState(SequentialMoveStateOutcome.DoorRoleFailed);
                break;
            
            case MovementFailureReason.Interrupted:
                // This state was interrupted externally. We do nothing as CompleteState was already called.
                break;

            // Any other failure is considered a terminal error for the sequence.
            default:
                Debug.LogWarning($"SequentialMoveState failed with reason: {reason}", this);
                CompleteState(SequentialMoveStateOutcome.Error);
                break;
        }
    }
    
    #endregion
    
    #region Helpers

    /// <summary>
    /// Constructs and issues a movement request for the current waypoint.
    /// Calling this with a new target gracefully interrupts the previous movement request.
    /// </summary>
    private void MoveToCurrentWaypoint()
    {
        var targetTransform = _waypoints[_currentWaypointIndex];
        if (targetTransform == null)
        {
            Debug.LogError($"Waypoint at index {_currentWaypointIndex} is null. Aborting sequence.", this);
            CompleteState(SequentialMoveStateOutcome.Error);
            return;
        }
        
        var request = new NpcMovementRequest(targetTransform);
        request.DesiredSpeed = _desiredSpeed;
        request.ExitOnComplete = true; // The request is self-contained and will fire a completion/failure event.
        
        // The stopping distance should always be small. The continuous movement is achieved
        // by the Update loop switching targets, NOT by having a large stopping distance.
        request.StoppingDistance = 0.1f; 
        
        // We only perform the final, precise alignment if it's the last waypoint and configured to do so.
        bool isLastWaypoint = (_currentWaypointIndex == _waypoints.Count - 1);
        request.RequireFinalAlignment = isLastWaypoint && _requireFinalAlignment;
        request.AlignmentAngularSpeed = _alignmentAngularSpeed;
        
        npcContext.MovementManager.SetMovementTarget(request);
    }
    
    /// <summary>
    /// A centralized method to handle the termination of the state, ensuring cleanup is always performed.
    /// </summary>
    /// <param name="outcome">The outcome to exit the state with.</param>
    private void CompleteState(SequentialMoveStateOutcome outcome)
    {
        if (!_isStateActive) return; // Prevent double-execution
        _isStateActive = false;

        // Always reset the saved waypoint index to 0 when the state terminates, for any reason.
        SetStateData(CURRENT_WAYPOINT_INDEX_KEY, 0);
        
        // Unsubscribe from events immediately to prevent any further handling after completion.
        if (npcContext?.MovementManager != null)
        {
            npcContext.MovementManager.OnRequestCompleted -= HandleRequestCompleted;
            npcContext.MovementManager.OnRequestFailed -= HandleRequestFailed;
        }

        // Trigger the state machine to transition to the next state.
        TriggerExit(outcome);
    }
    
    #endregion
}