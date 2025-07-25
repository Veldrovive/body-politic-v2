using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum CameraMode
{
    Orbital,
    FixedFollow
}

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(Camera))] // Ensure it's attached to a camera
public class CameraManager_v2 : SaveableGOConsumer, DefaultInputActions.ICameraControlActions
{
    [Header("General Settings")]
    [SerializeField] private CameraMode currentMode = CameraMode.Orbital;
    
    [Header("Dependencies")]
    [SerializeField] private Camera managedCamera;
    
    [Header("Fixed Follow Mode")]
    [SerializeField] private Transform fixedFollowTarget; // The transform to attach to in Fixed Follow mode
    [SerializeField] private Vector3 fixedFollowOffset = new Vector3(0, 0, 0); // Position offset from the
    private Quaternion curRotation = Quaternion.identity;
    private Quaternion targetRotation = Quaternion.identity;
    
    /// <summary>
    /// The view curve is a parameteric curve that defines the distance from the focused NPC, height of the camera,
    /// and height of the point the camera is looking at above the focused NPC.
    /// 
    /// </summary>
    [Header("Orbit Mode")]
    [SerializeField] private Transform orbitTarget;
    
    [SerializeField] private AnimationCurve viewDistanceCurve = AnimationCurve.Linear(0f, 3f, 1f, 10f);
    [SerializeField] private AnimationCurve viewHeightCurve = AnimationCurve.Linear(0f, 5f, 1f, 10f);
    [SerializeField] private AnimationCurve viewLookAtHeightCurve = AnimationCurve.Linear(0f, 2f, 1f, 2f);
    
    [SerializeField] float focusSnapSmoothTime = 0.1f; // Time to snap to the focus center when changing focus
    [SerializeField] float curveParamSmoothTime = 0.1f; // Time to smooth the curve parameter when changing focus
    [SerializeField] float rotParamSmoothTime = 0.1f; // Time to smooth the rotation parameter when changing focus

    [Header("View Logic")]
    [SerializeField] private float maxFocusCenterOffset = 10f;  // max distance the camera focus center can be from the focused NPC
    [SerializeField] private float focusSnapDistanceThreshold = 0.01f; // Distance threshold to consider focus center snapped when changing focus
    
    [Header("Input Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float rotateSpeed = 0.5f;
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float rotateDragSensitivity = 0.1f;
    
    // --- Input State Variables ---
    private Vector3 _desiredVel;
    private float _desiredRot;
    private float _desiredCurveDelta;
    private bool? _snapValue;
    
    private float curViewCurveParam = 0f;
    private float curViewRotParam = 0.75f;
    private Vector3 curFocusCenter = Vector3.zero;
    
    private float targetViewCurveParam = 0f;
    private float targetViewRotParam = 0.75f;

    private Vector3 targetFocusCenter = Vector3.zero; // The point the camera is looking at
    private bool attachedToTransform = true;  // If true, the focus center will follow the focused NPC's position
    private bool isSnappedToTransform = false;
    
    // Velocities for smooth damping
    private Vector3 focusSnapVelocity = Vector3.zero;  // Used with Vector3.SmoothDamp when ChangingFocus
    private float curveParamVelocity = 0f;  // Using with Mathf.SmoothDamp for smooth transitions
    private float rotParamVelocity = 0f;  // Using with Mathf.SmoothDamp for smooth transitions

    public override SaveableData GetSaveData()
    {
        CameraManagerSaveableData data = new CameraManagerSaveableData
        {
            Mode = currentMode,
            ViewCurveParam = curViewCurveParam,
            ViewRotParam = curViewRotParam,
            FocusCenter = curFocusCenter,
            AttachedToTransform = attachedToTransform
        };
        return data;
    }
    
    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            if (data is CameraManagerSaveableData cameraData)
            {
                currentMode = cameraData.Mode;
                targetViewCurveParam = cameraData.ViewCurveParam;
                curViewCurveParam = cameraData.ViewCurveParam;
                targetViewRotParam = cameraData.ViewRotParam;
                curViewRotParam = cameraData.ViewRotParam;
                targetFocusCenter = cameraData.FocusCenter;
                curFocusCenter = cameraData.FocusCenter;
                attachedToTransform = cameraData.AttachedToTransform;
                isSnappedToTransform = attachedToTransform; // If we are attached to the transform, we are snapped to it
            }
            else
            {
                Debug.LogError("CameraManager received invalid save data!", this);
            }
        }
    }

