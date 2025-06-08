// Subclass of SaveableGOConsumer that does not save any data. It is instead used as a pointer to the GameObject.
// This is useful for serializing children of a SaveableGOProducer in a way that is compatible with the use of
// prefabs. In practice, this is used for Holdables that are dynamically instantiated in the game.

public class SaveableGOPointer : SaveableGOConsumer
{
    public SaveableGOProducer ParentProducer => linkedSaveableProducer;
    public override SaveableData GetSaveData()
    {
        return null;
    }
    
    public override void LoadSaveData(SaveableData data)
    {
        // This consumer does not load any data. It is just a pointer to the GameObject.
    }
}