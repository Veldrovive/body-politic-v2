using UnityEngine;

/// <summary>
/// Listens for CameraModeRequest events on a ScriptableObject channel
/// and calls the RequestCameraMode method on the CameraManager component
/// attached to the same GameObject.
/// </summary>
[RequireComponent(typeof(CameraManager))] // Ensures CameraManager is present
public class CameraModeRequestListener : GameEventListenerBase<CameraModeRequest, CameraModeRequestEventSO>
{
    // Cached reference to the CameraManager on this GameObject.
    private CameraManager cameraManager;

    // No UnityEvent field is needed here.

    /// <summary>
    /// Called when the script instance is being loaded.
    /// Caches the required CameraManager component.
    /// </summary>
    void Awake() // Changed from OnEnable for component caching before Start
    {
        // Cache the CameraManager component on this GameObject.
        cameraManager = GetComponent<CameraManager>();
        if (cameraManager == null)
        {
            Debug.LogError("CameraManager component not found on this GameObject. Cannot process camera mode requests.", this);
        }
    }

    /// <summary>
    /// Finds the default event channel if necessary and calls the base Start method
    /// to register the listener *after* potential event assignment.
    /// </summary>
    protected override void Start()
    {
        // If the game event is null in the inspector, try to find it on a GlobalData instance.
        if (gameEvent == null && GlobalData.Instance != null) // Ensure GlobalData exists
        {

            // Example: Assume GlobalData has a reference to the default CameraModeRequestEventSO
            CameraModeRequestEventSO defaultEvent = GlobalData.Instance.CameraModeRequestEvent; // Replace with your actual field/property
            if (defaultEvent != null)
            {
                gameEvent = defaultEvent;
                // Debug.Log($"CameraModeRequest Event SO automatically assigned from GlobalData for {this.gameObject.name}.", this);
            }
            else
            {
                // Log an error only if the manager exists, otherwise the Awake error is sufficient.
                if (cameraManager != null)
                {
                     Debug.LogWarning($"CameraModeRequest Event SO is not assigned in the inspector for {this.gameObject.name} and could not be found via GlobalData (path needs configuration). Listener inactive.", this);
                }
            }
        }
        else if (gameEvent == null)
        {
             // Log an error only if the manager exists, otherwise the Awake error is sufficient.
            if (cameraManager != null)
            {
                Debug.LogError($"CameraModeRequest Event SO is not assigned in the inspector for {this.gameObject.name}. Listener inactive.", this);
            }
        }

        base.Start(); // Actually register the listener with the event channel
    }

     /// <summary>
    /// Handles the event raised by the CameraModeRequestEventSO channel.
    /// Calls RequestCameraMode on the associated CameraManager.
    /// </summary>
    /// <param name="request">The CameraModeRequest payload received from the event channel.</param>
    protected override void HandleEventRaised(CameraModeRequest request)
    {
        // If CameraManager is missing, we cannot proceed. Error logged in Awake.
        if (cameraManager == null)
        {
            return;
        }

        // Directly call the RequestCameraMode method on the CameraManager.
        // Debug.Log($"CameraModeRequestListener on {gameObject.name} received event, calling RequestCameraMode.", this); // Optional debug
        cameraManager.RequestCameraMode(request);
    }
}