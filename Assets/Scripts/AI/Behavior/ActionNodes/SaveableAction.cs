using Unity.Behavior;

/// <summary>
/// When we load a BehaviorGraph from a save file OnStart() is not called and we have no way
/// of initializing the action node. We can add that ability by keeping track of whether we have started.
/// </summary>
public abstract class SaveableAction : Action
{
    private bool started = false;
    protected override Status OnStart()
    {
        started = true;
        
        return base.OnStart();
    }

    protected abstract Status OnLoad();

    protected override Status OnUpdate()
    {
        if (!started)
        {
            OnLoad();
            started = true;
        }
        
        return base.OnUpdate();
    }

    protected override void OnEnd()
    {
        started = false;
        
        base.OnEnd();
    }
}