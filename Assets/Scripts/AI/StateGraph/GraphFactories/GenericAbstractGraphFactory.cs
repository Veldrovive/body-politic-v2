using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

public class AbstractGraphFactoryConfig
{
    [NonSerialized] public string GraphId = null;
    
    public AbstractGraphFactoryConfig(string graphId = null)
    {
        this.GraphId = graphId;
    }
}

public class GraphFactoryConnectionEnd
{
    public StateGraphNode GraphNode;
    public string PortName;
    
    public GraphFactoryConnectionEnd() {}

    public GraphFactoryConnectionEnd(StateGraphNode graphNode, string portName)
    {
        this.GraphNode = graphNode;
        this.PortName = portName;
    }
}

public abstract class AbstractGraphFactory
{
    protected bool configured = false;
    protected abstract AbstractGraphFactoryConfig abstractConfig { get; }
    public AbstractGraphFactoryConfig AbstractConfig => abstractConfig;
    
    protected abstract void ConstructGraphInternal(StateGraph graph, GraphFactoryConnectionEnd startPoint);
    public virtual void ConstructGraph(StateGraph graph, GraphFactoryConnectionEnd startPoint = null, bool fillExits = true)
    {
        if (!configured)
        {
            throw new System.Exception("GraphFactory not configured");
        }
        
        if (graph == null)
        {
            throw new System.Exception("Graph is null");
        }

        if (!string.IsNullOrEmpty(this.abstractConfig.GraphId))
        {
            graph.SetGUID(this.abstractConfig.GraphId);
        }

        if (startPoint == null)
        {
            StartNode startNode = new StartNode();
            graph.AddNode(startNode);
            startPoint = new GraphFactoryConnectionEnd(startNode, StartNode.OUT_PORT_NAME);
        }
        
        ConstructGraphInternal(graph, startPoint);
    }
    
    public void SetGraphId(string graphId)
    {
        if (abstractConfig == null)
        {
            throw new System.Exception("GraphFactory not configured. Cannot set Id unless configuration is complete.");
        }
        
        this.abstractConfig.GraphId = graphId;
    }
}

