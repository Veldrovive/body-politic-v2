using Newtonsoft.Json;

public class EventListenerNode : StateGraphNode
{
    [JsonIgnore]  // Gets set when the state graph is constructed. When the node is added to the graph.
    protected NpcContext npcContext = null;
    public void SetNpcContext(NpcContext npcContext)
    {
        this.npcContext = npcContext;
    }
}