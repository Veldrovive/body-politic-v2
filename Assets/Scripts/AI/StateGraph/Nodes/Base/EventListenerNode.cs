public class EventListenerNode : StateGraphNode
{
    protected NpcContext npcContext = null;
    public void SetNpcContext(NpcContext npcContext)
    {
        this.npcContext = npcContext;
    }
}