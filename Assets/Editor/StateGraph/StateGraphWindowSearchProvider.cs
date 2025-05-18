using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public struct SearchContextElement
{
    public object target { get; private set; }
    public string title { get; private set; }

    public SearchContextElement(object target, string title)
    {
        this.target = target;
        this.title = title;
    }
}

public class StateGraphWindowSearchProvider : ScriptableObject, ISearchWindowProvider
{
    public StateGraphView graph;
    public VisualElement target;

    public static List<SearchContextElement> elements;
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
        tree.Add(new SearchTreeGroupEntry(new GUIContent("Nodes"), 0));
        
        elements = new List<SearchContextElement>();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.CustomAttributes.ToList() != null)
                {
                    var attribute = type.GetCustomAttribute(typeof(NodeInfoAttribute));
                    if (attribute != null)
                    {
                        NodeInfoAttribute att = (NodeInfoAttribute)attribute;
                        var node = Activator.CreateInstance(type);
                        if (string.IsNullOrEmpty(att.MenuItem))
                        {
                            continue; 
                        }
                        elements.Add(new SearchContextElement(node, att.MenuItem));
                    }
                }
            }
        }
        
        elements.Sort((e1, e2) =>
        {
            string[] splits1 = e1.title.Split('/');
            string[] splits2 = e2.title.Split('/');
            for (int i = 0; i < splits1.Length; i++)
            {
                if (i >= splits2.Length)
                {
                    return 1;
                }
                int value = splits1[i].CompareTo(splits2[i]);
                if (value != 0)
                {
                    if (splits1.Length != splits2.Length && (i == splits1.Length - 1 || i == splits2.Length - 1))
                    {
                        return splits1.Length < splits2.Length ? 1 : -1;
                    }

                    return value;
                }
            }

            return 0;
        });

        List<string> groups = new List<string>();

        foreach (SearchContextElement element in elements)
        {
            string[] entryTitle = element.title.Split('/');
            string groupName = "";

            for (int i = 0; i < entryTitle.Length - 1; i++)
            {
                groupName += entryTitle[i];
                if (!groups.Contains(groupName))
                {
                    tree.Add(new SearchTreeGroupEntry(new GUIContent(entryTitle[i]), i+1));
                    groups.Add(groupName);
                }

                groupName += "/";
            }
            
            SearchTreeEntry entry = new SearchTreeEntry(new GUIContent(entryTitle.Last()));
            entry.level = entryTitle.Length;
            entry.userData = new SearchContextElement(element.target, element.title);
            tree.Add(entry);
        }
        
        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        var windowMousePosition = graph.ChangeCoordinatesTo(graph, context.screenMousePosition - graph.window.position.position);
        var graphMousePosition = graph.contentViewContainer.WorldToLocal(windowMousePosition);

        SearchContextElement element = (SearchContextElement)entry.userData;
        StateGraphNode node = (StateGraphNode)element.target;
        node.SetPosition(new Rect(graphMousePosition, new Vector2()));
        graph.Add(node);

        return true;
    }
}
