using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Increment Int", story: "Increment [Int]", category: "Action", id: "3e87dbf49dd8c37bb546a826f3ebb65d")]
public partial class IncrementIntAction : Action
{
    [SerializeReference] public BlackboardVariable<int> Int;

    protected override Status OnStart()
    {
        if (Int == null)
        {
            return Status.Failure;
        }
        else
        {
            Int.Value++;
            return Status.Success;
        }
    }
}

