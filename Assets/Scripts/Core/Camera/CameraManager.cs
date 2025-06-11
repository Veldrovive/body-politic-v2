using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;

public class CameraManagerSaveableData : SaveableData
{
    public float ViewCurveParam = 0f;
    public float ViewRotParam = 0.75f;
    public Vector3 FocusCenter = Vector3.zero;
    public bool AttachedToTransform = true;
}

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(Camera))] // Ensure it's attached to a camera
public class CameraManager : SaveableGOConsumer
{
    [Header("Dependencies")]
    [SerializeField] private Camera managedCamera;
    [Tooltip("Reference to the PlayerManager singleton.")]
    [SerializeField] private PlayerManager playerManager;

    [SerializeField] private Transform targetTransform;
    
    /// <summary>
    /// The view curve is a parameteric curve that defines the distance from the focused NPC, height of the camera,
    /// and height of the point the camera is looking at above the focused NPC.
    /// 
    /// </summary>
    [Header("View Curve Parameters")]
    [SerializeField] private AnimationCurve viewDistanceCurve = AnimationCurve.Linear(0f, 3f, 1f, 10f);
    [SerializeField] private AnimationCurve viewHeightCurve = AnimationCurve.Linear(0f, 5f, 1f, 10f);
    [SerializeField] private AnimationCurve viewLookAtHeightCurve = AnimationCurve.Linear(0f, 2f, 1f, 2f);
    
    [SerializeField] float focusSnapSmoothTime = 0.1f; // Time to snap to the focus center when changing focus
    [SerializeField] float curveParamSmoothTime = 0.1f; // Time to smooth the curve parameter when changing focus
    [SerializeField] float rotParamSmoothTime = 0.1f; // Time to smooth the rotation parameter when changing focus

    [Header("View Logic")]
    [SerializeField] private float maxFocusCenterOffset = 10f;  // max distance the camera focus center can be from the focused NPC
    [SerializeField] private float focusSnapDistanceThreshold = 0.01f; // Distance threshold to consider focus center snapped when changing focus
    
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
        
