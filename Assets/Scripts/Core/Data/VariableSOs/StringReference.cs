using System;

[Serializable]
public class StringReference : AbstractReference<string, StringVariableSO>
{
    public StringReference() : base() { }
    public StringReference(String value) : base(value) { }
    public StringReference(StringVariableSO variable) : base(variable) { }
}