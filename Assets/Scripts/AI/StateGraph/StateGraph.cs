using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Object = UnityEngine.Object;

public class StateGraph : MonoBehaviour
{
    [SerializeField] private string displayName = "State Graph";
    public string DisplayName => displayName;
    [SerializeField] private string description = "";
    public string Description => description;
    
    [SerializeField] private string m_guid = Guid.NewGuid().ToString();
    public string id => m_guid;
    
    [SerializeReference] private List<StateGraphNode> m_nodes = new();
    public List<StateGraphNode> Nodes => m_nodes;
    
    [SerializeField] private List<StateGraphConnection> m_connections = new();
    public List<StateGraphConnection> Connections => m_connections;
    
    public void SetGUID(string guid)
    {
        m_guid = guid;
    }

    #region Query Functions

    public bool IsEmpty()
    {
        return m_nodes.Count == 0;
    }
    
    public StateGraphNode GetNodeById(string nodeId)
    {
        foreach (var node in m_nodes)
        {
            if (node.id == nodeId)
                return node;
        }

        return null;
    }

    /// <summary>
    /// Finds all connections on a given node such that the PortType on the given node matches the parameter
    /// </summary>
    public (List<StateGraphConnection> outgoingConnections, List<StateGraphConnection> incominngConnections) GetNodeConnectionsOfType(string nodeId, PortType? portType)
    {
        StateGraphNode node = GetNodeById(nodeId);
        if (node == null)
        {
            Debug.LogError($"Node with ID {nodeId} not found.");
            return (null, null);
        }
        
        List<StateGraphConnection> outgoingConnection = new List<StateGraphConnection>();
        List<StateGraphConnection> incomingConnection = new List<StateGraphConnection>();
        foreach (var connection in m_connections)
        {
            if (connection.inputPort.nodeId == nodeId && (portType == null || connection.inputPort.portInfo.PortType == portType))
            {
                incomingConnection.Add(connection);
            }
            
            // Not else if as we allow edges between a node and itself
            if (connection.outputPort.nodeId == nodeId && (portType == null || connection.outputPort.portInfo.PortType == portType))
            {
                outgoingConnection.Add(connection);
            }
        }
        return (outgoingConnection, incomingConnection);
    }

    public (NodePortContext inputPortContext, NodePortContext outputPortContext) GetConnectionPortContext(
        StateGraphConnection connection)
    {
        StateGraphNode inputNode = GetNodeById(connection.inputPort.nodeId);
        StateGraphNode outputNode = GetNodeById(connection.outputPort.nodeId);
        
        if (inputNode == null || outputNode == null)
        {
            Debug.LogError($"One of the nodes in the connection is missing. Input Node: {inputNode}, Output Node: {outputNode}");
            return (default, default);
        }
        
        NodePortContext inputPortContext = inputNode.PortContext[connection.inputPort.portInfo];
        NodePortContext outputPortContext = outputNode.PortContext[connection.outputPort.portInfo];
        return (inputPortContext, outputPortContext);
    }