        if (playerManager != null)
        {
            // Subscribe AFTER PlayerManager Awake/Start have run
            playerManager.OnFocusChanged += HandleFocusChanged;

            // Query initial state
            NpcContext initialNpc = playerManager.GetFocusedNpc();
            InitializeFocus(initialNpc); // Use helper method
        }
    }

    void Awake()
    {
        managedCamera = GetComponent<Camera>();

        // Get PlayerManager instance
        if (playerManager == null)
        {
            playerManager = PlayerManager.Instance;
        }

        if (playerManager == null)
        {
            Debug.LogError("CameraManager could not find PlayerManager! Following will not work.", this);
            enabled = false;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe
        if (playerManager != null)
        {
            playerManager.OnFocusChanged -= HandleFocusChanged;
        }
    }

    /// <summary>
    /// Sets the initial NPC target based on PlayerManager.
    /// </summary>
    /// <param name="initialNpc">The initial NPC context, can be null.</param>
    private void InitializeFocus(NpcContext initialNpc)
    {
        if (initialNpc != null)
        {
            targetTransform = initialNpc.transform;
            attachedToTransform = true;
            isSnappedToTransform = true;
            targetFocusCenter = targetTransform.position;
            curFocusCenter = targetFocusCenter;
            
            var relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);
            transform.position = curFocusCenter + relPosition;
            transform.LookAt(curFocusCenter + Vector3.up * viewLookAtHeightCurve.Evaluate(curViewCurveParam), Vector3.up);
        }
        else
        {
            Debug.LogWarning("CameraManager initialized, but no initial NPC focused in PlayerManager.", this);
            // Keep camera at its initial scene position
        }
    }
    
    private void HandleFocusChanged(NpcContext prevNpcContext, NpcContext newNpcContext)
    {
        if (newNpcContext != null)
        {
            // If we are changing focus to a new NPC, we want to set the target transform to the new NPC's transform
            targetTransform = newNpcContext.transform;
            attachedToTransform = true; // We will follow the new NPC's position
            isSnappedToTransform = false;
            
            // Set the focus center to the new NPC's position
            targetFocusCenter = targetTransform.position;
        }
        else
        {
            Debug.LogWarning("CameraManager received null NpcContext in HandleFocusChanged. No change made.", this);
        }
    }

    private void UpdateCameraPose()
    {
        float lookAtHeight = viewLookAtHeightCurve.Evaluate(curViewCurveParam);
        
        // Update the focus center
        if (isSnappedToTransform || (curFocusCenter - targetFocusCenter).sqrMagnitude <= focusSnapDistanceThreshold * focusSnapDistanceThreshold)
        {
            // Then we are close enough that we should just snap to the target focus center
            curFocusCenter = targetFocusCenter;

            if (attachedToTransform)
            {
                // And we should note that we are snapped so that simple movement cannot un-snap us
                // Unsnapping will only happen when the player manually moves the focus center or changes focus to a new NPC
                isSnappedToTransform = true;
            }
        }
        else
        {
            // Movement would be jerky so we want to smoothly move the focus center towards the target focus center
            curFocusCenter = Vector3.SmoothDamp(curFocusCenter, targetFocusCenter, ref focusSnapVelocity, focusSnapSmoothTime);
        }
        
        // Update our curve and rot params
        curViewCurveParam = Mathf.SmoothDamp(curViewCurveParam, targetViewCurveParam, ref curveParamVelocity, curveParamSmoothTime);
        curViewRotParam = Mathf.SmoothDamp(curViewRotParam, targetViewRotParam, ref rotParamVelocity, rotParamSmoothTime);
        
        Vector3 relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);

        transform.position = curFocusCenter + relPosition;
        transform.LookAt(curFocusCenter + Vector3.up * lookAtHeight, Vector3.up);
    }

    private void LateUpdate()
    {
        
        var (desiredVel, desiredRot, desiredCurveDelta, snapValue) = HandleInputs();
        
        // Handle target focus center movement
        if (snapValue.HasValue)
        {
            if (snapValue.Value)
            {
                // Reattach to the target transform
                attachedToTransform = true;
            }
            else
            {
                // The player is manually moving the focus center, so we should not snap to the target focus center
                attachedToTransform = false;
                isSnappedToTransform = false; // We are no longer snapped to the target transform
            }
        }
        
        if (desiredVel != Vector3.zero)
        {
            // Desired velocity is in local space, so we need to convert it to world space
            // Get the camera's forward and right vectors, but ignore the y component for XZ plane movement.
            Vector3 cameraForward = transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize(); // Ensure the vector is normalized after altering the y component.

            Vector3 cameraRight = transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize(); // Ensure the vector is normalized.

            // Desired velocity is in local space relative to camera's XZ plane.
            // We construct the world space direction by combining the camera's XZ forward and right.
            Vector3 worldDesiredVel = cameraForward * desiredVel.z + cameraRight * desiredVel.x;

            // Then we need to move the target focus center based on the desired velocity
            targetFocusCenter += worldDesiredVel * Time.deltaTime;
        }
        
        // But also clamp the max (x, z) distance from the target transform. If we are outside the max distance,
        // be project back onto the circle of radius maxFocusCenterOffset centered at the target transform
        Vector3 offset = targetFocusCenter - targetTransform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > maxFocusCenterOffset * maxFocusCenterOffset)
        {
            // Project onto the circle of radius maxFocusCenterOffset
            offset = offset.normalized * maxFocusCenterOffset;
            targetFocusCenter = targetTransform.position + offset;  // We don't need to worry about the y value here, since we will set it later
        }
        
        if (attachedToTransform)
        {
            // Then we first need to set the focus center to the target transform position
            targetFocusCenter = targetTransform?.position ?? Vector3.zero;
        }
        // In any case, the y value of the target focus center should snap to the target transform's y value
        targetFocusCenter.y = targetTransform?.position.y ?? 0f;
        
        // Handle rotation input
        if (!Mathf.Approximately(desiredRot, 0f))
        {
            // If the player is rotating the camera, we need to update the rotation parameter
            targetViewRotParam += desiredRot * Time.deltaTime;
        }
        
        // Handle curve parameter input
        if (!Mathf.Approximately(desiredCurveDelta, 0f))
        {
            // If the player is changing the curve parameter, we need to update the curve parameter
            targetViewCurveParam += desiredCurveDelta * Time.deltaTime;
            // Clamp the curve parameter to [0, 1] range
            targetViewCurveParam = Mathf.Clamp01(targetViewCurveParam);
        }
        
        // Then we can take a step to update the camera pose based on the current state
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

    [Header("Input Handling")]
    [SerializeField] private List<KeyCode> forwardKeys = new List<KeyCode> { KeyCode.W, KeyCode.UpArrow };
    [SerializeField] private List<KeyCode> backwardKeys = new List<KeyCode> { KeyCode.S, KeyCode.DownArrow };
    [SerializeField] private List<KeyCode> leftKeys = new List<KeyCode> { KeyCode.A, KeyCode.LeftArrow };
    [SerializeField] private List<KeyCode> rightKeys = new List<KeyCode> { KeyCode.D, KeyCode.RightArrow };
    [SerializeField] private List<KeyCode> rotationLeftKeys = new List<KeyCode> { KeyCode.Q };
    [SerializeField] private List<KeyCode> rotationRightKeys = new List<KeyCode> { KeyCode.E };
    [SerializeField] private MouseButton rotationDragButton = MouseButton.Right;
    [SerializeField] private List<KeyCode> reattachToTransformKeys = new List<KeyCode> { KeyCode.F };
    
    [SerializeField] private float playerFocusMoveVel = 8f; // The speed at which the target focus center moves when using the input keys
    [SerializeField] private float playerFocusRotVel = 0.5f; // The speed at which the target focus center rotates when using the input keys
    [SerializeField] private float playerFocusRotDragSensitivity = 0.2f; // The sensitivity of the rotation drag when using the mouse button
    [SerializeField] private float scrollSensitivity = 1f; // The sensitivity of the scroll wheel for zooming in and out
    
    private bool dragButtonHeld = false; // Records if the drag button was left last frame
    private Vector3 lastMousePosition = Vector3.zero; // Records the last mouse position for drag calculations
    private (Vector3 desiredVel, float desiredRot, float desiredCurveDelta, bool? snapValue) HandleInputs()
    {
        // Movement keys (linear and rotation)
        Vector3 desiredVelocity = Vector3.zero;
        float desiredRotation = 0f;
        bool? snapValue = null;
        if (Input.anyKey)
        {
            foreach (KeyCode fkCode in forwardKeys)
            {
                if (Input.GetKey(fkCode))
                {
                    desiredVelocity += Vector3.forward;
                    snapValue = false;
                    break;
                }
            }
            
            foreach (KeyCode bkCode in backwardKeys)
            {
                if (Input.GetKey(bkCode))
                {
                    desiredVelocity += Vector3.back;
                    snapValue = false;
                    break;
                }
            }
            
            foreach (KeyCode lkCode in leftKeys)
            {
                if (Input.GetKey(lkCode))
                {
                    desiredVelocity += Vector3.left;
                    snapValue = false;
                    break;
                }
            }
            
            foreach (KeyCode rkCode in rightKeys)
            {
                if (Input.GetKey(rkCode))
                {
                    desiredVelocity += Vector3.right;
                    snapValue = false;
                    break;
                }
            }
            
            foreach (KeyCode rlCode in rotationLeftKeys)
            {
                if (Input.GetKey(rlCode))
                {
                    desiredRotation -= 1;
                    break;
                }
            }
            
            foreach (KeyCode rrCode in rotationRightKeys)
            {
                if (Input.GetKey(rrCode))
                {
                    desiredRotation += 1;
                    break;
                }
            }
            
            foreach (KeyCode reattachCode in reattachToTransformKeys)
            {
                if (Input.GetKeyDown(reattachCode))
                {
                    snapValue = true; // Indicate that we are snapping to the target transform
                }
            }
        }
        desiredVelocity *= playerFocusMoveVel;
        desiredRotation *= playerFocusRotVel;
        
        
        // Scroll wheel input for zooming
        float scrollDelta = GetScrollDelta();
        float desiredCurveDelta = -scrollDelta * scrollSensitivity;
        
        
        // Mouse drag input for rotation
        if (Input.GetMouseButton((int)rotationDragButton))
        {
            if (dragButtonHeld)
            {
                // Then a drag has occurred. We need to get the horizontal delta of the mouse movement
                Vector2 mouseDelta = Input.mousePosition - lastMousePosition;
                float rotationDelta = -mouseDelta.x * playerFocusRotDragSensitivity;
                desiredRotation += rotationDelta;
                
                lastMousePosition = Input.mousePosition; // Update the last mouse position to the current one
            }
            else
            {
                // Start dragging for next frame
                dragButtonHeld = true;
                lastMousePosition = Input.mousePosition; // Record the initial mouse position
            }
        }
        else
        {
            dragButtonHeld = false;
            lastMousePosition = Vector2.zero;
        }
        
        return (desiredVelocity, desiredRotation, desiredCurveDelta, snapValue);
    }
    
    private float GetScrollDelta()
    {
        // Returns the scroll delta for zooming in and out
        return Input.mouseScrollDelta.y;
    }

    #endregion
}