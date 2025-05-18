using System;
using UnityEngine;

[Serializable]
public abstract class AreaFilter
{
    public abstract bool IsInArea(Vector3 position);
}


[Serializable]
public class SphericalAreaFilter : AreaFilter
{
    private Vector3 center;
    private float squaredRadius;

    public SphericalAreaFilter(Vector3 center, float radius)
    {
        this.center = center;
        this.squaredRadius = radius * radius;  // Precompute squared radius for performance
    }

    public override bool IsInArea(Vector3 position)
    {
        // Check if the squared distance from the center is less than or equal to the squared radius
        return Vector3.SqrMagnitude(position - center) <= squaredRadius;
    }
}


[Serializable]
public class CylindricalAreaFilter : AreaFilter
{
    private Vector3 center;
    private float squaredRadius;
    private float minHeight;
    private float maxHeight;

    public CylindricalAreaFilter(Vector3 center, float radius, float heightBelow, float heightAbove)
    {
        this.center = center;
        this.squaredRadius = radius * radius;  // Precompute squared radius for performance
        this.minHeight = center.y - heightBelow;
        this.maxHeight = center.y + heightAbove;
    }

    public override bool IsInArea(Vector3 position)
    {
        // Check if the squared distance from the center is less than or equal to the squared radius
        // and if the height is within the specified range
        return Vector3.SqrMagnitude(new Vector3(position.x, 0, position.z) - new Vector3(center.x, 0, center.z)) <= squaredRadius &&
               position.y >= minHeight && position.y <= maxHeight;
    }
}