    void Awake()
    {
        managedCamera = GetComponent<Camera>();
        
        // Add this object as an event handler for the camera control actions
        GameManager.InputActions.CameraControl.SetCallbacks(this);
    }
    
    /// <summary>
    /// Sets the camera's operational mode and relevant targets.
    /// </summary>
    /// <param name="newMode">The mode to switch to (Orbital or FixedFollow).</param>
    /// <param name="orbitalTarget">The transform to orbit around (used in Orbital mode).</param>
    /// <param name="fixedTarget">The transform to attach to (used in FixedFollow mode).</param>
    public void SetCameraMode(CameraMode newMode, Transform orbitalTarget = null, Transform fixedTarget = null, bool immediate = false)
    {
        currentMode = newMode;
        // By setting attached=true and snapped=false, we tell the system to
        // start smoothly moving towards its new target on the next frame.
        attachedToTransform = true;

        switch (currentMode)
        {
            case CameraMode.Orbital:
                if (orbitalTarget != null)
                {
                    this.orbitTarget = orbitalTarget;
                    // Set the initial target to start the transition
                    this.targetFocusCenter = orbitalTarget.position;
                }
                else { Debug.LogWarning("Switched to Orbital mode but no orbitalTarget was provided.", this); }
                break;
            
            case CameraMode.FixedFollow:
                if (fixedTarget != null)
                {
                    this.fixedFollowTarget = fixedTarget;
                    // Set the initial targets to start the transition
                    Vector3 worldOffset = fixedFollowTarget.TransformDirection(fixedFollowOffset);
                    this.targetFocusCenter = fixedFollowTarget.position + worldOffset;
                    this.targetRotation = fixedFollowTarget.rotation;
                }
                else { Debug.LogWarning("Switched to FixedFollow mode but no fixedTarget was provided.", this); }
                break;
        }
        
        if (immediate)
        {
            // By setting isSnappedToTransform = true, we tell UpdateCameraPose to use the "SNAP LOGIC" path.
            isSnappedToTransform = true;
            // We then call UpdateCameraPose immediately to apply the snap, bypassing the normal LateUpdate loop for this frame.
            UpdateCameraPose(); 
        }
        else
        {
            // This is the original behavior: trigger a smooth transition on the next LateUpdate.
            isSnappedToTransform = false;
        }
    }