    private Dictionary<string, List<(EventInfo, object, Delegate)>> savedDelegates = new Dictionary<string, List<(EventInfo, object, Delegate)>>();
    public void LinkNodeOutgoingEvents(string nodeId, AbstractState state)
    {
        if (savedDelegates.ContainsKey(nodeId))
        {
            // We need to remove existing delegates. This should have been done when the state exited.
            Debug.LogWarning($"StateGraph: {nodeId} already has delegates. They should have been removed when the state exited.");
            RemoveNodeOutgoingEvents(nodeId);
        }
        
        var (outgoingConnections, incomingConnections) = GetNodeConnectionsOfType(nodeId, PortType.EventOut);
        // We are interested only in the outgoing connections
        List<(EventInfo, object, Delegate)> eventDelegates = new List<(EventInfo, object, Delegate)>();
        foreach (var outgoingConnection in outgoingConnections)
        {
            var (inputPortContext, outputPortContext) = GetConnectionPortContext(outgoingConnection);
            
            // Events are ugly as hell.
            object publisher;
            (string publisherType, EventInfo eventInfo) =
                (ValueTuple<string, EventInfo>) outputPortContext.portContext;
            if (publisherType == "state")
            {
                // Then the state that got passed in is the publisher
                publisher = state;
            }
            else if (publisherType == "node")
            {
                // Then the node corresponding to the node on the output port is the publisher
                publisher = GetNodeById(outgoingConnection.outputPort.nodeId);
            }
            else
            {
                // That's an error
                Debug.LogError($"Publisher type {publisherType} is not supported");
                continue;
            }

            object subscriber;
            (string subscriberType, MethodInfo methodInfo) = (ValueTuple<string, MethodInfo>) inputPortContext.portContext;
            if (subscriberType == "state")
            {
                // Then the state that got passed in is the subscriber
                subscriber = state;
            }
            else if (subscriberType == "node")
            {
                // Then the node corresponding to the node on the input port is the subscriber
                subscriber = GetNodeById(outgoingConnection.inputPort.nodeId);
            }
            else
            {
                // That's an error
                Debug.LogError($"Subscriber type {subscriberType} is not supported");
                continue;
            }
            
            Type eventHandlerType = eventInfo.EventHandlerType;
            Delegate handlerDelegate = methodInfo.CreateDelegate(eventHandlerType, subscriber);
            eventInfo.AddEventHandler(publisher, handlerDelegate);
            eventDelegates.Add((eventInfo, publisher, handlerDelegate));
            
            // Debug.Log($"Outgoing connection from {nodeId} to {outgoingConnection.inputPort.nodeId} with port {outgoingConnection.inputPort.portInfo.Name}");
        }
        savedDelegates[nodeId] = eventDelegates;
    }

    public void RemoveNodeOutgoingEvents(string nodeId)
    {
        if (!savedDelegates.ContainsKey(nodeId))
        {
            return;
        }

        foreach (var (eventInfo, publisher, handlerDelegate) in savedDelegates[nodeId])
        {
            eventInfo.RemoveEventHandler(publisher, handlerDelegate);
        }
        savedDelegates.Remove(nodeId);
    }


    /// <summary>
    /// Returns the node on the other side of the connection from the port with the given name.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="portName"></param>
    /// <returns></returns>
    public StateGraphNode GetNodeAfterPortConnection(string nodeId, string portName)
    {
        // First we get all connections from this node
        var (outgoingConnections, incomingConnection) = GetNodeConnectionsOfType(nodeId, null);
        // Now we look through all the connections to find the one that matches the port name
        foreach (var connection in outgoingConnections)
        {
            if (connection.outputPort.portInfo.Name == portName)
            {
                // Now we get the node on the other side of the connection
                string otherNodeId = connection.inputPort.nodeId;
                StateGraphNode otherNode = GetNodeById(otherNodeId);
                return otherNode;
            }
        }
        
        foreach (var connection in incomingConnection)
        {
            if (connection.inputPort.portInfo.Name == portName)
            {
                // Now we get the node on the other side of the connection
                string otherNodeId = connection.outputPort.nodeId;
                StateGraphNode otherNode = GetNodeById(otherNodeId);
                return otherNode;
            }
        }
        
        return null;
    }
    
    public StartNode GetStartNode()
    {
        List<StartNode> startNodes = this.Nodes.OfType<StartNode>().ToList();
        if (startNodes.Count == 0)
        {
            Debug.LogWarning($"State Graph has no startNodes");
            return null;
        }
        
        if (startNodes.Count > 1)
        {
            Debug.LogWarning($"State Graph has more than one startNode");
        }
        return startNodes.FirstOrDefault();
    }

