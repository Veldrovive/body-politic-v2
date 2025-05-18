using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A generic MonoBehaviour that listens to a specific GameEventSO channel
/// and invokes a UnityEvent in response.
/// </summary>
/// <typeparam name="T">The type of data the event carries.</typeparam>
/// <typeparam name="TEvent">The type of the ScriptableObject event channel.</typeparam>
/// <typeparam name="TUnityEvent">The type of the UnityEvent to invoke.</typeparam>
public class GameEventListener<T, TEvent, TUnityEvent> : GameEventListenerBase<T, TEvent>
    where TEvent : GameEventSO<T>
    where TUnityEvent : UnityEvent<T>
{
    [Tooltip("The UnityEvent response to invoke when the GameEvent is raised.")]
    [SerializeField] private TUnityEvent unityEventResponse = default;

    /// <summary>
    /// Handles the event raised by the GameEventSO channel.
    /// Invokes the configured UnityEvent response, passing the event data.
    /// </summary>
    /// <param name="data">The data payload received from the event channel.</param>
    protected override void HandleEventRaised(T data)
    {
        // Invoke the UnityEvent response if it's assigned.
        unityEventResponse?.Invoke(data);
    }
}