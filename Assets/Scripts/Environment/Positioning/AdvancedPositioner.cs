using UnityEngine;

/// <summary>
/// Defines the different ways this object can be positioned relative to a target.
/// </summary>
public enum PositioningMode
{
    /// <summary>
    /// Positions the object using a fixed offset in world space coordinates from the target.
    /// </summary>
    WorldSpaceOffset,

    /// <summary>
    /// Positions the object using an offset in the target's local space coordinates.
    /// The offset will rotate with the target.
    /// </summary>
    LocalSpaceOffsetTarget,

    /// <summary>
    /// Keeps the object at a specific radius from the target.
    /// The object will be positioned on the line from the target to its current position, at the specified radius.
    /// </summary>
    FollowRadius
}

/// <summary>
/// Positions this GameObject relative to a target Transform using various modes,
/// with an optional check for obstacles.
/// </summary>
public class AdvancedPositioner : MonoBehaviour
{
    [Header("Target Object")]
    [Tooltip("The Transform of the object to position relative to.")]
    public Transform targetObject;

    [Header("Positioning Logic")]
    [Tooltip("Determines how this object will be positioned relative to the target.")]
    public PositioningMode mode = PositioningMode.WorldSpaceOffset;

    [Header("Mode Settings")]
    [Tooltip("The offset from the target object in world space coordinates. Used in WorldSpaceOffset mode.")]
    public Vector3 worldOffset = new Vector3(1f, 0f, 0f);

    [Tooltip("The offset from the target object in its local space coordinates. Used in LocalSpaceOffsetTarget mode.")]
    public Vector3 localOffset = new Vector3(1f, 0f, 0f);

    [Tooltip("The desired distance to maintain from the target. Used in FollowRadius mode.")]
    public float followRadius = 5f;

    [Header("Obstacle Detection")]
    [Tooltip("If enabled, a linecast will check for obstacles between the target and the desired position. The object will be placed at the collision point if an obstacle is found.")]
    public bool checkForObstacles = false;

    [Tooltip("Layers that the linecast should consider as obstacles. Ensure your 'wall' or 'obstacle' GameObjects are on one of these layers.")]
    public LayerMask collisionLayers; // This LayerMask will be used by Physics.Linecast

    /// <summary>
    /// Called every frame, if the MonoBehaviour is enabled.
    /// Handles the positioning of this GameObject based on the selected mode, target, and obstacle detection settings.
    /// </summary>
    void Update()
    {
        // Ensure a targetObject is assigned to prevent errors.
        if (targetObject == null)
        {
            // It's good practice to log a warning if a critical dependency is missing.
            Debug.LogWarning("Target Object not assigned in AdvancedPositioner. Cannot position.", this);
            return; // Stop further execution in Update if no target.
        }

        Vector3 desiredPosition; // This will store the calculated position before obstacle check.
        Vector3 finalPosition;   // This will be the actual position set to the transform.

        switch (mode)
        {
            case PositioningMode.WorldSpaceOffset:
                // Calculate the desired position using a world-space offset from the target.
                // This offset remains constant regardless of the target's rotation.
                desiredPosition = targetObject.position + worldOffset;
                break;

            case PositioningMode.LocalSpaceOffsetTarget:
                // Calculate the desired position using an offset in the target's local space.
                // The targetObject.TransformDirection converts the local offset vector into a world space direction.
                // This means the object will move relative to the target's orientation.
                desiredPosition = targetObject.position + targetObject.TransformDirection(localOffset);
                break;

            case PositioningMode.FollowRadius:
                // For FollowRadius, the desired position is on a sphere around the target.
                Vector3 directionToSelf = transform.position - targetObject.position;
                float currentDistance = directionToSelf.magnitude;

                // if (currentDistance < Mathf.Epsilon) // Mathf.Epsilon is a very small float, used for comparing floats to zero.
                // {
                //     // If this object is at the same position as the target, choose a default direction.
                //     // Try target's forward, then world forward as a fallback.
                //     Vector3 fallbackDirection = targetObject.forward;
                //     if (fallbackDirection.sqrMagnitude < Mathf.Epsilon) // Check if target.forward is not a zero vector.
                //     {
                //         fallbackDirection = Vector3.forward; // Default to world Z-axis if target.forward is zero.
                //     }
                //     // Normalize the fallbackDirection to ensure it only represents direction, not magnitude.
                //     desiredPosition = targetObject.position + fallbackDirection.normalized * followRadius;
                // }
                if (currentDistance < followRadius)
                {
                    // Great! Nothing to do. We still set the desired position so that it will keep in LoS of the target
                    desiredPosition = transform.position;
                }
                else
                {
                    // Calculate the position on the radius along the line from the target to the object's current position.
                    // (directionToSelf / currentDistance) is a normalized vector.
                    desiredPosition = targetObject.position + (directionToSelf / currentDistance) * followRadius;
                }
                break;
            
            default:
                // This path should not be reached if all enum cases are handled.
                Debug.LogError("Unhandled PositioningMode in AdvancedPositioner.", this);
                // Set desiredPosition to current position to avoid uninitialized variable error if something goes wrong.
                desiredPosition = transform.position; 
                break;
        }

        // Apply obstacle detection if enabled.
        if (checkForObstacles)
        {
            Vector3 originPoint = targetObject.position; // Linecast originates from the target.
            Vector3 vectorToDesired = desiredPosition - originPoint;
            float distanceToDesired = vectorToDesired.magnitude;

            // Only perform linecast if there's a meaningful distance/direction to check.
            // If desiredPosition is the same as originPoint (e.g., zero offset), no need for a linecast.
            if (distanceToDesired > Mathf.Epsilon)
            {
                RaycastHit hitInfo;
                // Perform a linecast from the originPoint towards the desiredPosition, up to distanceToDesired.
                // It checks for colliders on the layers specified in collisionLayers.
                if (Physics.Linecast(originPoint, desiredPosition, out hitInfo, collisionLayers))
                {
                    // An obstacle was hit. Position the object at the point of collision.
                    // hitInfo.point gives the world space coordinate where the ray hit the collider.
                    finalPosition = hitInfo.point;
                }
                else
                {
                    // No obstacle was hit along the path. Use the originally calculated desiredPosition.
                    finalPosition = desiredPosition;
                }
            }
            else
            {
                // If the desired position is effectively the same as the origin (e.g., zero offset or zero radius),
                // no linecast is needed. Use the desired position.
                finalPosition = desiredPosition;
            }
        }
        else
        {
            // If obstacle checking is disabled, use the desired position directly.
            finalPosition = desiredPosition;
        }

        // Apply the final calculated position to this GameObject's transform.
        transform.position = finalPosition;
    }
}