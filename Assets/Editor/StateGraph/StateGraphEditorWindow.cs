using UnityEditor;
using UnityEngine;
using System.Collections;
using System;
using UnityEditor.Experimental.GraphView;

public class StateGraphEditorWindow : EditorWindow
{
    [SerializeField]
    private StateGraph m_currentGraph;

    public StateGraph currentGraph => m_currentGraph;
    
    [SerializeField]
    private SerializedObject m_serializedObject;
    
    [SerializeField]
    private StateGraphView m_currentView;

    public static void Open(StateGraph target)
    {
        // Don't reopen if there is already a window with this target open. just focus it
        StateGraphEditorWindow[] openWindows = Resources.FindObjectsOfTypeAll<StateGraphEditorWindow>();
        foreach (StateGraphEditorWindow otherWindow in openWindows)
        {
            if (otherWindow.currentGraph == target)
            {
                otherWindow.Focus();
                return;
            }
        }
        
        // Create a new window
        StateGraphEditorWindow window = CreateWindow<StateGraphEditorWindow>(typeof(StateGraphEditorWindow), typeof(SceneView));
        window.titleContent = new GUIContent($"StateGraph - {target}");
        window.Load(target);
    }

    private void OnEnable()
    {
        if (m_currentGraph != null)
        {
            DrawGraph();
        }
    }

    private void OnGUI()
    {
        if (m_currentGraph != null)
        {
            // I think this IsDirty stuff is more important for assets than components.
            // This will just force you to save the scene if you have unsaved changes.
            if (EditorUtility.IsDirty(m_currentGraph))
            {
                this.hasUnsavedChanges = true;
            }
            else
            {
                this.hasUnsavedChanges = false;
            }
        }
    }

    public void Load(StateGraph target)
    {
        m_currentGraph = target;
        DrawGraph();
    }

    private void DrawGraph()
    {
        m_serializedObject = new SerializedObject(m_currentGraph);
        m_currentView = new StateGraphView(m_serializedObject, this);
        m_currentView.graphViewChanged += HandleGraphChanged;
        rootVisualElement.Add(m_currentView);
    }

    private GraphViewChange HandleGraphChanged(GraphViewChange graphViewChange)
    {
        // Tell the user that they should save the scene when they try to close the window.
        EditorUtility.SetDirty(m_currentGraph);
        return graphViewChange;
    }
}
