using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;

    /// <summary>
    /// Standard Unity function called when the script instance is being loaded.
    /// Caches the main camera.
    /// </summary>
    void Start()
    {
        // Cache the camera reference
        mainCamera = Camera.main;
    }

    /// <summary>
    /// Standard Unity function called after all Update functions have been called.
    /// This is recommended for camera updates or adjustments like billboarding.
    /// </summary>
    void LateUpdate()
    {
        // Ensure the camera reference is valid
        if (mainCamera == null) return;

        // Make this object's forward direction point away from the camera
        // transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward, mainCamera.transform.rotation * Vector3.up);

        // Alternative: Simpler rotation, just matches camera rotation (good for orthographic or if you don't want perspective tilt)
        transform.rotation = mainCamera.transform.rotation;
    }
}