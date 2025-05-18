using System;

[Serializable]
public class IntReference : AbstractReference<int, IntVariableSO>
{
    public IntReference() : base() { }
    public IntReference(int value) : base(value) { }
    public IntReference(IntVariableSO variable) : base(variable) { }
}