using UnityEngine;
using System;

[Serializable]
public class Vector3Reference : AbstractReference<Vector3, Vector3VariableSO>
{
    public Vector3Reference() : base() { }
    public Vector3Reference(Vector3 value) : base(value) { }
    public Vector3Reference(Vector3VariableSO variable) : base(variable) { }
}