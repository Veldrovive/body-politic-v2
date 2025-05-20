using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;

// TODO: Handle copy and paste
// This gives it to you straight, but no info https://discussions.unity.com/t/copy-paste-in-graph-view/247628
// This seems to list other random events https://discussions.unity.com/t/best-way-of-implementing-copy-and-pasting-of-nodes-in-graph-view/791881/4
// not sure what that's about
// Jesus ok serialization in unity is a bit of a mess. https://discussions.unity.com/t/how-do-you-serialize-your-data/924478
// I think going with the .Net json serializer is the way to go. https://discussions.unity.com/t/unity-serialization-or-json-net/928261
// Vector3s cause issues due to properties depending on itself https://discussions.unity.com/t/how-to-serialize-vector3-with-json-net/167710
// This says it fixes that https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters?tab=readme-ov-file

public class StateGraphView : GraphView
{
    private StateGraph m_stateGraph;
    private SerializedObject m_serializedObject;
    private StateGraphEditorWindow m_window;
    public StateGraphEditorWindow window => m_window;

    // Stores all the current editor nodes (editorNode.GraphNode is the underlying graphNode structure that is in the StateGraph)
    public List<StateGraphEditorNode> m_graphNodes;
    // Maps from StateGraphNode.id to StateGraphEditorNode (in which editorNode.GraphNode.id is the id)
    public Dictionary<string, StateGraphEditorNode> m_nodeDictionary;
    // Maps from the visual GraphView Edge object to the underlying StateGraphConnection object in the StateGraph
    // Used to remove the connection from the graph when the visual edge is removed.
    public Dictionary<Edge, StateGraphConnection> m_connectionDictionary;
    
    private StateGraphWindowSearchProvider m_searchProvider;
    
