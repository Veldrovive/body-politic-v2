using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class NodeInfoAttribute : Attribute
{
    private string m_nodeTitle;
    private string m_menuItem;
    private int? m_nodeWidth;
    
    public string Title => m_nodeTitle;
    public string MenuItem => m_menuItem;
    public int? NodeWidth => m_nodeWidth;

    public NodeInfoAttribute(string nodeTitle, string menuItem = "", int nodeWidth = -1)
    {
        m_nodeTitle = nodeTitle;
        m_menuItem = menuItem;
        m_nodeWidth = nodeWidth == -1 ? null : nodeWidth;
    }
}

[AttributeUsage(AttributeTargets.Event)]
public class EventOutputPort : Attribute
{
    public string PortName { get; }
    public EventOutputPort(string name = null)
    {
        PortName = name;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class EventInputPort : Attribute
{
    public string PortName { get; }
    public EventInputPort(string name = null)
    {
        PortName = name;
    }
}