    private void UpdateCameraPose()
    {
        // --- 1. Perform Smoothing ---

        // Check if we are close enough to snap, or if we are already snapped
        bool positionIsClose = (curFocusCenter - targetFocusCenter).sqrMagnitude <= focusSnapDistanceThreshold * focusSnapDistanceThreshold;
        if (isSnappedToTransform || (attachedToTransform && positionIsClose))
        {
            // Snap current values directly to their targets
            curFocusCenter = targetFocusCenter;
            curRotation = targetRotation;
            isSnappedToTransform = true; // We are now fully snapped
        }
        else
        {
            // Smoothly transition current values towards their targets
            curFocusCenter = Vector3.SmoothDamp(curFocusCenter, targetFocusCenter, ref focusSnapVelocity, focusSnapSmoothTime);
            // Use Slerp for smooth quaternion rotation. Slerp is often better than damping euler angles.
            curRotation = Quaternion.Slerp(curRotation, targetRotation, 1 - Mathf.Exp(-Time.deltaTime / focusSnapSmoothTime));
        }

        // --- 2. Apply Pose Based on Mode ---

        switch (currentMode)
        {
            case CameraMode.Orbital:
                // Orbital mode uses additional smoothed parameters for its specific behavior
                curViewCurveParam = Mathf.SmoothDamp(curViewCurveParam, targetViewCurveParam, ref curveParamVelocity, curveParamSmoothTime);
                curViewRotParam = Mathf.SmoothDamp(curViewRotParam, targetViewRotParam, ref rotParamVelocity, rotParamSmoothTime);

                // Calculate position and look-at based on the focus center and orbital params
                float lookAtHeight = viewLookAtHeightCurve.Evaluate(curViewCurveParam);
                Vector3 relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);

                // In Orbital mode, 'curFocusCenter' is the point we orbit and look at
                transform.position = curFocusCenter + relPosition;
                transform.LookAt(curFocusCenter + Vector3.up * lookAtHeight, Vector3.up);
                break;

            case CameraMode.FixedFollow:
                // In Fixed Follow mode, 'curFocusCenter' is the camera's final world position
                transform.position = curFocusCenter;
                transform.rotation = curRotation;
                break;
        }
    }

    private void LateUpdate()
    {
        if (_snapValue.HasValue)
        {
            // Re-attach to the target for either mode
            attachedToTransform = _snapValue.Value;
            if (attachedToTransform)
            {
                isSnappedToTransform = false; // Force a smooth transition back to the target
            }
            _snapValue = null; // Reset the snap value after applying
        }

        // --- 2. Determine Targets Based on Mode ---
        switch (currentMode)
        {
            case CameraMode.Orbital:
                if (orbitTarget == null) return;

                // Handle manual movement (which detaches the camera)
                if (_desiredVel != Vector3.zero)
                {
                    attachedToTransform = false;
                    isSnappedToTransform = false;

                    Vector3 cameraForward = transform.forward;
                    cameraForward.y = 0;
                    cameraForward.Normalize();
                    Vector3 cameraRight = transform.right;
                    cameraRight.y = 0;
                    cameraRight.Normalize();
                    Vector3 worldDesiredVel = (cameraForward * _desiredVel.z + cameraRight * _desiredVel.x);
                    targetFocusCenter += worldDesiredVel * Time.deltaTime;
                }

                // Clamp focus center to max distance from the orbital target
                Vector3 offset = targetFocusCenter - orbitTarget.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > maxFocusCenterOffset * maxFocusCenterOffset)
                {
                    offset = offset.normalized * maxFocusCenterOffset;
                    targetFocusCenter = new Vector3(orbitTarget.position.x + offset.x, targetFocusCenter.y, orbitTarget.position.z + offset.z);
                }

                // If attached, target follows the transform
                if (attachedToTransform)
                {
                    targetFocusCenter = orbitTarget.position;
                }
                targetFocusCenter.y = orbitTarget.position.y; // Always match target's height

                // Handle orbital-specific inputs
                targetViewRotParam += _desiredRot * Time.deltaTime;
                targetViewCurveParam = Mathf.Clamp01(targetViewCurveParam + _desiredCurveDelta * Time.deltaTime);
                break;

            case CameraMode.FixedFollow:
                if (fixedFollowTarget == null) return;
                
                // In this mode, the "focus center" is the camera's actual desired position.
                Vector3 worldOffset = fixedFollowTarget.TransformDirection(fixedFollowOffset);
                targetFocusCenter = fixedFollowTarget.position + worldOffset;
                
                // And the target rotation is the target's rotation
                targetRotation = fixedFollowTarget.rotation;
                break;
        }

        // --- 3. Update the camera's pose using the determined targets ---
        UpdateCameraPose();
    }
    
    private Vector3 GetViewOffsetFromParams(float curveParam, float rotParam)
    {
        // Calculates the camera position and rotation relative to the focusCenter
        // Since rotation is with reference to world parameters, it will not change with the focusCenter
        // The position is a world space offset from the focusCenter based on the view curves
        
        // First, we need to calculate the direction that the camera will be positioned in using the rotation param
        Vector3 direction = new Vector3(Mathf.Cos(rotParam * Mathf.PI * 2f), 0f, Mathf.Sin(rotParam * Mathf.PI * 2f));
        // Then we can calculate the view distance and height based on the curve param
        float viewDistance = viewDistanceCurve.Evaluate(curveParam);
        float viewHeight = viewHeightCurve.Evaluate(curveParam);
        // This gives us the camera position relative to the focus center
        Vector3 cameraPosition = direction * viewDistance + Vector3.up * viewHeight;
        
        return cameraPosition;
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw if highlighted in the editor
        if (Selection.activeGameObject == gameObject)
        {
            // Draw the focus center
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetFocusCenter, 0.2f);
            Gizmos.DrawSphere(curFocusCenter, 0.2f);
        }
    }
