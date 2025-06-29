using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Say", story: "[Self] says [Message]", category: "Action", id: "e7d26f6292c0b780cc8a558971379f0f")]
public partial class SayAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<string> Message;
    
    [SerializeReference] public BlackboardVariable<float> TextDuration = new(3f);
    [SerializeReference] public BlackboardVariable<float> WaitDuration = new(3f);
    
    private float _waitTimer;
    private NpcContext _npcContext;

    protected override Status OnLoad()
    {
        if (string.IsNullOrEmpty(Message.Value))
        {
            return Status.Success;
        }
        _waitTimer = WaitDuration.Value;
        if (!Self.Value.TryGetComponent<NpcContext>(out _npcContext))
        {
            return Status.Failure;
        }

        _npcContext.SpeechBubbleManager.ShowBubble(Message.Value, TextDuration.Value);
        return TextDuration.Value > 0 ? Status.Running : Status.Success;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();
        
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0)
        {
            return Status.Success;
        }
        else
        {
            return Status.Running;
        }
    }

    protected override void OnEnd()
    {
        base.OnEnd();
        // We don't need to do any cleanup. The speech bubble will automatically hide after the duration.
    }
}

