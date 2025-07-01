using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Pause", story: "Pause Execution", category: "Action/Debug", id: "52c44ce733885614b3cae452fd2eab64")]
public partial class PauseAction : Action
{

    protected override Status OnStart()
    {
#if UNITY_EDITOR
        // In the editor, we can pause the game to inspect the state.
        UnityEditor.EditorApplication.isPaused = true;
#endif
        return Status.Success;
    }
}