#endif

    #region Input Handling

    private void ComputeDesiredVelocity()
    {
        _desiredVel = Vector3.zero;
        if (_isForwardPressed)
        {
            _desiredVel.z += moveSpeed;
        }

        if (_isBackwardPressed)
        {
            _desiredVel.z -= moveSpeed;
        }

        if (_isRightPressed)
        {
            _desiredVel.x += moveSpeed;
        }
        
        if (_isLeftPressed)
        {
            _desiredVel.x -= moveSpeed;
        }
        
        // Debug.Log($"Computed desired velocity: {_desiredVel}");
    }

    private void ComputeDesiredRotation()
    {
        _desiredRot = 0f;
        if (_isRotateLeftButtonPressed)
        {
            _desiredRot -= rotateSpeed;
        }

        if (_isRotateRightButtonPressed)
        {
            _desiredRot += rotateSpeed;
        }

        // If we have a drag delta, apply it to the rotation
        if (_rotateDragDelta != 0f)
        {
            _desiredRot -= _rotateDragDelta * rotateDragSensitivity;
        }
        
        // Debug.Log($"Computed desired rotation: {_desiredRot}");
    }

    private void ComputeDesiredZoom()
    {
        _desiredCurveDelta = 0f;
        if (_scrollDelta != 0f)
        {
            _desiredCurveDelta = -_scrollDelta * zoomSpeed; // Negative because scroll up zooms in
        }
        
        // Debug.Log($"Computed desired curve delta: {_desiredCurveDelta}");
    }
    
    private bool _isForwardPressed = false;
    public void OnCameraForward(InputAction.CallbackContext context)
    {
        if (context.performed) _isForwardPressed = true;
        if (context.canceled) _isForwardPressed = false;
        ComputeDesiredVelocity();
    }

    private bool _isBackwardPressed = false;
    public void OnCameraBackward(InputAction.CallbackContext context)
    {
        if (context.performed) _isBackwardPressed = true;
        if (context.canceled) _isBackwardPressed = false;
        ComputeDesiredVelocity();
    }

    private bool _isLeftPressed = false;
    public void OnCameraLeft(InputAction.CallbackContext context)
    {
        if (context.performed) _isLeftPressed = true;
        if (context.canceled) _isLeftPressed = false;
        ComputeDesiredVelocity();
    }
    
    private bool _isRightPressed = false;
    public void OnCameraRight(InputAction.CallbackContext context)
    {
        if (context.performed) _isRightPressed = true;
        if (context.canceled) _isRightPressed = false;
        ComputeDesiredVelocity();
    }
    
    private bool _isRotateLeftButtonPressed = false;
    public void OnCameraRotateLeft(InputAction.CallbackContext context)
    {
        if (context.performed) _isRotateLeftButtonPressed = true;
        if (context.canceled) _isRotateLeftButtonPressed = false;
        ComputeDesiredRotation();
    }

    private bool _isRotateRightButtonPressed = false;
    public void OnCameraRotateRight(InputAction.CallbackContext context)
    {
        if (context.performed) _isRotateRightButtonPressed = true;
        if (context.canceled) _isRotateRightButtonPressed = false;
        ComputeDesiredRotation();
    }
    
    public void OnCameraAttachToFocus(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Toggle the attached state
            attachedToTransform = !attachedToTransform;
            isSnappedToTransform = false; // Force a smooth transition back to the target
            _snapValue = attachedToTransform; // Set the snap value to the new state
        }
    }

    private float _rotateDragDelta = 0f;
    public void OnCameraRotateDrag(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _rotateDragDelta = context.ReadValue<float>();
        }
        else if (context.canceled)
        {
            _rotateDragDelta = 0f; // Reset the drag delta when the action is released
        }
        ComputeDesiredRotation();
    }

    private float _scrollDelta = 0f;
    public void OnCameraZoom(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _scrollDelta = context.ReadValue<float>();
        }
        else if (context.canceled)
        {
            _scrollDelta = 0f; // Reset the scroll delta when the action is released
        }
        ComputeDesiredZoom();
    }

    #endregion
}