public abstract class GenericAbstractGraphFactory<TConfig, TExitConnectionEnum> : AbstractGraphFactory
    where TConfig : AbstractGraphFactoryConfig
    where TExitConnectionEnum : Enum
{
    protected TConfig config;
    protected override AbstractGraphFactoryConfig abstractConfig => config;
    
    // Exit connections are used to chain graphs together. While the graph is being constructed, exit connections
    // are added. Then subsequent graphs can use these exit connections as the start point for their own graphs.
    protected Dictionary<TExitConnectionEnum, GraphFactoryConnectionEnd> exitConnections = new Dictionary<TExitConnectionEnum, GraphFactoryConnectionEnd>();
    protected Dictionary<TExitConnectionEnum, string> defaultSayOnExit = new Dictionary<TExitConnectionEnum, string>();
    protected HashSet<TExitConnectionEnum> openExitConnections = new HashSet<TExitConnectionEnum>();
    protected void AddExitConnection(TExitConnectionEnum exitId, GraphFactoryConnectionEnd exitConnection, string defaultExitSay=null)
    {
        if (exitConnections.ContainsKey(exitId))
        {
            // This is an error. The exit connection was already added.
            throw new System.Exception($"Exit connection {exitId} was already added to the graph. Please ensure that exit connections are unique.");
        }
        exitConnections.Add(exitId, exitConnection);
        defaultSayOnExit.Add(exitId, defaultExitSay);
        openExitConnections.Add(exitId);
    }

    protected void AddExitConnection(TExitConnectionEnum exitId, StateGraphNode exitNode, string portName, string defaultExitSay=null)
    {
        AddExitConnection(exitId, new GraphFactoryConnectionEnd(exitNode, portName), defaultExitSay);
    }

    /// <summary>
    ///  Helper method for simultaneously connecting multiple outcomes of a state node to other state nodes.
    /// </summary>
    protected void ConnectStateFlows<TOutcomeEnum>(StateGraph graph, StateNode lastNode, Dictionary<TOutcomeEnum, StateNode> outcomeFlows)
        where TOutcomeEnum : Enum
    {
        // Check to ensure that the outcome enum is correct for this state node
        if (lastNode?.OutcomeEnumType != typeof(TOutcomeEnum))
        {
            throw new System.Exception($"StateNode {lastNode?.id} has an outcome enum type of {lastNode?.OutcomeEnumType}, but the provided stateFlows has an outcome enum type of {typeof(TOutcomeEnum)}.");
        }
        
        foreach (var outcomeFlow in outcomeFlows)
        {
            TOutcomeEnum outcome = outcomeFlow.Key;  // Equals the portName when converted to string
            StateNode nextNode = outcomeFlow.Value;

            graph.ConnectStateFlow(lastNode, outcome, nextNode);
        }
    }

    /// <summary>
    /// Helper method for simultaneously connecting multiple outcomes of a state node to exit connections.
    /// </summary>
    protected void ConnectExitFlows<TOutcomeEnum>(StateNode lastNode,
        Dictionary<TOutcomeEnum, (TExitConnectionEnum exitConnection, string exitSay)> exitFlows)
        where TOutcomeEnum : Enum
    {
        // Check to ensure that the outcome enum is correct for this state node
        if (lastNode?.OutcomeEnumType != typeof(TOutcomeEnum))
        {
            throw new System.Exception($"StateNode {lastNode?.id} has an outcome enum type of {lastNode?.OutcomeEnumType}, but the provided exitFlows has an outcome enum type of {typeof(TOutcomeEnum)}.");
        }
        
        foreach (var exitFlow in exitFlows)
        {
            TOutcomeEnum outcome = exitFlow.Key;  // Equals the portName when converted to string
            string portName = nameof(outcome);
            (TExitConnectionEnum exitConnection, string exitSay) = exitFlow.Value;
            AddExitConnection(
                exitConnection,
                lastNode, portName, exitSay
            );
        }
    }

    /// <summary>
    /// Helper for connecting the interrupt outcome of a state node to the next state node.
    /// </summary>
    protected void ConnectStateInterrupt(StateGraph graph, StateNode lastNode, StateNode nextNode)
    {
        graph.ConnectStateFlow(lastNode, StateNode.INTERRUPT_PORT_NAME, nextNode, StateNode.IN_PORT_NAME);
    }

    /// <summary>
    /// Helper for connecting the load-in outcome of a state node to the next state node.
    /// </summary>
    protected void ConnectStateLoadIn(StateGraph graph, StateNode lastNode, StateNode nextNode)
    {
        graph.ConnectStateFlow(lastNode, StateNode.LOAD_IN_PORT_NAME, nextNode, StateNode.IN_PORT_NAME);
    }
    
    [CanBeNull]
    protected GraphFactoryConnectionEnd UseExitConnection(TExitConnectionEnum exitId)
    {
        if (!exitConnections.ContainsKey(exitId))
        {
            // This is an error. The exit connection was not added during graph construction.
            throw new System.Exception($"Exit connection {exitId} was not added to the graph. Please ensure that all exit connections are added during graph construction.");
        }
        // If the exit connection is not open, then we return null.
        if (!openExitConnections.Contains(exitId))
        {
            return null;
        }
        
        // Otherwise the connection is exists and is open, so we return it and mark it as closed.
        openExitConnections.Remove(exitId);
        return exitConnections[exitId];
    }
    
    public GenericAbstractGraphFactory(TConfig config, string graphId = null)
    {
        this.config = config;
        this.configured = true;
    }

    public void FillEmptyExitConnections(StateGraph graph)
    {
        // Fills all exit connections still in openExitConnections with a connection to an ExitNode.
        foreach (TExitConnectionEnum exitId in openExitConnections.ToList())
        {
            GraphFactoryConnectionEnd exitConnection = UseExitConnection(exitId);
            if (string.IsNullOrEmpty(defaultSayOnExit[exitId]))
            {
                // Then we just connect straight to the ExitNode without a SayStateNode.
                graph.ConnectStateFlow(exitConnection.GraphNode, exitConnection.PortName,
                    new ExitNode(), ExitNode.IN_PORT_NAME);
            }
            else
            {
                // Otherwise we add a SayStateNode between the exit connection and the ExitNode.
                AddConnectionThroughSay(
                    graph,
                    exitConnection.GraphNode, exitConnection.PortName,
                    new ExitNode(), ExitNode.IN_PORT_NAME,
                    defaultSayOnExit[exitId], 3f, 3f
                );
            }
        }
    }
    
    public override void ConstructGraph(StateGraph graph, GraphFactoryConnectionEnd startPoint = null, bool fillExits = true)
    {
        base.ConstructGraph(graph, startPoint, fillExits);
        // We expect that at the end of the graph construction, all of the exit connections have been added.
        // We should check if there are any values of TExitConnectionEnum that are not in the openExitConnections.
        // foreach (TExitConnectionEnum exitId in Enum.GetValues(typeof(TExitConnectionEnum)))
        // {
        //     if (!openExitConnections.Contains(exitId))
        //     {
        //         throw new System.Exception($"Exit connection {exitId} was not added to the graph. Please ensure that all exit connections are added during graph construction.");
        //     }
        // }
        // Actually, I decided against that. There are some cases where exits conditionally exist based on the graph configuration.
        
        if (fillExits)
        {
            FillEmptyExitConnections(graph);
        }
    }

    protected void AddConnectionThroughSay(StateGraph graph, StateGraphNode source, string sourcePortName,
        StateGraphNode destination, string destinationPortName, string message, float messageDuration, float? waitDuration = null)
    {
        if (!waitDuration.HasValue)
        {
            waitDuration = messageDuration;
        }
        
        // We automatically add the source and destination nodes to the graph if they are no already present
        if (graph.GetNodeById(source.id) == null)
        {
            graph.AddNode(source);
        }
        if (graph.GetNodeById(destination.id) == null)
        {
            graph.AddNode(destination);
        }

        if (string.IsNullOrEmpty(message))
        {
            graph.ConnectStateFlow(
                source.id, new NodePortInfo(sourcePortName, PortType.StateTransitionOut),
                destination.id, new NodePortInfo(destinationPortName, PortType.StateTransitionIn)
            );
        }
        else
        {
            SayStateNode sayStateNode = new(new SayStateConfiguration()
            {
                m_logLevel = LogLevel.Info,
                m_textDuration = messageDuration,
                m_textToSay = message,
                m_waitDuration = waitDuration.Value
            });
        
            graph.AddNode(sayStateNode);
            graph.ConnectStateFlow(
                source.id, new NodePortInfo(sourcePortName, PortType.StateTransitionOut),
                sayStateNode.id, new NodePortInfo(StateNode.IN_PORT_NAME, PortType.StateTransitionIn)
            );
            graph.ConnectStateFlow(
                sayStateNode.id, new NodePortInfo(nameof(SayStateOutcome.Timeout), PortType.StateTransitionOut),
                destination.id, new NodePortInfo(destinationPortName, PortType.StateTransitionIn)
            );
        }
    }
}