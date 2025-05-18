using UnityEngine;

/// <summary>
/// Abstract base class for components that listen to a specific GameEventSO channel.
/// Handles the registration and unregistration boilerplate.
/// Derived classes must implement how to respond to the event.
/// </summary>
/// <typeparam name="T">The type of data the event carries.</typeparam>
/// <typeparam name="TEvent">The type of the ScriptableObject event channel.</typeparam>
public abstract class GameEventListenerBase<T, TEvent> : MonoBehaviour, IGameEventListener<T>
    where TEvent : GameEventSO<T>
{
    [Tooltip("The event channel to register to.")]
    [SerializeField] protected TEvent gameEvent = default;

    /// <summary>
    /// Called when the component becomes enabled and active.
    /// Registers the listener with the specified event channel.
    /// </summary>
    protected virtual void Start()
    {
        // Ensure the gameEvent is assigned before attempting to register.
        if (gameEvent == null)
        {
            Debug.LogError($"GameEvent is not assigned in the inspector for {this.GetType().Name} on GameObject {this.gameObject.name}. Cannot register listener.", this);
            return;
        }
        gameEvent.RegisterListener(this);
    }

    /// <summary>
    /// Called when the component is disabled or destroyed.
    /// Unregisters the listener from the event channel.
    /// </summary>
    protected virtual void OnDisable()
    {
        // Only attempt to unregister if the gameEvent was assigned.
        gameEvent?.UnregisterListener(this);
    }

    /// <summary>
    /// This method is called by the GameEventSO when the event it is listening to is raised.
    /// It delegates the actual response logic to the abstract HandleEventRaised method.
    /// </summary>
    /// <param name="data">The data payload received from the event channel.</param>
    public void OnEventRaised(T data)
    {
        HandleEventRaised(data);
    }

    /// <summary>
    /// Abstract method that derived classes must implement to define
    /// how they react to the received event data.
    /// </summary>
    /// <param name="data">The data payload received from the event channel.</param>
    protected abstract void HandleEventRaised(T data);
}