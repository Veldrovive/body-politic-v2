// Purpose: Defines the contract for listeners of generic game events.

public interface IGameEventListener<T> {
    // Method called by the GameEventSO<T> when the event is raised
    void OnEventRaised(T data);
}