using UnityEditor;
using UnityEngine;

public class VariableSOSaveableData<T> : SaveableData
{
    public T Value;
}

public interface IVariableSOValueProvider
{
    object GetValueAsObject();
}

public abstract class AbstractVariableSO<T> : SaveableSO, IVariableSOValueProvider
{
    [SerializeField] private T value;
    public virtual T Value { get => value; set => this.value = value; }

    public object GetValueAsObject()
    {
        return value;
    }
    
    // From https://www.reddit.com/r/Unity3D/comments/182ija3/comment/kaj2nhv/?utm_source=share&utm_medium=web3x&utm_name=web3xcss&utm_term=1&utm_content=share_button
    #if UNITY_EDITOR
    T _startValue;
    [SerializeField] protected bool _resetOnPlay = true;
    protected virtual void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
    protected virtual void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }
    protected virtual void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (_resetOnPlay) 
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _startValue = Value;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Value = _startValue;
            }
        }
        else
        {
            return;
        }
    }
    #endif

    public override SaveableData GetSaveData()
    {
        VariableSOSaveableData<T> data = new VariableSOSaveableData<T>();
        data.Value = Value;
        return data;
    }

    public override void LoadSaveData(SaveableData data, bool blankLoad)
    {
        if (!blankLoad)
        {
            // Then we load the value from the save data
            if (data is VariableSOSaveableData<T> variableData)
            {
                Value = variableData.Value;
            }
            else
            {
                Debug.LogError($"Invalid data type for {name}. Expected VariableSOSaveableData<{typeof(T)}> but got {data.GetType()}");
            }
        }
    }
}
