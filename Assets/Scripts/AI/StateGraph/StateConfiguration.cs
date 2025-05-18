using System;

/// <summary>
/// Base class for configuration data objects used with states.
/// </summary>
[Serializable]
public abstract class AbstractStateConfiguration
{
    public string StateName;
    public string StateId = Guid.NewGuid().ToString();
    
    public abstract Type AssociatedStateType { get; }

    public AbstractStateConfiguration()
    {
        
    }
}