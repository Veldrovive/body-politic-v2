using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public enum PortType
{
    StateTransitionOut,
    StateTransitionIn,
    EventOut,
    EventIn
}

[System.Serializable]
public class NodePortInfo
{
    public string Name;
    public PortType PortType;
    
    public NodePortInfo(string name, PortType portType)
    {
        Name = name;
        PortType = portType;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is NodePortInfo other)
        {
            return Name == other.Name && PortType == other.PortType;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, PortType);
    }
}

public struct NodePortContext
{
    public NodePortInfo portInfo;
    public Type portDataType;
    public object portContext;
    
    public NodePortContext(NodePortInfo portInfo, Type portDataType, object portContext)
    {
        this.portInfo = portInfo;
        this.portDataType = portDataType;
        this.portContext = portContext;
    }
}

[System.Serializable]
public abstract class StateGraphNode
{
    [SerializeField] private string m_guid;

    [SerializeField] private Rect m_position;
    
    [SerializeField] private List<NodePortInfo> m_ports;
    private Dictionary<NodePortInfo, NodePortContext> m_portContext;
    public List<NodePortInfo> Ports => m_ports;
    public Dictionary<NodePortInfo, NodePortContext> PortContext => m_portContext;

    public string typeName;

    public string id => m_guid;
    public Rect position => m_position;

    public StateGraphNode()
    {
        m_ports = new List<NodePortInfo>();
        m_portContext = new Dictionary<NodePortInfo, NodePortContext>();
        RefreshPorts();
        NewGUID();
    }

    protected virtual List<NodePortContext> ComputePorts()
    {
        return null;
    }

    /// <summary>
    /// Called when displaying the node in the editor, or when port information needs to be up-to-date.
    /// It clears existing ports and recomputes them based on structural definitions and reflection for event ports.
    /// </summary>
    public void RefreshPorts()
    {
        m_ports.Clear();
        List<NodePortContext> computedPorts = ComputePorts();
        if (computedPorts != null)
        {
            foreach (var portContext in computedPorts)
            {
                m_ports.Add(portContext.portInfo);
                m_portContext[portContext.portInfo] = portContext;
            }
        }
        List<NodePortContext> eventPorts = FindEventPorts();
        foreach (var portContext in eventPorts)
        {
            m_ports.Add(portContext.portInfo);
            m_portContext[portContext.portInfo] = portContext;
        }
    }

    /// <summary>
    /// Finds all methods on the derived class that are marked with the EventInputPort attribute
    /// and events marked with the EventOutputPort attribute.
    /// It assigns them a name based on the attribute or member name, and determines a PortDataType
    /// representing the data payload (using typeof(void) for parameterless/trigger-like ports).
    /// </summary>
    private List<NodePortContext> FindEventPorts()
    {
        List<NodePortContext> eventPorts = new List<NodePortContext>();
        Type nodeType = this.GetType();

        // Find EventInputPorts (Methods)
        // BindingFlags.DeclaredOnly ensures we only process attributes declared directly on this specific type.
        MethodInfo[] methods = nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (MethodInfo method in methods)
        {
            EventInputPort inputPortAttribute = method.GetCustomAttribute<EventInputPort>();
            if (inputPortAttribute != null)
            {
                string portName = string.IsNullOrEmpty(inputPortAttribute.PortName) ? method.Name : inputPortAttribute.PortName;
                Type portDataType;
                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length == 0)
                {
                    portDataType = typeof(void); // Represents a trigger, no data payload.
                }
                else if (parameters.Length == 1)
                {
                    portDataType = parameters[0].ParameterType;
                }
                else
                {
                    Debug.LogWarning($"Method '{method.Name}' on node type '{nodeType.Name}' with [EventInputPort] has {parameters.Length} parameters. Only methods with 0 or 1 parameter are supported for automatic port generation. Skipping this port.");
                    continue; // Skip creating a port for this multi-parameter method.
                }
                
                eventPorts.Add(new NodePortContext(
                    new NodePortInfo(portName, PortType.EventIn),
                    portDataType,
                    method
                ));
            }
        }

        // Find EventOutputPorts (Events)
        EventInfo[] events = nodeType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (EventInfo eventInfo in events)
        {
            EventOutputPort outputPortAttribute = eventInfo.GetCustomAttribute<EventOutputPort>();
            if (outputPortAttribute != null)
            {
                string portName = string.IsNullOrEmpty(outputPortAttribute.PortName) ? eventInfo.Name : outputPortAttribute.PortName;
                Type eventHandlerType = eventInfo.EventHandlerType; // This is a delegate type e.g. Action, Action<T>, CustomDelegate.
                Type portDataType;

                // Try to determine payload type from common delegate types
                if (eventHandlerType == typeof(Action))
                {
                    portDataType = typeof(void); // Action means no payload.
                }
                else if (eventHandlerType.IsGenericType && eventHandlerType.GetGenericTypeDefinition() == typeof(Action<>))
                {
                    portDataType = eventHandlerType.GetGenericArguments()[0]; // Payload is T from Action<T>.
                }
                // Add more specific handlers if needed e.g. Action<T1, T2> -> Tuple<T1,T2>
                // For now, fallback to inspecting the delegate's Invoke method for simpler cases.
                else
                {
                    MethodInfo invokeMethod = eventHandlerType.GetMethod("Invoke");
                    if (invokeMethod == null)
                    {
                        Debug.LogWarning($"Event '{eventInfo.Name}' on node type '{nodeType.Name}' uses delegate '{eventHandlerType.Name}' which does not have a standard Invoke method. Cannot determine PortDataType. Skipping this port.");
                        continue;
                    }

                    ParameterInfo[] delegateParams = invokeMethod.GetParameters();
                    if (delegateParams.Length == 0)
                    {
                        portDataType = typeof(void);
                    }
                    else if (delegateParams.Length == 1)
                    {
                        portDataType = delegateParams[0].ParameterType;
                    }
                    else
                    {
                        Debug.LogWarning($"Event '{eventInfo.Name}' on node type '{nodeType.Name}' uses delegate '{eventHandlerType.Name}' which has {delegateParams.Length} parameters. Only delegates with 0 or 1 parameter are supported for automatic PortDataType inference. Skipping this port.");
                        continue; 
                    }
                }
                eventPorts.Add(new NodePortContext(
                    new NodePortInfo(portName, PortType.EventOut),
                    portDataType,
                    eventInfo
                ));
            }
        }
        
        return eventPorts;
    }

    public void NewGUID()
    {
        m_guid = System.Guid.NewGuid().ToString();
    }

    public void SetPosition(Rect position)
    {
        m_position = position;
    }
}
