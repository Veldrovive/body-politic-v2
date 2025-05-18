using UnityEngine;
using System;

[Serializable]
public class TransformReference : AbstractReference<Transform, TransformVariableSO>
{
    public TransformReference() : base() { }
    public TransformReference(Transform value) : base(value) { }
    public TransformReference(TransformVariableSO variable) : base(variable) { }
}