    private JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Objects
    };

    public StateGraphView(SerializedObject serializedObject, StateGraphEditorWindow window)
    {
        m_serializedObject = serializedObject;
        m_stateGraph = (StateGraph)serializedObject.targetObject;
        m_window = window;
        
        m_graphNodes = new List<StateGraphEditorNode>();
        m_nodeDictionary = new Dictionary<string, StateGraphEditorNode>();
        m_connectionDictionary = new Dictionary<Edge, StateGraphConnection>();
        
        m_searchProvider = ScriptableObject.CreateInstance<StateGraphWindowSearchProvider>();
        m_searchProvider.graph = this;
        this.nodeCreationRequest = ShowSearchWindow;
        

        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/StateGraph/USS/StateGraphEditor.uss");
        styleSheets.Add(style);
        
        GridBackground background = new GridBackground();
        background.name = "Grid";
        Add(background);
        background.SendToBack();
        
        ContentZoomer zoom = new ContentZoomer();
        zoom.scaleStep = 0.02f;
        this.AddManipulator(zoom);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.AddManipulator(new ClickSelector());

        DrawNodes();
        DrawConnections();

        // Callback needs to come after loading the nodes, since it will call HandleGraphViewChanged
        graphViewChanged += HandleGraphViewChanged;
        serializeGraphElements += HandleCutCopyOperation;
        unserializeAndPaste += HandlePasteOperation;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> allPorts = new List<Port>();
        List<Port> validPorts = new List<Port>();

        foreach (var node in m_graphNodes)
        {
            allPorts.AddRange(node.Ports);
        }
        
        foreach (var port in allPorts)
        {
            if (port == startPort) { continue; }
            // if (port.node == startPort.node) { continue; }
            if (port.direction == startPort.direction) { continue; }
            if (port.portType != startPort.portType) { continue; }
            
            validPorts.Add(port);
        }

        return validPorts;
    }
    
    /// <summary>
    /// This handler couples the visual representation of the graph with the underlying data structure.
    /// Except for adding nodes. That is handled by AddNodeToGraph. Anything that edits the graph is coupled to the
    /// underlying structure here.
    /// </summary>
    /// <param name="graphViewChange"></param>
    /// <returns></returns>
    private GraphViewChange HandleGraphViewChanged(GraphViewChange graphViewChange)
    {
        Debug.Log("Coupling view changes to underlying structure");
        // Handle moving of nodes
        if (graphViewChange.movedElements != null)
        {
            Debug.Log($"Elements moved: {graphViewChange.movedElements.Count()}");
            Undo.RecordObject(m_serializedObject.targetObject, "Moved Graph Element(s)");
            foreach (StateGraphEditorNode editorNode in graphViewChange.movedElements.OfType<StateGraphEditorNode>())
            {
                // editorNode.GetPosition() will now return the new position. We copy that to the graphNode.
                editorNode.SavePosition();
            }
        }
        
        // Handle node and edge deletion
        if (graphViewChange.elementsToRemove != null)
        {
            // Debug.Log($"Elements removed: {graphViewChange.elementsToRemove.Count()}");
            Undo.RecordObject(m_serializedObject.targetObject, "Removed Graph Element(s)");
            
            // Remove Nodes
            List<StateGraphEditorNode> nodesToRemove = graphViewChange.elementsToRemove.OfType<StateGraphEditorNode>().ToList();
            Debug.Log($"Nodes removed: {nodesToRemove.Count}" );
            
            // These are now the editor nodes that are being removed. Now we need to remove these from the graph.
            // Or actually we remove the editorNode.m_graphNode from the graph.
            if (nodesToRemove.Count > 0)
            {
                for (int i = nodesToRemove.Count - 1; i >= 0; i--)
                {
                    RemoveNode(nodesToRemove[i]);
                }
            }
            
            // Remove Edges
            List<Edge> edgesToRemove = graphViewChange.elementsToRemove.OfType<Edge>().ToList();
            Debug.Log($"Edges removed: {edgesToRemove.Count}" );
            // These are the edges that are being removed. We need to remove the connection from the graph.
            if (edgesToRemove.Count > 0)
            {
                for (int i = edgesToRemove.Count - 1; i >= 0; i--)
                {
                    RemoveConnection(edgesToRemove[i]);
                }
            }
        }
        
        // Handle edge creation
        if (graphViewChange.edgesToCreate != null)
        {
            Undo.RecordObject(m_serializedObject.targetObject, "Created Graph Edge(s)");
            foreach (Edge edge in graphViewChange.edgesToCreate)
            {
                CreateEdge(edge);
            }
        }

        return graphViewChange;  // Internal handling
    }
    
    private void CreateEdge(Edge edge)
    {
        StateGraphEditorNode inputNode = edge.input.node as StateGraphEditorNode;
        StateGraphEditorNode outputNode = edge.output.node as StateGraphEditorNode;

        NodePortInfo inputPort = inputNode.GetPortInfoByPort(edge.input);
        NodePortInfo outputPort = outputNode.GetPortInfoByPort(edge.output);

        StateGraphConnection connection = new(inputNode.GraphNode.id, inputPort, outputNode.GraphNode.id, outputPort);
        m_stateGraph.Connections.Add(connection);
        m_serializedObject.Update();
        
        m_connectionDictionary.Add(edge, connection);
    }

    private void RemoveConnection(Edge edge)
    {
        if (m_connectionDictionary.TryGetValue(edge, out StateGraphConnection connection))
        {
            m_stateGraph.Connections.Remove(connection);
            m_serializedObject.Update();
            
            m_connectionDictionary.Remove(edge);
            RemoveElement(edge);
        }
    }

    /// <summary>
    /// Handler for HandleGraphViewChanged elementsToRemove. 
    /// Tight coupling function between the StateGraphView and the StateGraph. Removes the graphEditorNode from the view
    /// and the graphNode from the graph.
    /// </summary>
    /// <param name="stateGraphEditorNode"></param>
    private void RemoveNode(StateGraphEditorNode stateGraphEditorNode)
    {
        m_stateGraph.Nodes.Remove(stateGraphEditorNode.GraphNode);
        m_serializedObject.Update();  // We updated the target of the serialized object, so we need to update the serialized object.
        
        m_graphNodes.Remove(stateGraphEditorNode);
        m_nodeDictionary.Remove(stateGraphEditorNode.GraphNode.id);
        // If this was called from HandleGraphViewChanged we don't need to also remove the editorNode from the GraphView,
        // since we are handling that removal. However, if this was called externally, using RemoveElement will also
        // remove the visual element from the graph.
        RemoveElement(stateGraphEditorNode);
    }

    private void DrawNodes()
    {
        foreach (StateGraphNode node in m_stateGraph.Nodes)
        {
            AddNodeToGraph(node);
        }
    }

    private void DrawConnections()
    {
        if (m_stateGraph.Connections == null) { return; }

        foreach (var connection in m_stateGraph.Connections)
        {
            DrawConnection(connection);
        }
    }

    private void DrawConnection(StateGraphConnection connection)
    {
        StateGraphEditorNode inputNode = GetNode(connection.inputPort.nodeId);
        StateGraphEditorNode outputNode = GetNode(connection.outputPort.nodeId);
        
        if (inputNode == null || outputNode == null)
        {
            Debug.LogError($"Connection between {connection.inputPort.nodeId} and {connection.outputPort.nodeId} could not be created. One of the nodes is null.");
            return;
        }
        
        // Port inputPort = inputNode.Ports[connection.inputPort.portIndex];
        // Port outputPort = outputNode.Ports[connection.outputPort.portIndex];
        Port inputPort = inputNode.GetPortByInfo(connection.inputPort.portInfo);
        Port outputPort = outputNode.GetPortByInfo(connection.outputPort.portInfo);

        if (inputPort == null || outputPort == null)
        {
            // The port was not found. This can happen if the port was removed or renamed.
            // For now we just log an error and skip the connection.
            int inputPortIndex = inputNode.Ports.IndexOf(inputPort);
            Debug.LogError($"Connection {inputPortIndex} between {connection.inputPort.nodeId} and {connection.outputPort.nodeId} could not be created. One of the ports is null.");
            return;
        }
        
        Edge edge = inputPort.ConnectTo(outputPort);
        AddElement(edge);
        m_connectionDictionary.Add(edge, connection);
    }

    private StateGraphEditorNode GetNode(string nodeId)
    {
        if (m_nodeDictionary.TryGetValue(nodeId, out StateGraphEditorNode node))
        {
            return node;
        }
        else
        {
            Debug.LogError($"Node with id {nodeId} not found in dictionary.");
            return null;
        }
    }

    private void ShowSearchWindow(NodeCreationContext obj)
    {
        m_searchProvider.target = (VisualElement)focusController.focusedElement;
        SearchWindow.Open(new SearchWindowContext(obj.screenMousePosition), m_searchProvider);
    }

    public void Add(StateGraphNode node)
    {
        Undo.RecordObject(m_serializedObject.targetObject, "Added Node");
        m_stateGraph.Nodes.Add(node);
        m_serializedObject.Update();

        AddNodeToGraph(node);
    }

    private void AddNodeToGraph(StateGraphNode node)
    {
        node.typeName = node.GetType().AssemblyQualifiedName;

        SerializedProperty nodeProperty = GetStateGraphNodeProperty(node);
        StateGraphEditorNode editorNode = new StateGraphEditorNode(node, nodeProperty);
        editorNode.SetPosition(node.position);
        
        m_graphNodes.Add(editorNode);
        m_nodeDictionary.Add(node.id, editorNode);
        
        AddElement(editorNode);
    }

    /// <summary>
    /// Searches through the state graph SerializedObject for the SerializedProperty that corresponds to the StateGraphNode.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private SerializedProperty GetStateGraphNodeProperty(StateGraphNode nodeInstance)
    {
        SerializedProperty nodesArrayProperty = m_serializedObject.FindProperty("m_nodes");
        if (nodesArrayProperty != null && nodesArrayProperty.isArray)
        {
            for (int i = 0; i < nodesArrayProperty.arraySize; i++)
            {
                SerializedProperty sp = nodesArrayProperty.GetArrayElementAtIndex(i);
                if (sp.managedReferenceValue == nodeInstance)
                {
                    return sp;
                }
            }
        }
        Debug.LogError($"SerializedProperty not found for node instance with ID: {nodeInstance.id}. This should not happen if the node is in StateGraph.Nodes and m_serializedObject is updated.");
        return null;
    }
    
    /// <summary>
    /// Handles the paste operation with the provided serialized data.
    /// </summary>
    /// <param name="operationName">The name of the paste operation.</param>
    /// <param name="data">The serialized JSON string containing node data.</param>
    private void HandlePasteOperation(string operationName, string data)
    {
        Debug.Log($"{operationName} attempted with data: {data}");

        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning("No data to paste.");
            return;
        }

        // Deserialize the JSON string into a list of StateGraphNode objects
        List<StateGraphNode> nodesToPaste = JsonConvert.DeserializeObject<List<StateGraphNode>>(data, jsonSettings);
        if (nodesToPaste == null || nodesToPaste.Count == 0)
        {
            Debug.LogWarning("No nodes to paste.");
            return;
        }
    }

    /// <summary>
    /// Handles the cut or copy operation for selected graph elements.
    /// </summary>
    /// <param name="elements">The elements selected for cut or copy.</param>
    /// <returns>A JSON string representing the serializable data of the selected nodes.</returns>
    private string HandleCutCopyOperation(IEnumerable<GraphElement> elements)
    {
        Debug.Log($"Cut/Copy attempted");

        // Collect the underlying serializable node data
        List<string> nodeIdsToCopy = new List<string>();
        List<StateGraphNode> nodesToCopy = new List<StateGraphNode>();
        foreach (var element in elements)
        {
            if (element is StateGraphEditorNode editorNode)
            {
                // Add the underlying serializable node data to the list
                nodeIdsToCopy.Add(editorNode.GraphNode.id);
                nodesToCopy.Add(editorNode.GraphNode);
            }
        }

        if (nodesToCopy.Count == 0)
        {
            Debug.Log("No nodes selected for copy/cut.");
            return ""; // Return an empty string if nothing is selected
        }

        string jsonNodes = JsonConvert.SerializeObject(nodesToCopy, jsonSettings);
        return jsonNodes;

        // return string.Join("\n", nodeIdsToCopy);
    }
}
