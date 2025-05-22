using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(Camera))] // Ensure it's attached to a camera
public class CameraManager : MonoBehaviour
{
    // --- State ---
    // Simplified internal state enum matching the public CameraMode
    private CameraMode currentState = CameraMode.FollowingNpc;

    // --- Dependencies ---
    [Header("Dependencies")]
    [Tooltip("Reference to the PlayerManager singleton.")]
    [SerializeField] private PlayerManager playerManager;

    // --- Following Settings ---
    [Header("Following Settings")]
    [Tooltip("The default offset from the target when following (used when LookFromTransform is null).")]
    [SerializeField] private Vector3 followOffset = new Vector3(0, 10, -3);
    [Tooltip("How quickly the camera moves to the target position (lower is faster).")]
    [SerializeField] private float smoothTime = 0.1f;

    // --- Runtime Targets ---
    private Transform currentTargetNpcTransform; // The transform of the currently focused NPC
    private Transform temporaryTargetTransform; // Target for FollowingTransform mode
    private Transform temporaryLookFromTransform; // Look from target for FollowingTransform mode

    // --- Internal State ---
    private Camera managedCamera;
    private Vector3 velocity = Vector3.zero; // Used by SmoothDamp for following
    private Coroutine modeTimerCoroutine;
    private CameraModeRequest currentRequest; // Store the active request details

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

