using System;
using UnityEngine;
using System.Collections;
using UnityEditor;

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(Camera))] // Ensure it's attached to a camera
public class CameraManager_v2 : MonoBehaviour
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
    [SerializeField] private float focusSnapDistanceThreshold = 0.1f; // Distance threshold to consider focus center snapped when changing focus

    private enum CameraManagerState
    {
        /// <summary>
        /// Used when shifting between target transforms or when refocusing on the target transform.
        /// In this mode, we LERP the camera position and rotation to the new camera position and rotation.
        /// When we get near enough to our target position, we switch to MovingAroundFocusCenter mode.
        /// </summary>
        ChangingFocus,
        /// <summary>
        /// Used when moving the camera around the focus center using scrolling and WASD.
        /// In this mode, we LERP the curve param and rot param so that we smoothly move on the circle around the focus center.
        /// The focusCenter is moved smoothly to ensure linear movement is smooth.
        /// </summary>
        MovingAroundFocusCenter,
    }
    [SerializeField] private CameraManagerState currentState = CameraManagerState.ChangingFocus;
    
    private float curViewCurveParam = 0f;
    private float curViewRotParam = 0f;
    
    [SerializeField] [Range(0, 1)] private float targetViewCurveParam = 0f;
    [SerializeField] [Range(0, 1)] private float targetViewRotParam = 0f;

    private Vector3 focusCenter = Vector3.zero; // The point the camera is looking at
    private bool attachedToTransform = true;  // If true, the focus center will follow the focused NPC's position
    
    // Velocities for smooth damping
    private Vector3 focusSnapVelocity = Vector3.zero;  // Used with Vector3.SmoothDamp when ChangingFocus
    private float curveParamVelocity = 0f;  // Using with Mathf.SmoothDamp for smooth transitions
    private float rotParamVelocity = 0f;  // Using with Mathf.SmoothDamp for smooth transitions
    
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
    
    void Start()
    {
        if (playerManager != null)
        {
            // Subscribe AFTER PlayerManager Awake/Start have run
            playerManager.OnFocusChanged += HandleFocusChanged;

            // Query initial state
            NpcContext initialNpc = playerManager.GetFocusedNpc();
            InitializeFocus(initialNpc); // Use helper method
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
            focusCenter = targetTransform.position;
            
            currentState = CameraManagerState.ChangingFocus;
            var relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);
            transform.position = focusCenter + relPosition;
            transform.LookAt(focusCenter + Vector3.up * viewLookAtHeightCurve.Evaluate(curViewCurveParam), Vector3.up);
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
            
            // Set the focus center to the new NPC's position
            focusCenter = targetTransform.position;

            // Reset the camera state to ChangingFocus
            currentState = CameraManagerState.ChangingFocus;
        }
        else
        {
            Debug.LogWarning("CameraManager received null NpcContext in HandleFocusChanged. No change made.", this);
        }
    }
    
    /// <summary>
    /// I don't like the logic here. The two states can be unified by smoothly moving the focus center and the params
    /// at the same time. If the focus center is stationary, then changing the parameters moves you in a circle.
    /// If the focus center is moving, you can move in a circle around it still, but it will also be smoothly moving.
    /// The focus center is always following something. When it is attached to the transform it is the transform position
    /// and while unattached it follows a player controlled position that moves jerkily based on WASD input.
    /// The player controlled position only has an x and y. The z is taken from the tracked transform still.
    /// </summary>
    /// <returns></returns>

    private Vector3 UpdateCameraPose()
    {
        float lookAtHeight = viewLookAtHeightCurve.Evaluate(curViewCurveParam);
        if (currentState == CameraManagerState.ChangingFocus)
        {
            // Then we want to LERP the camera position to the new position and rotation. Rotation is not lerped, but set directly.
            var relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);
            Vector3 desiredPosition = focusCenter + relPosition;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref focusSnapVelocity, focusSnapSmoothTime);
            // transform.LookAt(focusCenter + Vector3.up * lookAtHeight, Vector3.up);
            return desiredPosition - transform.position;
        }
        else if (currentState == CameraManagerState.MovingAroundFocusCenter)
        {
            // Then we instead LERP the curve and rotation parameters to the target parameters.
            curViewCurveParam = Mathf.SmoothDamp(curViewCurveParam, targetViewCurveParam, ref curveParamVelocity, curveParamSmoothTime);
            curViewRotParam = Mathf.SmoothDamp(curViewRotParam, targetViewRotParam, ref rotParamVelocity, rotParamSmoothTime);
            var relPosition = GetViewOffsetFromParams(curViewCurveParam, curViewRotParam);
            transform.position = focusCenter + relPosition;
            transform.LookAt(focusCenter + Vector3.up * lookAtHeight, Vector3.up);
            return Vector3.zero;
        }
        else
        {
            Debug.LogWarning("Unknown camera manager state: " + currentState);
            return Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (attachedToTransform)
        {
            // Then we first need to set the focus center to the target transform position
            focusCenter = targetTransform?.position ?? Vector3.zero;
        }
        
        // Then we can take a step to update the camera pose based on the current state
        Vector3 targetDisplacement = UpdateCameraPose();
        
        // If we are within focusSnapDistanceThreshold of the target position then we can swap to MovingAroundFocusCenter state
        if (currentState == CameraManagerState.ChangingFocus && targetDisplacement.magnitude < focusSnapDistanceThreshold)
        {
            currentState = CameraManagerState.MovingAroundFocusCenter;
            // Reset the velocities to ensure smooth movement
            focusSnapVelocity = Vector3.zero;
            curveParamVelocity = 0f;
            rotParamVelocity = 0f;
        }
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

    private void OnDrawGizmos()
    {
        // Draw if highlighted in the editor
        if (Selection.activeGameObject == gameObject)
        {
            // Draw the focus center
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(focusCenter, 0.2f);
        }
    }
}