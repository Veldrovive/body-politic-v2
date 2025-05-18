using System;

[Serializable]
public class FloatReference : AbstractReference<float, FloatVariableSO>
{
    public FloatReference() : base() { }
    public FloatReference(float value) : base(value) { }
    public FloatReference(FloatVariableSO variable) : base(variable) { }
}
