using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StateGraph))]
public class StateGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        StateGraph stateGraphDef = (StateGraph)target;

        if (GUILayout.Button("Open State Graph Editor"))
        {
            StateGraphEditorWindow.Open(stateGraphDef);
        }

        DrawDefaultInspector();
    }
}
