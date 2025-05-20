using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class StateNodePort
{
}

/// <summary>
/// StateNodes define both an abstract configuration and a outcome enum type.
/// </summary>
[Serializable]
public abstract class StateNode : StateGraphNode
{
    public static string INTERRUPT_PORT_NAME = "Interrupted";
    public static string LOAD_IN_PORT_NAME = "LoadIn";
    public static string IN_PORT_NAME = "In";
    
    public abstract AbstractStateConfiguration GenericConfiguration { get; }
    public abstract Type OutcomeEnumType { get; }  // Defines the outcome ports
    public abstract Type StateType { get; }

    protected override List<NodePortContext> ComputePorts()
    {
        List<NodePortContext> ports = new List<NodePortContext>();
        
        // We always have two ports corresponding to being interrupted and loading in while in the state
        ports.Add(new NodePortContext(
            new NodePortInfo(INTERRUPT_PORT_NAME, PortType.StateTransitionOut),
            typeof(StateNodePort),
            -1
        ));
        ports.Add(new NodePortContext(
            new NodePortInfo(LOAD_IN_PORT_NAME, PortType.StateTransitionOut),
            typeof(StateNodePort),
            -2
        ));
        
        // Fill in the ports based on the outcome enum
        foreach (string outcome in Enum.GetNames(OutcomeEnumType))
        {
            // AddPort(outcome, PortType.StateTransitionOut);
            ports.Add(new NodePortContext(
                new NodePortInfo(outcome, PortType.StateTransitionOut),
                typeof(StateNodePort),
                Enum.Parse(OutcomeEnumType, outcome)
            ));
        }
        
        // And also the incoming port for the previous state
        ports.Add(new NodePortContext(
            new NodePortInfo(IN_PORT_NAME, PortType.StateTransitionIn),
            typeof(StateNodePort),
            null
        ));
        
        // We also use reflection to search the StateType for any events
        // Find EventOutputPorts (Events)
        EventInfo[] events = StateType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (EventInfo eventInfo in events)
        {
            EventOutputPort outputPortAttribute = eventInfo.GetCustomAttribute<EventOutputPort>();
            if (outputPortAttribute != null)
            {
                string portName = string.IsNullOrEmpty(outputPortAttribute.PortName)
                    ? eventInfo.Name
                    : outputPortAttribute.PortName;
                Type eventHandlerType =
                    eventInfo.EventHandlerType; // This is a delegate type e.g. Action, Action<T>, CustomDelegate.
                Type portDataType;

                // Try to determine payload type from common delegate types
                if (eventHandlerType == typeof(Action))
                {
                    portDataType = typeof(void); // Action means no payload.
                }
                else if (eventHandlerType.IsGenericType &&
                         eventHandlerType.GetGenericTypeDefinition() == typeof(Action<>))
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
                        Debug.LogWarning(
                            $"Event '{eventInfo.Name}' on node type '{StateType.Name}' uses delegate '{eventHandlerType.Name}' which does not have a standard Invoke method. Cannot determine PortDataType. Skipping this port.");
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
                        Debug.LogWarning(
                            $"Event '{eventInfo.Name}' on node type '{StateType.Name}' uses delegate '{eventHandlerType.Name}' which has {delegateParams.Length} parameters. Only delegates with 0 or 1 parameter are supported for automatic PortDataType inference. Skipping this port.");
                        continue;
                    }
                }
                
                ports.Add(new NodePortContext(
                    new NodePortInfo(portName, PortType.EventOut),
                    portDataType,
                    ("state", eventInfo)
                ));
            }
        }

        return ports;
    }
}

public abstract class ConfigurableStateNode<TStateConfiguration> : StateNode
    where TStateConfiguration : AbstractStateConfiguration, new()
{
    [SerializeField] private TStateConfiguration configuration; // Defines the configuration data object
    public TStateConfiguration Configuration => configuration;
    public override AbstractStateConfiguration GenericConfiguration => configuration;

    protected ConfigurableStateNode()
    {
        configuration = new TStateConfiguration();
    }

    protected ConfigurableStateNode(TStateConfiguration config)
    {
        configuration = config;
    }

    public void SetConfiguration(TStateConfiguration config)
    {
        configuration = config;
    }
}