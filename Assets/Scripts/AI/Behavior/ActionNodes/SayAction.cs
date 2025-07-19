using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[BlackboardEnum]
public enum SayActionWaitMode
{
    None,  // No waiting, just show the bubble and return immediately.
    WaitForBubble,  // Wait for the bubble to finish before returning.
    WaitForDuration,  // Wait for a specified duration before returning.
    WaitForBubbleAndDuration  // Wait for the bubble to finish and then wait for an additional duration (can be negative)
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Say", story: "[Self] says [Message]", category: "Action", id: "e7d26f6292c0b780cc8a558971379f0f")]
public partial class SayAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<string> Message;
    
    [SerializeReference] public BlackboardVariable<ReadingTimeEstimatorSO> DurationEstimator;
    [SerializeReference] public BlackboardVariable<SayActionWaitMode> WaitMode = new(SayActionWaitMode.WaitForBubble);
    [SerializeReference] public BlackboardVariable<float> WaitDuration = new(1f);

    private float _textDuration = 0f;
    private float _waitTimer;
    private NpcContext _npcContext;

    protected override Status OnLoad()
    {
        if (string.IsNullOrEmpty(Message.Value))
        {
            // We use a blank message to skip the action. So we don't want to wait or show a bubble.
            return Status.Success;
        }

        if (DurationEstimator == null || DurationEstimator.Value == null)
        {
            if (GlobalData.Instance.defaultReadingTimeEstimator == null)
            {
                Debug.LogWarning("No ReadingTimeEstimatorSO set in GlobalData. Skipping SayAction.");
                return Status.Success;
            }
            DurationEstimator = new BlackboardVariable<ReadingTimeEstimatorSO>(GlobalData.Instance.defaultReadingTimeEstimator);
        }
        _textDuration = DurationEstimator.Value.Estimate(Message.Value);

        switch (WaitMode.Value)
        {
            case SayActionWaitMode.WaitForBubble:
                _waitTimer = _textDuration;
                break;
            case SayActionWaitMode.WaitForDuration:
                _waitTimer = WaitDuration.Value;
                break;
            case SayActionWaitMode.WaitForBubbleAndDuration:
                _waitTimer = _textDuration + WaitDuration.Value;
                break;
            case SayActionWaitMode.None:
                _waitTimer = 0f;  // No waiting, just show the bubble and return immediately.
                break;
        }
        
        if (!Self.Value.TryGetComponent<NpcContext>(out _npcContext))
        {
            return Status.Failure;
        }

        _npcContext.SpeechBubbleManager.ShowBubble(Message.Value, _textDuration);
        return _waitTimer > 0 ? Status.Running : Status.Success;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();
        
        if (string.IsNullOrEmpty(Message.Value))
        {
            // We use a blank message to skip the action. So we don't want to wait or show a bubble.
            return Status.Success;
        }
        
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

