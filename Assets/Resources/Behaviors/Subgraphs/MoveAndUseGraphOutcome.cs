using Unity.Behavior;

[BlackboardEnum]
public enum MoveAndUseGraphOutcome
{
    Completed,
    Error,
    DoorRoleFailed,
    InteractionRoleFailed,
    InteractionProximityFailed,
}