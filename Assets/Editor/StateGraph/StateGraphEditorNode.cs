using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class StateGraphEditorNode : Node
{
    private StateGraphNode m_graphNode; // The actual data node instance
    public StateGraphNode GraphNode => m_graphNode;

    private SerializedProperty m_nodeSerializedProperty; // The SerializedProperty for m_graphNode

    private List<Port> m_ports;
    public List<Port> Ports => m_ports;

    private Dictionary<NodePortInfo, Port> m_portMap;

    private Type typeInfo;
    private NodeInfoAttribute info;

    private void SetTitle()
    {
        if (m_graphNode is StateNode stateNode && !string.IsNullOrEmpty(stateNode.GenericConfiguration.StateName))
        {
            title = $"{stateNode.GenericConfiguration.StateName} ({typeInfo.Name})";
        } else if (m_graphNode is JumpOutputNode jumpOutNode && !string.IsNullOrEmpty(jumpOutNode.JumpKey))
        {
            title = $"Jump from: {jumpOutNode.JumpKey}";
        } else if (m_graphNode is JumpInputNode jumpIntNode && !string.IsNullOrEmpty(jumpIntNode.JumpKey))
        {
            title = $"Jump to: {jumpIntNode.JumpKey}";
        }
        else
        {
            title = info.Title;
        }
    }

    public StateGraphEditorNode(StateGraphNode nodeInstance, SerializedProperty nodeSerializedProperty)
    {
        AddToClassList("state-graph-node");
        m_graphNode = nodeInstance; // Store the direct instance
        m_nodeSerializedProperty = nodeSerializedProperty; // Store the specific SP for this node
        m_ports = new List<Port>();
        m_portMap = new Dictionary<NodePortInfo, Port>();

        typeInfo = m_graphNode.GetType(); // Use the instance
        info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();

        if (info.NodeWidth != null)
        {
            this.style.minWidth = info.NodeWidth.Value;
        }

        SetTitle();

        string[] depths = info.MenuItem.Split('/');
        foreach (string depth in depths)
        {
            this.AddToClassList(depth.ToLower().Replace(' ', '-'));
        }

        this.name = typeInfo.Name;

        CreatePorts(); // Call port creation logic
        CreateInspector();
        
        RefreshExpandedState();
        RefreshPorts();
    }

    private void CreateInspector()
    {
        HashSet<string> internalFieldNames = new HashSet<string>
        {
            "m_guid",
            "m_position",
            "typeName",
            "m_ports",
            "StateId"
            // "m_Script" // Typically not an issue for SerializeReference fields
        };

        List<SerializedProperty> propertiesToDraw = new List<SerializedProperty>();
        
        // Iterate over the direct children (serialized fields) of the m_nodeSerializedProperty
        SerializedProperty currentChildProperty = m_nodeSerializedProperty.Copy();
        SerializedProperty endProperty = m_nodeSerializedProperty.GetEndProperty();

        // Move to the first child of m_nodeSerializedProperty
        if (currentChildProperty.NextVisible(true)) 
        {
            do
            {
                // Check if we've iterated past all children of m_nodeSerializedProperty
                if (SerializedProperty.EqualContents(currentChildProperty, endProperty))
                    break;
                
                // We only want direct children. The iteration pattern NextVisible(true) then NextVisible(false)
                // ensures we are iterating siblings at the correct depth.
                // A depth check can be an extra safeguard but is often not needed with this pattern.
                // if (currentChildProperty.depth != m_nodeSerializedProperty.depth + 1) continue;


                if (!internalFieldNames.Contains(currentChildProperty.name))
                {
                    propertiesToDraw.Add(currentChildProperty.Copy()); // Copy() is important as currentChildProperty is a 'cursor'
                }
            }
            while (currentChildProperty.NextVisible(false)); // Move to the next sibling
        }

        if (propertiesToDraw.Count > 0)
        {
            IMGUIContainer inspector = new IMGUIContainer(() =>
            {
                // Use the serializedObject from the node's own SerializedProperty (which is the StateGraph SO)
                m_nodeSerializedProperty.serializedObject.Update();
            
                EditorGUI.BeginChangeCheck();
                foreach(var prop in propertiesToDraw)
                {
                    // Draw the property. 'true' allows it to draw children if the property is a complex object.
                    EditorGUILayout.PropertyField(prop, true);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    m_nodeSerializedProperty.serializedObject.ApplyModifiedProperties();
                    // Re-apply the title
                    SetTitle();
                }
            });
            inspector.style.paddingLeft = 5;
            inspector.style.paddingRight = 5;
            inspector.style.paddingTop = 5;
            inspector.style.paddingBottom = 5;
            inspector.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f); // Optional: Style your inspector
            extensionContainer.Add(inspector);
        }
    }

    public Port GetPortByInfo(NodePortInfo portInfo)
    {
        return m_portMap.TryGetValue(portInfo, out Port port) ? port : null;
    }

    public NodePortInfo GetPortInfoByPort(Port port)
    {
        foreach (KeyValuePair<NodePortInfo, Port> pair in m_portMap)
        {
            if (pair.Value == port)
            {
                return pair.Key;
            }
        }
        Debug.LogError($"Port {port} not found in port map!");
        return default;
    }
    
    private void CreatePorts()
    {
        m_graphNode.RefreshPorts();
        foreach (NodePortInfo portInfo in m_graphNode.Ports)
        {
            Direction dir;
            Port.Capacity capacity;
            Orientation orientation;
            VisualElement container = null;
            Type portType = m_graphNode.PortContext[portInfo].portDataType;
            switch (portInfo.PortType)
            {
                case PortType.EventIn:
                    dir = Direction.Input;
                    capacity = Port.Capacity.Single;  // Event subscriber subscribes to a single event
                    orientation = Orientation.Vertical;
                    container = inputContainer;
                    break;
                case PortType.EventOut:
                    dir = Direction.Output;
                    capacity = Port.Capacity.Multi;  // Event provider can be listened to by multiple subscribers
                    orientation = Orientation.Vertical;
                    container = outputContainer;
                    break;
                case PortType.StateTransitionIn:
                    dir = Direction.Input;
                    capacity = Port.Capacity.Multi;  // Multiple state outcomes can flow to this state
                    orientation = Orientation.Horizontal;
                    container = inputContainer;
                    break;
                case PortType.StateTransitionOut:
                    dir = Direction.Output;
                    capacity = Port.Capacity.Single;  // You can only flow to one state at a time
                    orientation = Orientation.Horizontal;
                    container = outputContainer;
                    break;
                default:
                    Debug.LogError($"Port type {portInfo.PortType} not supported!");
                    continue;
            }
            
            Port port = InstantiatePort(orientation, dir, capacity, portType);
            port.portName = portInfo.Name;
            port.name = portInfo.Name;  // Used as a unique id for connections
            port.tooltip = $"{portInfo.PortType} port";
            
            m_ports.Add(port);
            m_portMap.Add(portInfo, port);
            container.Add(port);
        }
    }

    public void SavePosition()
    {
        m_graphNode.SetPosition(this.GetPosition());
    }
}