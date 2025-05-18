using UnityEngine;
using System;

/// <summary>
/// Defines the different modes the main camera can operate in.
/// </summary>
public enum CameraMode
{
    FollowingNpc,       // Standard mode, follows the PlayerManager's focused NPC
    FollowingTransform, // Temporarily follows a specific Transform, potentially looking from another Transform
}

/// <summary>
/// Data structure for requesting a temporary change in camera behavior.
/// </summary>
[Serializable] // Make it viewable in the Inspector if used with UnityEvents
public class CameraModeRequest
{
    [Tooltip("The mode to switch the camera to.")]
    public CameraMode Mode = CameraMode.FollowingNpc;

    [Tooltip("The primary target Transform for FollowingTransform mode. Can be null if Mode is FollowingNpc.")]
    public Transform TargetTransform = null;

    [Tooltip("Optional Transform defining where the camera should position itself when looking at the TargetTransform. If null, uses standard follow offset.")]
    public Transform LookFromTransform = null;

    [Tooltip("How long (in seconds) to stay in this mode before potentially reverting. <= 0 means indefinite until another request or focus change.")]
    public float Duration = 0f;

    // Optional: Add camera parameters like custom offset, field of view, smoothing, etc. for FOLLOWING modes if LookFromTransform is null
    // public Vector3? CustomOffset = null; // Use nullable Vector3 to signify if override is active
    // public float? CustomSmoothTime = null;

    /// <summary>
    /// Gets the effective target position. Returns Vector3.zero if TargetTransform is null.
    /// </summary>
    public Vector3 GetTargetPosition()
    {
        return TargetTransform != null ? TargetTransform.position : Vector3.zero;
    }

    /// <summary>
    /// Gets the effective look-from position. Returns Vector3.zero if LookFromTransform is null.
    /// </summary>
    public Vector3 GetLookFromPosition() // Method name kept, but logic uses LookFromTransform
    {
        return LookFromTransform != null ? LookFromTransform.position : Vector3.zero;
    }
}