    public StateGraphNode GetStartNodeConnection(StartNode startNode)
    {
       
       
        var (outgoingConnections, incomingConnections) = GetNodeConnectionsOfType(startNode.id, PortType.StateTransitionOut);
        if (outgoingConnections.Count == 0)
        {
            Debug.LogWarning($"State Graph has no outgoingConnections from the start node");
            return null;
        }
        
        StateGraphConnection outgoingConnection = outgoingConnections[0];
        StateGraphConnectionPort inputPort = outgoingConnection.inputPort;
        string inputNodeId = inputPort.nodeId;
        StateGraphNode inputNode = GetNodeById(inputNodeId);
        if (inputNode == null)
        {
            Debug.LogWarning($"State Graph has no inputNode after the start node");
            return null;
        }

        return inputNode;
    }

    /// <summary>
    /// There are special node types JumpInputNode and JumpOutputNode that are used to create jump connections
    /// parameterized by the key .JumpKey. This function finds the next node after the JumpOutputNode with the given key
    /// </summary>
    /// <param name="jumpNodeJumpKey"></param>
    /// <returns></returns>
    public StateGraphNode FindJumpExitNode(string jumpNodeJumpKey)
    {
        List<JumpOutputNode> jumpOutputNodes = this.Nodes.OfType<JumpOutputNode>()
            .Where(jumpNode => jumpNode.JumpKey == jumpNodeJumpKey)
            .ToList();
        
        if (jumpOutputNodes.Count == 0)
        {
            Debug.LogWarning($"State Graph has no JumpOutputNode for key {jumpNodeJumpKey}");
            return null;
        }

        if (jumpOutputNodes.Count > 1)
        {
            Debug.LogWarning($"State Graph has more than one jumpOutputNode for key {jumpNodeJumpKey}");
        }
        
        JumpOutputNode jumpOutputNode = jumpOutputNodes.FirstOrDefault(j => j.JumpKey == jumpNodeJumpKey);
        if (jumpOutputNode == null)
        {
            Debug.LogWarning($"State Graph has no JumpOutputNode with key {jumpNodeJumpKey}");
            return null;
        }

        StateGraphNode nextNode = GetNodeAfterPortConnection(jumpOutputNode.id, JumpOutputNode.OUT_PORT_NAME);
        return nextNode;
    }
    
    #endregion
    
    
    #region Construction Functions

    /// <summary>
    /// Adds an existing StateGraphNode instance to the graph.
    /// Nodes must be added before connections can be made involving them.
    /// </summary>
    /// <param name="node">The node instance to add.</param>
    public void AddNode(StateGraphNode node)
    {
        if (m_nodes.Any(n => n.id == node.id))
        {
            Debug.LogWarning($"Node with ID {node.id} already exists in graph {displayName}. Skipping.");
            return;
        }
        if (node.id == null) // Basic check for uninitialized nodes
        {
            Debug.LogError($"Attempted to add a node with a null ID to graph {displayName}. Node type: {node.GetType().Name}.");
            return;
        }
        m_nodes.Add(node);
        if (node is EventListenerNode listener)
        {
            // These require us to set their npcContext
            listener.SetNpcContext(GetComponent<NpcContext>());
        }
        // Ensure ports are refreshed in case they weren't fully initialized before adding.
        // This is crucial for Connect methods to find ports.
        node.RefreshPorts();
    }

    public bool ConnectStateFlow(StartNode startNode, StateNode inputNode)
    {
        // Since startNodes are interchangeable, we just add the start node if it doesn't exist
        if (!m_nodes.Contains(startNode))
        {
            m_nodes.Add(startNode);
            startNode.RefreshPorts();
        }
        
        // Ensure both nodes are actually in the graph
        if (!m_nodes.Contains(startNode) || !m_nodes.Contains(inputNode))
        {
            Debug.LogError($"One or both nodes are not part of the graph {displayName}. StartNode: {startNode.id}, InputNode: {inputNode.id}");
            return false;
        }
        
        // We now have the info to construct the required data for the generic ConnectStateFlow
        string startNodeId = startNode.id;
        NodePortInfo outputPortInfo = new NodePortInfo(StartNode.OUT_PORT_NAME, PortType.StateTransitionOut);
        string inputNodeId = inputNode.id;
        NodePortInfo inputPortInfo = new NodePortInfo(StateNode.IN_PORT_NAME, PortType.StateTransitionIn);
        
        // Then call the generic connector for any ports
        return ConnectStateFlow(startNodeId, outputPortInfo, inputNodeId, inputPortInfo);
    }

