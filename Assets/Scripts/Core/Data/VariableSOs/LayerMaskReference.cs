using System;
using UnityEngine;

[Serializable]
public class LayerMaskReference : AbstractReference<LayerMask, LayerMaskVariableSO>
{
    public LayerMaskReference() : base() { }
    public LayerMaskReference(LayerMask value) : base(value) { }
    public LayerMaskReference(LayerMaskVariableSO variable) : base(variable) { }
}