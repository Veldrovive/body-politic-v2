using UnityEditor;
using UnityEngine;

public abstract class AbstractVariableSO<T> : ScriptableObject
{
    [SerializeField] private T value;
    public virtual T Value { get => value; set => this.value = value; }
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
}