    public bool ConnectStateFlow<TOutcomeEnum>(StateNode outputNode, TOutcomeEnum outcomeEnum, ExitNode exitNode)
        where TOutcomeEnum : struct, IConvertible
    {
        // Since exitNodes are interchangeable, we just add the exit node if it doesn't exist
        if (!m_nodes.Contains(exitNode))
        {
            m_nodes.Add(exitNode);
            exitNode.RefreshPorts();
        }
        
        // Ensure both nodes are actually in the graph
        if (!m_nodes.Contains(outputNode) || !m_nodes.Contains(exitNode))
        {
            Debug.LogError($"One or both nodes are not part of the graph {displayName}. OutputNode: {outputNode.id}, ExitNode: {exitNode.id}");
            return false;
        }
        
        string outputNodeId = outputNode.id;
        // Port names for state outcomes are the string value of the enum
        string outputPortName = outcomeEnum.ToString();
        NodePortInfo outputPortInfo = new NodePortInfo(outputPortName, PortType.StateTransitionOut);
        string exitNodeId = exitNode.id;
        NodePortInfo inputPortInfo = new NodePortInfo(ExitNode.IN_PORT_NAME, PortType.StateTransitionIn);
        
        // Then call the generic connector for any ports
        return ConnectStateFlow(outputNodeId, outputPortInfo, exitNodeId, inputPortInfo);
    }

    public bool ConnectStateFlow<TOutcomeEnum>(StateNode outputNode, TOutcomeEnum outcomeEnum, RestartNode restartNode)
    {
        // Since restartNodes are interchangeable, we just add the restart node if it doesn't exist
        if (!m_nodes.Contains(restartNode))
        {
            m_nodes.Add(restartNode);
            restartNode.RefreshPorts();
        }
        
        // Ensure both nodes are actually in the graph
        if (!m_nodes.Contains(outputNode) || !m_nodes.Contains(restartNode))
        {
            Debug.LogError($"One or both nodes are not part of the graph {displayName}. OutputNode: {outputNode.id}, RestartNode: {restartNode.id}");
            return false;
        }
        
        string outputNodeId = outputNode.id;
        // Port names for state outcomes are the string value of the enum
        string outputPortName = outcomeEnum.ToString();
        NodePortInfo outputPortInfo = new NodePortInfo(outputPortName, PortType.StateTransitionOut);
        string restartNodeId = restartNode.id;
        NodePortInfo inputPortInfo = new NodePortInfo(RestartNode.IN_PORT_NAME, PortType.StateTransitionIn);
        
        // Then call the generic connector for any ports
        return ConnectStateFlow(outputNodeId, outputPortInfo, restartNodeId, inputPortInfo);
    }
    
    public bool ConnectStateFlow<TOutcomeEnum>(StateNode outputNode, TOutcomeEnum outcomeEnum, StateNode inputNode)
        where TOutcomeEnum : Enum 
    {
        // Ensure both nodes are actually in the graph
        if (!m_nodes.Contains(outputNode) || !m_nodes.Contains(inputNode))
        {
            Debug.LogError($"One or both nodes are not part of the graph {displayName}. OutputNode: {outputNode.id}, InputNode: {inputNode.id}");
            return false;
        }
        
        string outputNodeId = outputNode.id;
        // Port names for state outcomes are the string value of the enum
        string outputPortName = outcomeEnum.ToString();
        NodePortInfo outputPortInfo = new NodePortInfo(outputPortName, PortType.StateTransitionOut);
        string inputNodeId = inputNode.id;
        NodePortInfo inputPortInfo = new NodePortInfo(StateNode.IN_PORT_NAME, PortType.StateTransitionIn);
        
        // Then call the generic connector for any ports
        return ConnectStateFlow(outputNodeId, outputPortInfo, inputNodeId, inputPortInfo);
    }

