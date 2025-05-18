// Purpose: The generic ScriptableObject representing an event channel.

using System.Collections.Generic;
using UnityEngine;

public abstract  class GameEventSO<T> : ScriptableObject {
    // We set up listeners as a list that takes the generic type
    private readonly List<IGameEventListener<T>> listeners = new List<IGameEventListener<T>>();

    // Method to raise the event
    public void Raise(T data) {
        // We loop backwards because a listener might unregister itself
        for (int i = listeners.Count - 1; i >= 0; i--) {
            // A listener might have been destroyed, so we check if it's null
            if (listeners[i] != null) {
                listeners[i].OnEventRaised(data);
            } else {
                listeners.RemoveAt(i);
            }
        }
    }

    public void RegisterListener(IGameEventListener<T> listener) {
        // We check if the listener is already in the list
        if (!listeners.Contains(listener)) {
            listeners.Add(listener);
        }
    }

    public void UnregisterListener(IGameEventListener<T> listener) {
        // Can't remove it if it's not in the list
        if (listeners.Contains(listener)) {
            listeners.Remove(listener);
        }
    }
}
