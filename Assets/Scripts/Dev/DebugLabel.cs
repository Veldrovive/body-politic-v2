// Indicate that we are providing the full file content.
// Full File
using UnityEngine;
#if UNITY_EDITOR // Only compile this using statement in the editor
using UnityEditor;
#endif

/// <summary>
/// Draws gizmos and labels in the Scene view for easier identification and debugging.
/// Can show the object's name and local coordinate axes.
/// </summary>
public class DebugLabel : MonoBehaviour
{
    [Header("Sphere Gizmo")]
    [Tooltip("Color of the Gizmo sphere when the object is NOT selected.")]
    public Color gizmoColor = Color.yellow;

    [Tooltip("Radius of the sphere drawn when the object is NOT selected.")]
    public float gizmoRadius = 0.5f;

    [Tooltip("Color of the Gizmo sphere when the object IS selected.")]
    public Color selectedGizmoColor = new Color(1f, 0.6f, 0.1f, 1f); // Orange-ish

    [Tooltip("Radius of the wire sphere drawn when the object IS selected.")]
    public float selectedGizmoRadius = 0.7f;

    [Header("Label Display")]
    // [Tooltip("Display the object's name as a label?")] // Kept commented out as in original
    // public bool showLabel = true;

    [Tooltip("Multiplier for the label's vertical offset from the gizmo center.")]
    public float labelOffsetMultiplier = 2.0f;

    [Tooltip("Always display the label, even when not selected?")]
    public bool alwaysShowLabel = false;

    [Header("Coordinate Axes")]
    [Tooltip("Show local X (red), Y (green), Z (blue) axes originating from the object's pivot?")]
    public bool showAxes = false; // The new boolean flag

    [Tooltip("Length of the coordinate axes lines to draw.")]
    public float axisLength = 1.0f;

    /// <summary>
    /// Called by the editor to draw Gizmos in the Scene view.
    /// Executes both in Play mode and Edit mode.
    /// Also called when the object is selected (before OnDrawGizmosSelected).
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!enabled) // Respect the component's enabled state
            return;

        // Store original color to restore later
        Color originalGizmoColor = Gizmos.color;

        // --- Draw Sphere ---
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

        // --- Draw Label (if always shown) ---
        if (alwaysShowLabel)
        {
            DrawLabel();
        }

        // --- Draw Axes (if enabled) ---
        if (showAxes)
        {
            DrawAxes();
        }

        // Restore original color
        Gizmos.color = originalGizmoColor;
    }

    /// <summary>
    /// Called by the editor only when the object IS selected.
    /// Executes both in Play mode and Edit mode.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!enabled) // Respect the component's enabled state
            return;

        // Store original color to restore later
        Color originalGizmoColor = Gizmos.color;

        // --- Draw Selection Sphere ---
        Gizmos.color = selectedGizmoColor;
        // Draw a wire sphere for selection, slightly larger
        Gizmos.DrawWireSphere(transform.position, selectedGizmoRadius);

        // --- Draw Label (always shown when selected, unless disabled globally) ---
        // No need to check alwaysShowLabel here, as this function means it *is* selected.
        DrawLabel();

        // --- Draw Axes (if enabled, potentially drawing over non-selected axes) ---
        // Note: If showAxes is true, DrawAxes() was already called in OnDrawGizmos.
        // You could uncomment the next lines if you wanted selected axes to be different
        // (e.g., different color or length), but usually drawing them once is sufficient.
        // if (showAxes)
        // {
        //     // Potentially use selectedGizmoColor or different logic here
        //     DrawAxes();
        // }


        // Restore original color
        Gizmos.color = originalGizmoColor;
    }

    /// <summary>
    /// Helper method to draw the coordinate axes lines.
    /// Assumes Gizmos.color might be changed and does not restore it.
    /// </summary>
    private void DrawAxes()
    {
        Vector3 origin = transform.position; // Axes originate from the actual pivot point

        // X Axis (Red)
        Gizmos.color = Color.red;
        // transform.right gives the object's local X direction in world space
        Gizmos.DrawRay(origin, transform.right * axisLength);

        // Y Axis (Green)
        Gizmos.color = Color.green;
        // transform.up gives the object's local Y direction in world space
        Gizmos.DrawRay(origin, transform.up * axisLength);

        // Z Axis (Blue)
        Gizmos.color = Color.blue;
        // transform.forward gives the object's local Z direction in world space
        Gizmos.DrawRay(origin, transform.forward * axisLength);
    }


    /// <summary>
    /// Helper method to draw the object's name label using Handles.
    /// Requires UnityEditor namespace and needs preprocessor guards.
    /// </summary>
    private void DrawLabel()
    {
#if UNITY_EDITOR // Ensure this code is only compiled in the editor
        // Use Handles for drawing labels in the Scene view for better control
        GUIStyle style = new GUIStyle();
        // Use the selected color for the label for consistency when selected,
        // but fall back to the normal gizmo color if not selected but alwaysShowLabel is true.
        style.normal.textColor = (Selection.activeGameObject == gameObject) ? selectedGizmoColor : gizmoColor;
        style.alignment = TextAnchor.MiddleCenter;
        // Use the larger selected radius for offset calculation if selected, otherwise normal radius
        float radiusForOffset = (Selection.activeGameObject == gameObject) ? selectedGizmoRadius : gizmoRadius;
        Handles.Label(transform.position + Vector3.up * radiusForOffset * labelOffsetMultiplier, gameObject.name, style); // Offset slightly above
#endif
    }
}