    public bool ConnectStateFlow(StateGraphNode raiserNode, string raiserPortName, EventListenerNode listenerNode,
        string listenerPortName)
    {
        if (!m_nodes.Contains(raiserNode) || !m_nodes.Contains(listenerNode))
        {
            Debug.LogError($"One or both nodes are not part of the graph {displayName}. RaiserNode: {raiserNode.id}, ListenerNode: {listenerNode.id}");
            return false;
        }
        
        string raiserNodeId = raiserNode.id;
        NodePortInfo raiserPortInfo = new NodePortInfo(raiserPortName, PortType.EventOut);
        string listenerNodeId = listenerNode.id;
        NodePortInfo listenerPortInfo = new NodePortInfo(listenerPortName, PortType.EventIn);
        
        // Then call the generic connector for any ports
        return ConnectStateFlow(raiserNodeId, raiserPortInfo, listenerNodeId, listenerPortInfo);
    }
    
    public bool ConnectStateFlow(string outputNodeId, NodePortInfo outputPortInfo, string inputNodeId, NodePortInfo inputPortInfo)
    {
        // Check for existence of node
        StateGraphNode outputNode = GetNodeById(outputNodeId);
        if (outputNode == null)
        {
            Debug.LogError($"Output node with ID {outputNodeId} not found in graph {displayName}. Cannot connect.");
            return false;
        }

        StateGraphNode inputNode = GetNodeById(inputNodeId);
        if (inputNode == null)
        {
            Debug.LogError($"Input node with ID {inputNodeId} not found in graph {displayName}. Cannot connect.");
            return false;
        }
        
        // Check that these ports exist
        if (!outputNode.PortContext.ContainsKey(outputPortInfo))
        {
            Debug.LogError($"Output port {outputPortInfo.Name} not found on node {outputNodeId} in graph {displayName}. Cannot connect.");
            return false;
        }
        if (!inputNode.PortContext.ContainsKey(inputPortInfo))
        {
            Debug.LogError($"Input port {inputPortInfo.Name} not found on node {inputNodeId} in graph {displayName}. Cannot connect.");
            return false;
        }
        
        // Check that the type of the ports match
        NodePortContext outputPortContext = outputNode.PortContext[outputPortInfo];
        NodePortContext inputPortContext = inputNode.PortContext[inputPortInfo];
        if (outputPortContext.portDataType != inputPortContext.portDataType)
        {
            Debug.LogError($"Output port {outputPortInfo.Name} on node {outputNodeId} has type {outputPortContext.portDataType} " +
                           $"but input port {inputPortInfo.Name} on node {inputNodeId} has type {inputPortContext.portDataType}. Cannot connect.");
            return false;
        }
        
        // Ok, we are good to connect these ports
        StateGraphConnection connection = new StateGraphConnection(inputNodeId, inputPortInfo, outputNodeId, outputPortInfo);
        
        // Add the connection
        m_connections.Add(connection);
        return true;
    }

    public bool ConnectEvent(StateGraphNode raiserNode, string raiserPortName, EventListenerNode listenerNode,
        string listenerPortName)
    {
        // We can do this by making the NodePortInfos for the raiser and listener ports and then calling the generic ConnectStateFlow
        NodePortInfo raiserPortInfo = new NodePortInfo(raiserPortName, PortType.EventOut);
        NodePortInfo listenerPortInfo = new NodePortInfo(listenerPortName, PortType.EventIn);
       
        return ConnectStateFlow(raiserNode.id, raiserPortInfo, listenerNode.id, listenerPortInfo);
    }
    
    #endregion
    
}
