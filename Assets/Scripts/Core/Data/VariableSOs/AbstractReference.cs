using System;
using UnityEngine;

[System.Serializable]
public abstract class AbstractReference<T, VarT>
    where VarT : AbstractVariableSO<T>
{
    public bool UseConstant = true;
    public T ConstantValue = default;
    public VarT Variable;

    public object Clone()
    {
        return MemberwiseClone();
    }

    public T Value
    {
        get => UseConstant ? ConstantValue : Variable.Value;
        set
        {
            if (UseConstant)
            {
                ConstantValue = value;
            }
            else
            {
                Variable.Value = value;
            }
        }
    }
    public AbstractReference() { }
    public AbstractReference(T value)
    {
        UseConstant = true;
        ConstantValue = value;
    }
    public AbstractReference(VarT variable)
    {
        UseConstant = false;
        Variable = variable;
    }

    // Implicit conversion operator to allow direct assignment from the reference to the value
    // This allows you to use the reference as if it were the value type
    public static implicit operator T(AbstractReference<T, VarT> reference)
    {
        return reference.Value;
    }
}
