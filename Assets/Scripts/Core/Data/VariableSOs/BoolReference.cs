using System;

[Serializable]
public class BoolReference : AbstractReference<bool, BoolVariableSO>
{
    public BoolReference() : base() { }
    public BoolReference(bool value) : base(value) { }
    public BoolReference(BoolVariableSO variable) : base(variable) { }
}