        // Set initial state based on default
        currentState = CameraMode.FollowingNpc;
        // Initialize currentRequest with default settings
        currentRequest = new CameraModeRequest { Mode = CameraMode.FollowingNpc };
    }

    void OnDestroy()
    {
        // Unsubscribe
        if (playerManager != null)
        {
            playerManager.OnFocusChanged -= HandleFocusChanged;
        }
        StopModeTimer(); // Use helper method
    }

    /// <summary>
    /// Sets the initial NPC target based on PlayerManager.
    /// </summary>
    /// <param name="initialNpc">The initial NPC context, can be null.</param>
    private void InitializeFocus(NpcContext initialNpc)
    {
        if (initialNpc != null)
        {
            currentTargetNpcTransform = initialNpc.transform;
            // Debug.Log($"CameraManager initialized, following: {initialNpc.gameObject.name}", this);
            // Snap immediately to target on start
            transform.position = currentTargetNpcTransform.position + followOffset;
            // Ensure initial look direction is reasonable if offset is directly behind
            transform.LookAt(currentTargetNpcTransform);
        }
        else
        {
            Debug.LogWarning("CameraManager initialized, but no initial NPC focused in PlayerManager.", this);
            // Keep camera at its initial scene position
        }
    }


    /// <summary>
    /// Handles the PlayerManager's focus change event.
    /// </summary>
    private void HandleFocusChanged(NpcContext previousNpc, NpcContext newNpc)
    {
        // Debug.Log($"CameraManager received focus change to: {newNpc?.gameObject.name ?? "None"}", this);
        currentTargetNpcTransform = newNpc?.transform;

        // If a temporary mode (like FollowingTransform) is active, changing focus
        // should likely revert the camera back to following the new NPC.
        if (currentState != CameraMode.FollowingNpc)
        {
            Debug.Log($"Focus changed during temporary camera mode ({currentState}). Reverting to FollowingNpc.", this);
            RequestCameraMode(new CameraModeRequest { Mode = CameraMode.FollowingNpc }); // Revert to default
        }
        // If already FollowingNpc, LateUpdate will handle the new target automatically.
    }

    /// <summary>
    /// Main camera movement logic, executed after all Updates.
    /// </summary>
    void LateUpdate()
    {
        switch (currentState)
        {
            case CameraMode.FollowingNpc:
                if (currentTargetNpcTransform != null)
                {
                    // 1. Calculate the desired camera position based on offset and target
                    Vector3 targetPos = currentTargetNpcTransform.position + followOffset;

                    // 2. Smoothly move the camera towards the target position
                    transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

                    // 3. Rotation is NOT modified here. It keeps its current orientation.
                    //    The camera's rotation is determined by its initial setup or previous modes.
                }
                // Else: No NPC focused, camera stays put
                break;

            case CameraMode.FollowingTransform:
                 if (temporaryTargetTransform != null)
                 {
                     if (temporaryLookFromTransform != null)
                     {
                         // Mode with specific viewpoint: Position camera at the 'LookFrom' point and look at the target
                         transform.position = temporaryLookFromTransform.position; // Using LookFrom position
                         transform.LookAt(temporaryTargetTransform.position); // Orient towards target
                         velocity = Vector3.zero; // Reset velocity as we are not smoothing position in this sub-mode
                     }
                     else
                     {
                         // Standard FollowingTransform mode (no specific LookFrom): Only update position based on offset.
                         // 1. Calculate the desired camera position based on offset and target
                         Vector3 targetPos = temporaryTargetTransform.position + followOffset;

                         // 2. Smoothly move the camera towards the target position
                         transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

                         // 3. Rotation is NOT modified here. It keeps its current orientation.
                     }
                 }
                 // Else: Target transform became null? Handled partly by HandleFocusChanged. Position/Rotation stay.
                break;
        }
    }

     /// <summary>
    /// Stops the active mode timer coroutine if it exists.
    /// </summary>
    private void StopModeTimer()
    {
        if (modeTimerCoroutine != null)
        {
            StopCoroutine(modeTimerCoroutine);
            modeTimerCoroutine = null;
        }
    }

    /// <summary>
    /// External way to request a camera mode change.
    /// </summary>
    public void RequestCameraMode(CameraModeRequest request)
    {
         Debug.Log($"CameraManager received mode request: {request.Mode}, Target: {request.TargetTransform?.name ?? "N/A"}, LookFrom: {request.LookFromTransform?.name ?? "N/A"}, Duration: {request.Duration}", this);

         StopModeTimer(); // Stop previous timer

         currentRequest = request; // Store the new request details
         currentState = request.Mode; // Update the state directly

         // Apply the new mode specifics
        switch (request.Mode)
        {
            case CameraMode.FollowingNpc:
                // Target is handled by HandleFocusChanged/initialization
                temporaryTargetTransform = null;
                temporaryLookFromTransform = null;
                // Reset velocity if we were looking from a static point
                velocity = Vector3.zero;
                // Immediately try to follow current NPC if available
                if (currentTargetNpcTransform != null)
                {
                     Vector3 targetPos = currentTargetNpcTransform.position + followOffset;
                     // Could snap here or let LateUpdate handle smoothing
                     // transform.position = targetPos;
                }
                break;

            case CameraMode.FollowingTransform:
                temporaryTargetTransform = request.TargetTransform;
                temporaryLookFromTransform = request.LookFromTransform; // Store the look-from transform

                 if (temporaryTargetTransform == null)
                 {
                     Debug.LogWarning("CameraModeRequest: FollowingTransform requested but TargetTransform is null. Reverting to FollowingNpc.", this);
                     // Directly revert state without calling RequestCameraMode recursively
                     currentState = CameraMode.FollowingNpc;
                     temporaryTargetTransform = null;
                     temporaryLookFromTransform = null;
                     currentRequest = new CameraModeRequest { Mode = CameraMode.FollowingNpc }; // Reset request
                 }
                 else
                 {
                     // Start timer if duration is positive
                     if (request.Duration > 0)
                     {
                         modeTimerCoroutine = StartCoroutine(ModeTimer(request.Duration));
                     }
                 }
                break;
        }
    }

    /// <summary>
    /// Coroutine to revert to FollowingNpc after a specified duration.
    /// </summary>
    /// <param name="duration">The time in seconds to wait.</param>
    private IEnumerator ModeTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
         Debug.Log($"Camera mode timer finished ({duration}s). Reverting to FollowingNpc.", this);
         // Timer finished, revert to default state ONLY if we are still in the mode the timer was set for
         if (currentState == currentRequest.Mode && currentRequest.Duration > 0) // Check if the request that started the timer is still active
         {
            RequestCameraMode(new CameraModeRequest { Mode = CameraMode.FollowingNpc }); // Request default mode
         }
         modeTimerCoroutine = null; // Clear coroutine reference
    }
}